// SPDX-License-Identifier: GPL-3.0-or-later
// Cached stores suit grep's immediate consumption of the destination. These
// native-CPU experiments deliberately do not add portable runtime dispatch.
#include "memory-copy.h"
#include <cstring>
#ifndef QUEUE_COPY_KIND
#define QUEUE_COPY_KIND 0
#endif
#if QUEUE_COPY_KIND < 0 || QUEUE_COPY_KIND > 2
#error QUEUE_COPY_KIND must be 0 (memcpy), 1 (AVX2), or 2 (rep movsb)
#endif
#if QUEUE_COPY_KIND == 1
#include <immintrin.h>
#endif

extern "C" void queue_memory_copy(void *destination, const void *source, std::size_t count) {
    if (!count) return;
#if QUEUE_COPY_KIND == 0
    std::memcpy(destination, source, count);
#elif QUEUE_COPY_KIND == 2
    // The x86-64 ABI requires DF clear on entry. The compiler must see all
    // pointer/count updates and arbitrary memory effects of the string copy.
    asm volatile ("rep movsb" : "+D"(destination), "+S"(source), "+c"(count) : : "memory");
#else
    auto *d = static_cast<unsigned char *>(destination);
    const auto *s = static_cast<const unsigned char *>(source);
    // Each hardware operation moves 32 bytes; the loop handles 256 bytes.
    // No load/store touches an unrequested byte, even on the final page.
    while (count >= 256) {
        const auto a = _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s));
        const auto b = _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s + 32));
        const auto c = _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s + 64));
        const auto e = _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s + 96));
        const auto f = _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s + 128));
        const auto g = _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s + 160));
        const auto h = _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s + 192));
        const auto i = _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s + 224));
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d), a);
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d + 32), b);
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d + 64), c);
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d + 96), e);
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d + 128), f);
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d + 160), g);
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d + 192), h);
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d + 224), i);
        s += 256; d += 256; count -= 256;
    }
    while (count >= 32) {
        _mm256_storeu_si256(reinterpret_cast<__m256i *>(d),
            _mm256_loadu_si256(reinterpret_cast<const __m256i *>(s)));
        s += 32; d += 32; count -= 32;
    }
    // The standard library handles only the remaining 0..31 bytes. Keeping
    // this bounded tail avoids duplicated unaligned scalar type-punning code.
    if (count) std::memcpy(d, s, count);
#endif
}
