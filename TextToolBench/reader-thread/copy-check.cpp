// SPDX-License-Identifier: GPL-3.0-or-later
// Independent byte references, sentinels and protected boundaries for the
// nonoverlapping copy contract (READAHEAD-011). Never test undefined overlap.
#include "memory-copy.h"
#include <algorithm>
#include <array>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <sys/mman.h>
#include <unistd.h>
#include <vector>
namespace {
void check(bool condition) { if (!condition) std::abort(); }
void verify(const unsigned char *source, size_t n, size_t offset) {
    std::vector<unsigned char> output(n + 128, 0xa7);
    auto *destination = output.data() + 64 + offset;
    queue_memory_copy(destination, source, n);
    for (size_t i = 0; i < output.size(); ++i)
        check(output[i] == (i >= 64 + offset && i < 64 + offset + n ? source[i - 64 - offset] : 0xa7));
}
}
extern "C" int LLVMFuzzerTestOneInput(const unsigned char *data, size_t size) {
    // libFuzzer supplies independent content; low six bits select destination
    // alignment. Deterministic checks cover every source/destination pair.
    if (size > 8388608) return 0;
    verify(data, size, size ? data[0] & 63 : 0);
    return 0;
}
#ifndef LIBFUZZER
int main() {
    queue_memory_copy(nullptr, nullptr, 0);
    size_t references = 0, guards = 0;
    std::vector<unsigned char> bytes(8388608 + 128);
    for (size_t i = 0; i < bytes.size(); ++i) bytes[i] = static_cast<unsigned char>((i * 131 + i / 17) % 256);
    // Exhaust the entire tail range and every unaligned pair around both
    // vector boundaries; larger queue sizes have a bounded alignment sample.
    for (size_t n : {0U,1U,2U,3U,4U,7U,8U,15U,16U,17U,31U,32U,33U,63U,64U,65U,127U,128U,129U,255U,256U,257U,511U,512U,513U})
        for (size_t s = 0; s < 64; ++s)
            for (size_t d = 0; d < 64; ++d) { verify(bytes.data() + s, n, d); ++references; }
    for (size_t n = 0; n <= 513; ++n) { verify(bytes.data() + n % 64, n, (n * 17) % 64); ++references; }
    for (size_t n : {4095U,4096U,4097U,65535U,65536U,65537U,262143U,262144U,262145U,8388608U})
        for (size_t s : {0U,1U,31U,63U})
            for (size_t d : {0U,1U,31U,63U}) { verify(bytes.data() + s, n, d); ++references; }
    const auto page = static_cast<size_t>(::sysconf(_SC_PAGESIZE));
    auto allocate = [page] {
        auto *p = static_cast<unsigned char *>(::mmap(nullptr, page * 3, PROT_NONE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0));
        check(p != MAP_FAILED && ::mprotect(p + page, page, PROT_READ | PROT_WRITE) == 0);
        return p;
    };
    auto *source = allocate(), *destination = allocate();
    std::memcpy(source + page, bytes.data(), page);
    for (size_t n = 0; n <= 513; ++n)
        for (bool source_end : {false,true})
            for (bool destination_end : {false,true}) {
                auto *s = source + (source_end ? 2 * page - n : page);
                auto *d = destination + (destination_end ? 2 * page - n : page);
                std::memset(destination + page, 0xa7, page);
                queue_memory_copy(d, s, n);
                for (size_t i = 0; i < page; ++i) {
                    const auto *at = destination + page + i;
                    check(*at == (at >= d && at < d + n ? s[at - d] : 0xa7));
                }
                ++guards;
            }
    check(::munmap(source, page * 3) == 0 && ::munmap(destination, page * 3) == 0);
    std::printf("PASS %zu copy reference cases and %zu protected-boundary cases; null/zero case\n", references, guards);
}
#endif
