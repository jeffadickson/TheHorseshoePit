// SPDX-License-Identifier: GPL-3.0-or-later
// Independent byte/offset properties for READAHEAD-002..004. The same entry
// point is driven by libFuzzer and by deterministic boundary regression cases.
#include "read-ahead.h"
#include <algorithm>
#include <array>
#include <cerrno>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fcntl.h>
#include <stdexcept>
#include <string>
#include <unistd.h>
#include <vector>
void vector_properties(const unsigned char *, size_t);
void vector_regressions();

extern "C" size_t safe_read(int fd, void *buffer, size_t count) {
    ssize_t result;
    do { result = ::read(fd, buffer, count); } while (result < 0 && errno == EINTR);
    return static_cast<size_t>(result);
}

namespace {
void check(bool condition) { if (!condition) std::abort(); }
struct Input {
    char path[64] = "/tmp/texttool-reader-XXXXXX";
    int fd = ::mkstemp(path);
    Input() { check(fd >= 0); }
    ~Input() { read_ahead_end(); ::close(fd); ::unlink(path); }
};

void property(const unsigned char *data, size_t size, unsigned control) {
    Input input;
    size_t written = 0;
    while (written < size) {
        const auto n = ::write(input.fd, data + written, size - written);
        check(n > 0); written += static_cast<size_t>(n);
    }
    check(::lseek(input.fd, 0, SEEK_SET) == 0);
    ::setenv("GREP_READ_AHEAD", "1", 1);
    ::setenv("GREP_READ_CHUNK", control & 1 ? "4096" : "65536", 1);
    ::setenv("GREP_READ_SLOTS", control & 2 ? "1" : "3", 1);
    read_ahead_begin(input.fd);
    // Zero-byte reads must neither block nor consume input.
    check(read_ahead_read(input.fd, nullptr, 0) == 0);
    std::array<unsigned char, 8193> buffer {};
    size_t offset = 0;
    while (offset < size) {
        const size_t request = std::min(buffer.size(), static_cast<size_t>(1 + (control * 31 + offset) % 8193));
        const auto n = read_ahead_read(input.fd, buffer.data(), request);
        check(n > 0 && n <= request && n <= size - offset);
        check(std::memcmp(buffer.data(), data + offset, n) == 0);
        offset += n;
        if (offset < size && offset > size / 2 && (control & 4)) break;
    }
    if (offset == size) {
        check(read_ahead_read(input.fd, buffer.data(), buffer.size()) == 0);
        check(read_ahead_read(input.fd, buffer.data(), 1) == 0); // sticky EOF
    }
    // Seek must discard stale queued bytes and use the logical consumed offset.
    const auto position = read_ahead_seek(input.fd, 0, SEEK_CUR);
    check(position == static_cast<off_t>(offset));
    if (size) {
        const auto target = static_cast<off_t>(control % size);
        check(read_ahead_seek(input.fd, target, SEEK_SET) == target);
        const auto n = read_ahead_read(input.fd, buffer.data(), buffer.size());
        check(n > 0 && n <= size - static_cast<size_t>(target));
        check(std::memcmp(buffer.data(), data + target, n) == 0);
        offset = static_cast<size_t>(target) + n;
        if (control & 8) {
            check(read_ahead_seek(input.fd, -1, SEEK_SET) == -1);
            // Failed seek leaves the committed descriptor available to safe_read.
        }
    }
    check(read_ahead_end() == 0);
    check(::lseek(input.fd, 0, SEEK_CUR) == static_cast<off_t>(offset));
    check(read_ahead_end() == 0); // idempotent cancellation
}

[[maybe_unused]] void negative_cases() {
    Input input;
    check(::write(input.fd, "abc", 3) == 3);
    char buffer[4] {};
    for (const auto *bad : {"", "0", "-1", "+1", "1x", "999999999999999999999999", "4095", "8388609"}) {
        check(::lseek(input.fd, 0, SEEK_SET) == 0);
        ::setenv("GREP_READ_CHUNK", bad, 1);
        read_ahead_begin(input.fd);
        check(read_ahead_read(input.fd, buffer, 3) == 3);
        check(std::memcmp(buffer, "abc", 3) == 0);
        check(read_ahead_end() == 0);
    }
    ::setenv("GREP_READ_CHUNK", "4096", 1);
    for (const auto *bad : {"0", "17", "bad"}) {
        ::setenv("GREP_READ_SLOTS", bad, 1);
        read_ahead_begin(input.fd); check(read_ahead_end() == 0);
    }
    ::unsetenv("GREP_READ_SLOTS"); ::unsetenv("GREP_READ_CHUNK");
    ::unsetenv("GREP_READ_AHEAD");
    check(::lseek(input.fd, 0, SEEK_SET) == 0);
    read_ahead_begin(input.fd);
    check(read_ahead_read(input.fd, buffer, 3) == 3);
    check(read_ahead_end() == 0);
    ::setenv("GREP_READ_AHEAD", "1", 1);
    // A regular descriptor opened write-only reaches the producer's error path.
    int write_only = ::open(input.path, O_WRONLY);
    check(write_only >= 0); read_ahead_begin(write_only);
    errno = 0;
    check(read_ahead_read(write_only, buffer, 1) == static_cast<size_t>(-1) && errno == EBADF);
    check(read_ahead_read(write_only, buffer, 1) == static_cast<size_t>(-1));
    check(read_ahead_end() == 0); ::close(write_only);
    read_ahead_begin(-1); errno = 0;
    check(read_ahead_read(-1, buffer, 1) == static_cast<size_t>(-1) && errno == EBADF);
    int pipes[2]; check(::pipe(pipes) == 0); read_ahead_begin(pipes[0]);
    check(::write(pipes[1], "z", 1) == 1); ::close(pipes[1]);
    check(read_ahead_read(pipes[0], buffer, 1) == 1 && buffer[0] == 'z');
    check(read_ahead_end() == 0); ::close(pipes[0]);
}
}

extern "C" int LLVMFuzzerTestOneInput(const unsigned char *data, size_t size) {
    const unsigned control = size ? data[0] : 0;
    property(data, size, control);
    vector_properties(data, size);
    return 0;
}

#ifndef LIBFUZZER
int main() {
    vector_regressions();
    negative_cases();
    int cases = 0;
    for (const size_t size : {0U, 1U, 4095U, 4096U, 4097U, 65535U, 65536U, 65537U, 1048577U}) {
        std::vector<unsigned char> bytes(size);
        for (size_t i = 0; i < size; ++i) bytes[i] = static_cast<unsigned char>((i * 131 + i / 17) % 256);
        for (unsigned control = 0; control < 16; ++control) {
            property(bytes.data(), bytes.size(), control);
            vector_properties(bytes.data(), bytes.size());
            ++cases;
        }
    }
    std::printf("PASS %d queue properties, negative API cases and 245760 vector reference cases plus 260 guard-page cases\n", cases);
}
#endif
