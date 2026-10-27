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
