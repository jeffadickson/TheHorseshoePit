// SPDX-License-Identifier: GPL-3.0-or-later
// A bounded, single-producer/single-consumer queue for named regular inputs.
// Requirements: READAHEAD-001..004. Matcher and line framing remain GNU grep's.
#include "read-ahead.h"
#include "queue-profile.h"
#if defined(QUEUE_COPY_KIND) && QUEUE_COPY_KIND != 0
#include "memory-copy.h"
#endif
#include <algorithm>
#include <cerrno>
#include <condition_variable>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>
#include <sys/stat.h>
#include <unistd.h>

extern "C" size_t safe_read(int, void *, size_t);

namespace {
// An invalid tuning value disables the experiment rather than risking an
// unbounded allocation. Defaults reserve 768 KiB, excluding library overhead.
size_t setting(const char *name, size_t fallback, size_t low, size_t high) {
    const char *text = std::getenv(name);
    if (!text) return fallback;
    if (!*text || *text == '-' || *text == '+') return 0;
    char *end = nullptr;
    errno = 0;
    const auto value = std::strtoull(text, &end, 10);
    return errno || *end || value < low || value > high
        ? 0 : static_cast<size_t>(value);
}

struct Slot {
    std::unique_ptr<char[]> bytes;
    size_t capacity = 0;
    size_t size = 0;
    int error = 0;
};

class Reader final {
    int fd_;
    off_t delivered_;
    std::vector<Slot> slots_;
    std::mutex mutex_;
    std::condition_variable changed_;
    size_t head_ = 0, tail_ = 0, ready_ = 0, consumed_ = 0;
    bool stopping_ = false;
#ifdef QUEUE_PROFILE
    queue_profile::Data profile_;
#endif
    // Declared last so all state exists before the thread starts. Shutdown
    // explicitly wakes the condition variable before joining this jthread.
    std::jthread producer_;

    void produce() {
#ifdef QUEUE_PROFILE
        queue_profile::ProducerCpu cpu_scope {profile_};
#endif
        off_t offset = delivered_;
        for (;;) {
            QP_STAMP(lock_start);
            std::unique_lock lock(mutex_);
            QP_ADD(producer_lock_ns, lock_start);
            if (!stopping_ && ready_ == slots_.size()) {
                QP_INC(full_waits);
                QP_STAMP(wait_start);
                changed_.wait(lock, [this] { return stopping_ || ready_ < slots_.size(); });
                QP_ADD(producer_wait_ns, wait_start);
            }
            if (stopping_) return;
            auto &slot = slots_[tail_];
            // This slot is producer-owned until published under the mutex.
            // pread leaves the public descriptor's offset untouched, so queued
            // bytes cannot change grep's observable logical position.
            lock.unlock();
            ssize_t bytes;
            QP_STAMP(read_start);
            do {
                bytes = ::pread(fd_, slot.bytes.get(), slot.capacity, offset);
            } while (bytes < 0 && errno == EINTR);
            const int error = bytes < 0 ? errno : 0;
            QP_ADD(producer_read_ns, read_start);
            QP_INC(read_calls);
            if (bytes > 0) { QP_BYTES(read_bytes, static_cast<size_t>(bytes)); }
            QP_STAMP(publish_start);
            lock.lock();
            QP_ADD(producer_lock_ns, publish_start);
            if (stopping_) return;
            slot.size = bytes > 0 ? static_cast<size_t>(bytes) : 0;
            slot.error = error;
            if (++tail_ == slots_.size()) tail_ = 0;
            ++ready_;
#ifdef QUEUE_PROFILE
            profile_.max_ready = std::max(profile_.max_ready, static_cast<std::uint64_t>(ready_));
#endif
            lock.unlock();
            changed_.notify_one();
            if (bytes <= 0) return; // Publish exactly one sticky terminal slot.
            offset += bytes;
        }
    }
public:
    Reader(int fd, off_t offset, size_t chunk, size_t depth)
        : fd_(fd), delivered_(offset), slots_(depth) {
        // The producer overwrites the allocation. Only slot.size published
        // bytes are readable; zero initialization would be redundant traffic.
        for (auto &slot : slots_) {
            slot.bytes = std::make_unique_for_overwrite<char[]>(chunk);
            slot.capacity = chunk;
        }
        producer_ = std::jthread([this] { produce(); });
    }
    Reader(const Reader &) = delete;
    Reader &operator=(const Reader &) = delete;
    ~Reader() { stop(); }
    int descriptor() const { return fd_; }
    off_t position() const { return delivered_; }
    void stop() {
        QP_STAMP(stop_start);
        {
            std::lock_guard lock(mutex_);
            stopping_ = true;
        }
        changed_.notify_all();
        if (producer_.joinable()) producer_.join();
        QP_ADD(stop_ns, stop_start);
    }
#ifdef QUEUE_PROFILE
    void emit_profile() const { profile_.emit(); }
#endif
    size_t read(void *buffer, size_t count) {
        if (!count) return 0;
        QP_STAMP(lock_start);
        std::unique_lock lock(mutex_);
        QP_ADD(consumer_lock_ns, lock_start);
        if (!ready_) {
            QP_INC(empty_waits);
            QP_STAMP(wait_start);
            changed_.wait(lock, [this] { return ready_ != 0; });
            QP_ADD(consumer_wait_ns, wait_start);
        }
        auto &slot = slots_[head_];
        if (!slot.size) {
            if (slot.error) { errno = slot.error; return static_cast<size_t>(-1); }
            return 0;
        }
        const auto bytes = std::min(count, slot.size - consumed_);
        // The extra copy keeps grep's alignment, sentinels and partial-line
        // retention unchanged. Its cost is deliberately included in timings.
        // Keep the published slot reserved until copying finishes. The producer
        // can fill other free slots while this consumer owns these bytes.
        lock.unlock();
        QP_STAMP(copy_start);
#if defined(QUEUE_COPY_KIND) && QUEUE_COPY_KIND != 0
        queue_memory_copy(buffer, slot.bytes.get() + consumed_, bytes);
#else
        std::memcpy(buffer, slot.bytes.get() + consumed_, bytes);
#endif
        QP_ADD(copy_ns, copy_start);
        QP_INC(copy_calls);
        QP_BYTES(copy_bytes, bytes);
        QP_STAMP(release_start);
        lock.lock();
        QP_ADD(consumer_lock_ns, release_start);
        consumed_ += bytes;
        delivered_ += static_cast<off_t>(bytes);
        if (consumed_ == slot.size) {
            consumed_ = 0;
            if (++head_ == slots_.size()) head_ = 0;
            --ready_;
            lock.unlock();
            changed_.notify_one();
        }
        return bytes;
    }
};

// GNU grep processes descriptors serially. Only its main thread calls this ABI;
// the producer accesses its Reader's state and never this owning pointer.
std::unique_ptr<Reader> active;
}

