
/c/Users/jeffd/Documents/Codex/2026-10-26/benchmark-sed-and-grep-and-awk/publish/TheHorseshoePit/TextToolBench/reader-thread/build/copy-rep-memory.o:     file format pe-x86-64


Disassembly of section .text:

0000000000000000 <queue_memory_copy>:
   0:	57                   	push   %rdi
   1:	56                   	push   %rsi
   2:	48 89 c8             	mov    %rcx,%rax
   5:	4c 89 c1             	mov    %r8,%rcx
   8:	4d 85 c0             	test   %r8,%r8
   b:	74 08                	je     15 <queue_memory_copy+0x15>
   d:	48 89 c7             	mov    %rax,%rdi
  10:	48 89 d6             	mov    %rdx,%rsi
  13:	f3 a4                	rep movsb (%rsi),(%rdi)
  15:	5e                   	pop    %rsi
  16:	5f                   	pop    %rdi
  17:	c3                   	ret
  18:	90                   	nop
  19:	90                   	nop
  1a:	90                   	nop
  1b:	90                   	nop
  1c:	90                   	nop
  1d:	90                   	nop
  1e:	90                   	nop
  1f:	90                   	nop
