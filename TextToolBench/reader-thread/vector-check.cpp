// SPDX-License-Identifier: GPL-3.0-or-later
// Independent reference and guard-page checks for READAHEAD-006.
#include "vector-search.h"
#include <algorithm>
#include <array>
#include <cstdlib>
#include <cstring>
#include <sys/mman.h>
#include <unistd.h>
#include <utility>
#include <vector>

// Test-only stand-in for the unchanged gnulib fallback linked by real grep.
extern "C" void *memchr2(const void *buffer, int a, int b, size_t n) {
    const auto *p = static_cast<const unsigned char *>(buffer);
    for (size_t i = 0; i < n; ++i)
        if (p[i] == static_cast<unsigned char>(a) || p[i] == static_cast<unsigned char>(b))
            return const_cast<unsigned char *>(p + i);
    return nullptr;
}
namespace {
void verify(const unsigned char *p, size_t n, int a, int b) {
    const auto *end = p + n;
    const auto *one = std::find(p, end, static_cast<unsigned char>(a));
    const auto *two = std::find_if(p, end, [a,b](unsigned char v) {
        return v == static_cast<unsigned char>(a) || v == static_cast<unsigned char>(b);
    });
    if (grep_vector_memchr(p, a, n) != (one == end ? nullptr : one)
        || grep_vector_memchr2(p, a, b, n) != (two == end ? nullptr : two)) std::abort();
}
}
void vector_properties(const unsigned char *data, size_t size) {
    // Bound test-harness allocation arithmetic. Fuzz inputs are at most
    // 131073 bytes; deterministic checks additionally exercise 1 MiB buffers.
    if (size > 8388608) return;
    std::vector<unsigned char> bytes(size + 64, 0x91);
    if (size) std::memcpy(bytes.data() + 32, data, size);
    const int a = size ? data[0] : 0;
    const int b = size > 1 ? data[1] : 255;
    for (size_t offset : {0U, 1U, 15U, 31U}) {
        const auto n = size > offset ? size - offset : 0;
        verify(bytes.data() + 32 + offset, n, a + 256, b - 256);
    }
}
std::pair<size_t, size_t> vector_regressions() {
    size_t references = 0, guards = 0;
    std::array<unsigned char, 1024> bytes {};
    for (size_t i = 0; i < bytes.size(); ++i) bytes[i] = static_cast<unsigned char>(i * 37);
    for (size_t length : {0U,1U,7U,15U,16U,17U,31U,32U,33U,63U,64U,65U,127U,128U,129U,255U,256U,257U,511U,512U,513U})
        for (size_t offset = 0; offset < 32; ++offset)
            for (int a = 0; a < 256; ++a) {
                verify(bytes.data() + offset, length, a, (a * 73 + 17) % 256);
                verify(bytes.data() + offset, length, a, a);
                references += 2;
            }
    // Exercise matches in every lane, including a skipped first 256-byte block,
    // and competing matches whose lane order must be preserved.
    for (size_t position = 0; position < 512; ++position) {
        bytes.fill(0x11);
        bytes[position] = 0x7f;
        verify(bytes.data(), 512, 0x7f, 0xe3);
        bytes[511 - position] = 0xe3;
        verify(bytes.data(), 512, 0x7f, 0xe3);
        references += 2;
    }
    const auto page = static_cast<size_t>(::sysconf(_SC_PAGESIZE));
    auto *memory = static_cast<unsigned char *>(::mmap(nullptr, page * 2,
        PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0));
    if (memory == MAP_FAILED || ::mprotect(memory + page, page, PROT_NONE) != 0) std::abort();
    std::memset(memory, 'x', page);
    for (size_t length = 0; length <= 513; ++length) {
        verify(memory + page - length, length, 'q', 'z');
        ++guards;
    }
    memory[page - 1] = 'z';
    for (size_t length = 0; length <= 513; ++length) {
        verify(memory + page - length, length, 'q', 'z');
        ++guards;
    }
    if (::munmap(memory, page * 2) != 0) std::abort();
    return {references, guards};
}