extern "C" void read_ahead_begin(int fd) {
    const int saved_errno = errno;
    const char *enabled = std::getenv("GREP_READ_AHEAD");
    if (!active && enabled && std::strcmp(enabled, "1") == 0) {
        struct stat status {};
        const auto chunk = setting("GREP_READ_CHUNK", 262144, 4096, 8388608);
        const auto depth = setting("GREP_READ_SLOTS", 3, 1, 16);
        const auto offset = ::lseek(fd, 0, SEEK_CUR);
        if (chunk && depth && fd != STDIN_FILENO && offset >= 0
            && ::fstat(fd, &status) == 0 && S_ISREG(status.st_mode)) {
            // Allocation/thread creation failure keeps the original reader.
            // No C++ exception is allowed across the C ABI.
            try { active = std::make_unique<Reader>(fd, offset, chunk, depth); }
            catch (...) { }
        }
    }
    errno = saved_errno;
}

extern "C" int read_ahead_end(void) {
    if (!active) return 0;
    const int saved_errno = errno;
    active->stop();
#ifdef QUEUE_PROFILE
    active->emit_profile();
#endif
    const auto result = ::lseek(active->descriptor(), active->position(), SEEK_SET);
    const int seek_errno = errno;
    active.reset();
    errno = result < 0 ? seek_errno : saved_errno;
    return result < 0 ? -1 : 0;
}

extern "C" size_t read_ahead_read(int fd, void *buffer, size_t count) {
    return active && active->descriptor() == fd
        ? active->read(buffer, count) : safe_read(fd, buffer, count);
}

extern "C" off_t read_ahead_seek(int fd, off_t offset, int whence) {
    const bool restart = active && active->descriptor() == fd;
    if (restart && read_ahead_end() != 0) return -1;
    const auto result = ::lseek(fd, offset, whence);
    const int saved_errno = errno;
    if (restart && result >= 0) read_ahead_begin(fd);
    errno = saved_errno;
    return result;
}
