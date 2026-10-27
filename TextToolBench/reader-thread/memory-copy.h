// SPDX-License-Identifier: GPL-3.0-or-later
#pragma once
#include <cstddef>

// READAHEAD-011: experimental memcpy semantics, never memmove semantics.
// Regions must not overlap and must contain at least count accessible bytes.
// No alignment is required; zero length permits null pointers. Only the queue's
// consumer calls this function, while its source slot remains reserved.
extern "C" void queue_memory_copy(void *destination, const void *source, std::size_t count);
