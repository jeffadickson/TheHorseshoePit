// SPDX-License-Identifier: GPL-3.0-or-later
// READAHEAD-006: explicit 32-byte comparisons, with the original library paths
// retained as an opt-out control. Whole binaries remain processor-specific.
#include "vector-search.h"
#include <bit>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#if defined(__AVX2__)
#include <immintrin.h>
#endif
extern "C" void *memchr2(const void *, int, int, size_t);

// Compile two isolated variants: the existing 32-byte loop and an eight-vector
// 256-byte rejection loop. There is no run-time block-size branch in either.
#ifndef GREP_VECTOR_BLOCK
#define GREP_VECTOR_BLOCK 32
#endif
#if GREP_VECTOR_BLOCK != 32 && GREP_VECTOR_BLOCK != 256
#error "GREP_VECTOR_BLOCK must be 32 or 256"
#endif

namespace {
bool enabled() {
    static const bool use_vectors = [] {
        const char *value = std::getenv("GREP_VECTOR_SEARCH");
        return value && std::strcmp(value, "1") == 0;
    }();
    return use_vectors;
}

template<bool Two>
void *find(const void *buffer, int first, int second, size_t count) {
    if (!enabled()) {
        if constexpr (Two) return memchr2(buffer, first, second, count);
        else return const_cast<void *>(std::memchr(buffer, first, count));
    }
    auto *p = static_cast<const unsigned char *>(buffer);
    const auto a = static_cast<unsigned char>(first);
    const auto b = static_cast<unsigned char>(second);
#if defined(__AVX2__)
    const auto va = _mm256_set1_epi8(static_cast<char>(a));
    const auto vb = _mm256_set1_epi8(static_cast<char>(b));
#if GREP_VECTOR_BLOCK == 256
    while (count >= 256) {
        const auto compare = [&](const unsigned char *q) {
            const auto v = _mm256_loadu_si256(reinterpret_cast<const __m256i_u *>(q));
            auto equal = _mm256_cmpeq_epi8(v, va);
            if constexpr (Two) equal = _mm256_or_si256(equal, _mm256_cmpeq_epi8(v, vb));
            return equal;
        };
        const auto e0 = compare(p);
        const auto e1 = compare(p + 32);
        const auto e2 = compare(p + 64);
        const auto e3 = compare(p + 96);
        const auto e4 = compare(p + 128);
        const auto e5 = compare(p + 160);
        const auto e6 = compare(p + 192);
        const auto e7 = compare(p + 224);
        const auto any = _mm256_or_si256(
            _mm256_or_si256(_mm256_or_si256(e0, e1), _mm256_or_si256(e2, e3)),
            _mm256_or_si256(_mm256_or_si256(e4, e5), _mm256_or_si256(e6, e7)));
        // One mask rejects an entire block. On a hit, examine lanes in address
        // order: combining lane masks alone would lose the first-match position.
        if (_mm256_movemask_epi8(any)) {
            if (const auto m = static_cast<std::uint32_t>(_mm256_movemask_epi8(e0))) return const_cast<unsigned char *>(p + std::countr_zero(m));
            if (const auto m = static_cast<std::uint32_t>(_mm256_movemask_epi8(e1))) return const_cast<unsigned char *>(p + 32 + std::countr_zero(m));
            if (const auto m = static_cast<std::uint32_t>(_mm256_movemask_epi8(e2))) return const_cast<unsigned char *>(p + 64 + std::countr_zero(m));
            if (const auto m = static_cast<std::uint32_t>(_mm256_movemask_epi8(e3))) return const_cast<unsigned char *>(p + 96 + std::countr_zero(m));
            if (const auto m = static_cast<std::uint32_t>(_mm256_movemask_epi8(e4))) return const_cast<unsigned char *>(p + 128 + std::countr_zero(m));
            if (const auto m = static_cast<std::uint32_t>(_mm256_movemask_epi8(e5))) return const_cast<unsigned char *>(p + 160 + std::countr_zero(m));
            if (const auto m = static_cast<std::uint32_t>(_mm256_movemask_epi8(e6))) return const_cast<unsigned char *>(p + 192 + std::countr_zero(m));
            const auto m = static_cast<std::uint32_t>(_mm256_movemask_epi8(e7));
            return const_cast<unsigned char *>(p + 224 + std::countr_zero(m));
        }
        p += 256;
        count -= 256;
    }
#endif
    while (count >= 32) {
        // Unaligned loads are intentional. The count guard, not padding or
        // page layout, guarantees that every loaded byte belongs to the input.
        const auto bytes = _mm256_loadu_si256(reinterpret_cast<const __m256i_u *>(p));
        auto equal = _mm256_cmpeq_epi8(bytes, va);
        if constexpr (Two) equal = _mm256_or_si256(equal, _mm256_cmpeq_epi8(bytes, vb));
        const auto mask = static_cast<std::uint32_t>(_mm256_movemask_epi8(equal));
        if (mask) return const_cast<unsigned char *>(p + std::countr_zero(mask));
        p += 32;
        count -= 32;
    }
#endif
    // This also provides a build-time fallback for non-AVX2 verification builds.
    while (count) {
        if (*p == a || (Two && *p == b)) return const_cast<unsigned char *>(p);
        ++p;
        --count;
    }
    return nullptr;
}
}
extern "C" void *grep_vector_memchr(const void *buffer, int byte, size_t count) {
    return find<false>(buffer, byte, byte, count);
}
extern "C" void *grep_vector_memchr2(const void *buffer, int first, int second, size_t count) {
    return find<true>(buffer, first, second, count);
}
