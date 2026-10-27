/* SPDX-License-Identifier: GPL-3.0-or-later
   Byte searches preserve memchr/memchr2 first-match and unsigned-byte semantics. */
#ifndef GREP_VECTOR_SEARCH_H
#define GREP_VECTOR_SEARCH_H
#include <stddef.h>
#ifdef __cplusplus
extern "C" {
#endif
/* Search exactly count accessible bytes; neither helper reads a full vector
   past the end. GREP_VECTOR_SEARCH=1 enables AVX2 in native AVX2 builds. */
void *grep_vector_memchr(const void *buffer, int byte, size_t count);
void *grep_vector_memchr2(const void *buffer, int first, int second, size_t count);
#ifdef __cplusplus
}
#endif
#endif
