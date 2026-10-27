/* SPDX-License-Identifier: GPL-3.0-or-later
   C ABI for the experimental, single-consumer GNU grep input adapter. */
#ifndef TEXTTOOL_READ_AHEAD_H
#define TEXTTOOL_READ_AHEAD_H
#include <stddef.h>
#include <sys/types.h>
#ifdef __cplusplus
extern "C" {
#endif
/* Begin only for a named regular file. Disabled or unavailable resources retain
   the original safe_read path. Environment settings are validated and bounded. */
void read_ahead_begin(int fd);
/* Stop and join before grep finalizes/seeks/closes the input. Restore the file
   offset to bytes delivered to grep, excluding queued but unconsumed bytes.
   Return -1 with errno on offset restoration failure, otherwise zero. */
int read_ahead_end(void);
/* Match safe_read's byte count, EOF and (size_t)-1/errno error convention. */
size_t read_ahead_read(int fd, void *buffer, size_t count);
/* Coordinate grep's sparse-file seeks with any active producer. */
off_t read_ahead_seek(int fd, off_t offset, int whence);
#ifdef __cplusplus
}
#endif
#endif
