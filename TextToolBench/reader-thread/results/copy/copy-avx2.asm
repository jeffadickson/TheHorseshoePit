
/c/Users/jeffd/Documents/Codex/2026-10-26/benchmark-sed-and-grep-and-awk/publish/TheHorseshoePit/TextToolBench/reader-thread/build/copy-avx2-memory.o:     file format pe-x86-64


Disassembly of section .text:

0000000000000000 <queue_memory_copy>:
   0:	48 83 ec 28          	sub    $0x28,%rsp
   4:	c5 f8 29 34 24       	vmovaps %xmm6,(%rsp)
   9:	c5 f8 29 7c 24 10    	vmovaps %xmm7,0x10(%rsp)
   f:	49 89 d1             	mov    %rdx,%r9
  12:	4d 85 c0             	test   %r8,%r8
  15:	0f 84 7d 01 00 00    	je     198 <queue_memory_copy+0x198>
  1b:	49 81 f8 ff 00 00 00 	cmp    $0xff,%r8
  22:	0f 86 00 02 00 00    	jbe    228 <queue_memory_copy+0x228>
  28:	4d 89 c2             	mov    %r8,%r10
  2b:	48 89 c8             	mov    %rcx,%rax
  2e:	49 c1 ea 08          	shr    $0x8,%r10
  32:	4d 8d 5a ff          	lea    -0x1(%r10),%r11
  36:	49 c1 e2 08          	shl    $0x8,%r10
  3a:	49 01 ca             	add    %rcx,%r10
  3d:	0f 1f 00             	nopl   (%rax)
  40:	c5 fe 6f 72 20       	vmovdqu 0x20(%rdx),%ymm6
  45:	c5 fe 6f 6a 40       	vmovdqu 0x40(%rdx),%ymm5
  4a:	48 05 00 01 00 00    	add    $0x100,%rax
  50:	48 81 c2 00 01 00 00 	add    $0x100,%rdx
  57:	c5 fe 6f a2 60 ff ff 	vmovdqu -0xa0(%rdx),%ymm4
  5e:	ff 
  5f:	c5 fe 6f 5a 80       	vmovdqu -0x80(%rdx),%ymm3
  64:	c5 fe 6f 52 a0       	vmovdqu -0x60(%rdx),%ymm2
  69:	c5 fe 6f 4a c0       	vmovdqu -0x40(%rdx),%ymm1
  6e:	c5 fe 6f 42 e0       	vmovdqu -0x20(%rdx),%ymm0
  73:	c5 fe 6f ba 00 ff ff 	vmovdqu -0x100(%rdx),%ymm7
  7a:	ff 
  7b:	c5 fe 7f b0 20 ff ff 	vmovdqu %ymm6,-0xe0(%rax)
  82:	ff 
  83:	c5 fe 7f a8 40 ff ff 	vmovdqu %ymm5,-0xc0(%rax)
  8a:	ff 
  8b:	c5 fe 7f b8 00 ff ff 	vmovdqu %ymm7,-0x100(%rax)
  92:	ff 
  93:	c5 fe 7f a0 60 ff ff 	vmovdqu %ymm4,-0xa0(%rax)
  9a:	ff 
  9b:	c5 fe 7f 58 80       	vmovdqu %ymm3,-0x80(%rax)
  a0:	c5 fe 7f 50 a0       	vmovdqu %ymm2,-0x60(%rax)
  a5:	c5 fe 7f 48 c0       	vmovdqu %ymm1,-0x40(%rax)
  aa:	c5 fe 7f 40 e0       	vmovdqu %ymm0,-0x20(%rax)
  af:	4c 39 d0             	cmp    %r10,%rax
  b2:	75 8c                	jne    40 <queue_memory_copy+0x40>
  b4:	4c 89 da             	mov    %r11,%rdx
  b7:	41 0f b6 c0          	movzbl %r8b,%eax
  bb:	48 c1 e2 08          	shl    $0x8,%rdx
  bf:	41 81 e0 e0 00 00 00 	and    $0xe0,%r8d
  c6:	48 8d 8c 11 00 01 00 	lea    0x100(%rcx,%rdx,1),%rcx
  cd:	00 
  ce:	4d 8d 8c 11 00 01 00 	lea    0x100(%r9,%rdx,1),%r9
  d5:	00 
  d6:	0f 84 b4 00 00 00    	je     190 <queue_memory_copy+0x190>
  dc:	c4 c1 7e 6f 01       	vmovdqu (%r9),%ymm0
  e1:	48 8d 50 e0          	lea    -0x20(%rax),%rdx
  e5:	c5 fe 7f 01          	vmovdqu %ymm0,(%rcx)
  e9:	48 83 fa 1f          	cmp    $0x1f,%rdx
  ed:	0f 86 8c 00 00 00    	jbe    17f <queue_memory_copy+0x17f>
  f3:	c4 c1 7e 6f 41 20    	vmovdqu 0x20(%r9),%ymm0
  f9:	4c 8d 40 c0          	lea    -0x40(%rax),%r8
  fd:	c5 fe 7f 41 20       	vmovdqu %ymm0,0x20(%rcx)
 102:	49 83 f8 1f          	cmp    $0x1f,%r8
 106:	76 77                	jbe    17f <queue_memory_copy+0x17f>
 108:	c4 c1 7e 6f 41 40    	vmovdqu 0x40(%r9),%ymm0
 10e:	4c 8d 40 a0          	lea    -0x60(%rax),%r8
 112:	c5 fe 7f 41 40       	vmovdqu %ymm0,0x40(%rcx)
 117:	49 83 f8 1f          	cmp    $0x1f,%r8
 11b:	76 62                	jbe    17f <queue_memory_copy+0x17f>
 11d:	c4 c1 7e 6f 41 60    	vmovdqu 0x60(%r9),%ymm0
 123:	4c 8d 40 80          	lea    -0x80(%rax),%r8
 127:	c5 fe 7f 41 60       	vmovdqu %ymm0,0x60(%rcx)
 12c:	49 83 f8 1f          	cmp    $0x1f,%r8
 130:	76 4d                	jbe    17f <queue_memory_copy+0x17f>
 132:	c4 c1 7e 6f 81 80 00 	vmovdqu 0x80(%r9),%ymm0
 139:	00 00 
 13b:	4c 8d 80 60 ff ff ff 	lea    -0xa0(%rax),%r8
 142:	c5 fe 7f 81 80 00 00 	vmovdqu %ymm0,0x80(%rcx)
 149:	00 
 14a:	49 83 f8 1f          	cmp    $0x1f,%r8
 14e:	76 2f                	jbe    17f <queue_memory_copy+0x17f>
 150:	c4 c1 7e 6f 81 a0 00 	vmovdqu 0xa0(%r9),%ymm0
 157:	00 00 
 159:	4c 8d 80 40 ff ff ff 	lea    -0xc0(%rax),%r8
 160:	c5 fe 7f 81 a0 00 00 	vmovdqu %ymm0,0xa0(%rcx)
 167:	00 
 168:	49 83 f8 1f          	cmp    $0x1f,%r8
 16c:	76 11                	jbe    17f <queue_memory_copy+0x17f>
 16e:	c4 c1 7e 6f 81 c0 00 	vmovdqu 0xc0(%r9),%ymm0
 175:	00 00 
 177:	c5 fe 7f 81 c0 00 00 	vmovdqu %ymm0,0xc0(%rcx)
 17e:	00 
 17f:	48 83 e2 e0          	and    $0xffffffffffffffe0,%rdx
 183:	83 e0 1f             	and    $0x1f,%eax
 186:	4d 8d 4c 11 20       	lea    0x20(%r9,%rdx,1),%r9
 18b:	48 8d 4c 11 20       	lea    0x20(%rcx,%rdx,1),%rcx
 190:	48 85 c0             	test   %rax,%rax
 193:	75 1b                	jne    1b0 <queue_memory_copy+0x1b0>
 195:	c5 f8 77             	vzeroupper
 198:	c5 f8 28 34 24       	vmovaps (%rsp),%xmm6
 19d:	c5 f8 28 7c 24 10    	vmovaps 0x10(%rsp),%xmm7
 1a3:	48 83 c4 28          	add    $0x28,%rsp
 1a7:	c3                   	ret
 1a8:	0f 1f 84 00 00 00 00 	nopl   0x0(%rax,%rax,1)
 1af:	00 
 1b0:	c5 f8 77             	vzeroupper
 1b3:	89 c2                	mov    %eax,%edx
 1b5:	83 f8 08             	cmp    $0x8,%eax
 1b8:	72 46                	jb     200 <queue_memory_copy+0x200>
 1ba:	49 8b 11             	mov    (%r9),%rdx
 1bd:	48 89 11             	mov    %rdx,(%rcx)
 1c0:	89 c2                	mov    %eax,%edx
 1c2:	4d 8b 44 11 f8       	mov    -0x8(%r9,%rdx,1),%r8
 1c7:	4c 89 44 11 f8       	mov    %r8,-0x8(%rcx,%rdx,1)
 1cc:	4c 8d 41 08          	lea    0x8(%rcx),%r8
 1d0:	49 83 e0 f8          	and    $0xfffffffffffffff8,%r8
 1d4:	4c 29 c1             	sub    %r8,%rcx
 1d7:	8d 14 08             	lea    (%rax,%rcx,1),%edx
 1da:	49 29 c9             	sub    %rcx,%r9
 1dd:	83 e2 f8             	and    $0xfffffff8,%edx
 1e0:	83 fa 08             	cmp    $0x8,%edx
 1e3:	72 b3                	jb     198 <queue_memory_copy+0x198>
 1e5:	83 e2 f8             	and    $0xfffffff8,%edx
 1e8:	31 c0                	xor    %eax,%eax
 1ea:	89 c1                	mov    %eax,%ecx
 1ec:	83 c0 08             	add    $0x8,%eax
 1ef:	4d 8b 14 09          	mov    (%r9,%rcx,1),%r10
 1f3:	4d 89 14 08          	mov    %r10,(%r8,%rcx,1)
 1f7:	39 d0                	cmp    %edx,%eax
 1f9:	72 ef                	jb     1ea <queue_memory_copy+0x1ea>
 1fb:	eb 9b                	jmp    198 <queue_memory_copy+0x198>
 1fd:	0f 1f 00             	nopl   (%rax)
 200:	a8 04                	test   $0x4,%al
 202:	75 36                	jne    23a <queue_memory_copy+0x23a>
 204:	85 c0                	test   %eax,%eax
 206:	74 90                	je     198 <queue_memory_copy+0x198>
 208:	41 0f b6 11          	movzbl (%r9),%edx
 20c:	88 11                	mov    %dl,(%rcx)
 20e:	a8 02                	test   $0x2,%al
 210:	74 86                	je     198 <queue_memory_copy+0x198>
 212:	89 c0                	mov    %eax,%eax
 214:	41 0f b7 54 01 fe    	movzwl -0x2(%r9,%rax,1),%edx
 21a:	66 89 54 01 fe       	mov    %dx,-0x2(%rcx,%rax,1)
 21f:	e9 74 ff ff ff       	jmp    198 <queue_memory_copy+0x198>
 224:	0f 1f 40 00          	nopl   0x0(%rax)
 228:	4c 89 c0             	mov    %r8,%rax
 22b:	49 83 f8 1f          	cmp    $0x1f,%r8
 22f:	0f 87 a7 fe ff ff    	ja     dc <queue_memory_copy+0xdc>
 235:	e9 79 ff ff ff       	jmp    1b3 <queue_memory_copy+0x1b3>
 23a:	41 8b 01             	mov    (%r9),%eax
 23d:	89 01                	mov    %eax,(%rcx)
 23f:	41 8b 44 11 fc       	mov    -0x4(%r9,%rdx,1),%eax
 244:	89 44 11 fc          	mov    %eax,-0x4(%rcx,%rdx,1)
 248:	e9 4b ff ff ff       	jmp    198 <queue_memory_copy+0x198>
 24d:	90                   	nop
 24e:	90                   	nop
 24f:	90                   	nop
