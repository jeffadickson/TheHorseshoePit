// SPDX-License-Identifier: GPL-3.0-or-later
// Diagnostic stage wall clocks and per-thread CPU clocks. Completely absent
// from timing builds. Inter-thread wall intervals overlap and are not additive.
#ifndef GREP_QUEUE_PROFILE_H
#define GREP_QUEUE_PROFILE_H
#ifdef QUEUE_PROFILE
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <time.h>
namespace queue_profile {
inline std::uint64_t now() {
    return static_cast<std::uint64_t>(std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count());
}
inline std::int64_t cpu() {
    timespec t {};
    if (::clock_gettime(CLOCK_THREAD_CPUTIME_ID, &t) != 0) return -1;
    return static_cast<std::int64_t>(t.tv_sec) * 1000000000LL + t.tv_nsec;
}
struct Data {
    std::uint64_t started = now(), producer_read_ns = 0, producer_wait_ns = 0,
        producer_lock_ns = 0, consumer_wait_ns = 0, consumer_lock_ns = 0,
        copy_ns = 0, stop_ns = 0, read_calls = 0, copy_calls = 0,
        read_bytes = 0, copy_bytes = 0, full_waits = 0, empty_waits = 0, max_ready = 0;
    std::int64_t main_started = cpu(), producer_cpu_ns = -1;
    // Called only after join; producer writes are synchronized by that join.
    void emit() const {
        const auto end_cpu = cpu();
        const auto main_cpu = main_started >= 0 && end_cpu >= 0 ? end_cpu - main_started : -1;
        std::fprintf(stderr,
            "QUEUE_PROFILE {\"wall_ns\":%llu,\"producer_read_ns\":%llu,\"producer_wait_ns\":%llu,"
            "\"producer_lock_ns\":%llu,\"consumer_wait_ns\":%llu,\"consumer_lock_ns\":%llu,"
            "\"copy_ns\":%llu,\"stop_ns\":%llu,\"read_calls\":%llu,\"copy_calls\":%llu,"
            "\"read_bytes\":%llu,\"copy_bytes\":%llu,\"full_waits\":%llu,\"empty_waits\":%llu,"
            "\"max_ready\":%llu,\"producer_cpu_ns\":",
            static_cast<unsigned long long>(now() - started),
            static_cast<unsigned long long>(producer_read_ns),
            static_cast<unsigned long long>(producer_wait_ns),
            static_cast<unsigned long long>(producer_lock_ns),
            static_cast<unsigned long long>(consumer_wait_ns),
            static_cast<unsigned long long>(consumer_lock_ns),
            static_cast<unsigned long long>(copy_ns),
            static_cast<unsigned long long>(stop_ns),
            static_cast<unsigned long long>(read_calls),
            static_cast<unsigned long long>(copy_calls),
            static_cast<unsigned long long>(read_bytes),
            static_cast<unsigned long long>(copy_bytes),
            static_cast<unsigned long long>(full_waits),
            static_cast<unsigned long long>(empty_waits),
            static_cast<unsigned long long>(max_ready));
        if (producer_cpu_ns < 0) std::fputs("null", stderr);
        else std::fprintf(stderr, "%lld", static_cast<long long>(producer_cpu_ns));
        std::fputs(",\"main_cpu_ns\":", stderr);
        if (main_cpu < 0) std::fputs("null", stderr);
        else std::fprintf(stderr, "%lld", static_cast<long long>(main_cpu));
        std::fputs("}\n", stderr);
    }
};
struct ProducerCpu {
    Data &data;
    const std::int64_t start = cpu();
    ~ProducerCpu() {
        const auto end = cpu();
        data.producer_cpu_ns = start >= 0 && end >= 0 ? end - start : -1;
    }
};
}
#define QP_STAMP(name) const auto name = queue_profile::now()
#define QP_ADD(field, start) profile_.field += queue_profile::now() - start
#define QP_INC(field) ++profile_.field
#define QP_BYTES(field, count) profile_.field += count
#else
#define QP_STAMP(name)
#define QP_ADD(field, start)
#define QP_INC(field)
#define QP_BYTES(field, count)
#endif
#endif
