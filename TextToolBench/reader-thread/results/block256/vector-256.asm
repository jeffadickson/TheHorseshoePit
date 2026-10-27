
/c/Users/jeffd/Documents/Codex/2026-10-26/benchmark-sed-and-grep-and-awk/publish/TheHorseshoePit/TextToolBench/reader-thread/vector-256.o:     file format pe-x86-64


Disassembly of section .text:

0000000000000000 <grep_vector_memchr>:
   0:	53                   	push   %rbx
   1:	48 81 ec a0 00 00 00 	sub    $0xa0,%rsp
   8:	c5 f8 29 74 24 50    	vmovaps %xmm6,0x50(%rsp)
   e:	c5 f8 29 7c 24 60    	vmovaps %xmm7,0x60(%rsp)
  14:	c5 78 29 44 24 70    	vmovaps %xmm8,0x70(%rsp)
  1a:	c5 78 29 8c 24 80 00 	vmovaps %xmm9,0x80(%rsp)
  21:	00 00 
  23:	c5 78 29 94 24 90 00 	vmovaps %xmm10,0x90(%rsp)
  2a:	00 00 
  2c:	c5 f9 6e fa          	vmovd  %edx,%xmm7
  30:	48 89 cb             	mov    %rcx,%rbx
  33:	c4 e2 7d 78 ff       	vpbroadcastb %xmm7,%ymm7
  38:	c5 fe 7f 7c 24 20    	vmovdqu %ymm7,0x20(%rsp)
  3e:	0f b6 05 00 00 00 00 	movzbl 0x0(%rip),%eax        # 45 <grep_vector_memchr+0x45>
  45:	84 c0                	test   %al,%al
  47:	0f 84 33 04 00 00    	je     480 <grep_vector_memchr+0x480>
  4d:	80 3d 07 00 00 00 00 	cmpb   $0x0,0x7(%rip)        # 5b <grep_vector_memchr+0x5b>
  54:	0f 84 d6 00 00 00    	je     130 <grep_vector_memchr+0x130>
  5a:	c5 fe 6f 44 24 20    	vmovdqu 0x20(%rsp),%ymm0
  60:	49 81 f8 ff 00 00 00 	cmp    $0xff,%r8
  67:	77 22                	ja     8b <grep_vector_memchr+0x8b>
  69:	e9 02 01 00 00       	jmp    170 <grep_vector_memchr+0x170>
  6e:	66 90                	xchg   %ax,%ax
  70:	49 81 e8 00 01 00 00 	sub    $0x100,%r8
  77:	48 81 c3 00 01 00 00 	add    $0x100,%rbx
  7e:	49 81 f8 ff 00 00 00 	cmp    $0xff,%r8
  85:	0f 86 e5 00 00 00    	jbe    170 <grep_vector_memchr+0x170>
  8b:	c5 fd 74 bb 80 00 00 	vpcmpeqb 0x80(%rbx),%ymm0,%ymm7
  92:	00 
  93:	c5 7d 74 93 a0 00 00 	vpcmpeqb 0xa0(%rbx),%ymm0,%ymm10
  9a:	00 
  9b:	c5 7d 74 8b c0 00 00 	vpcmpeqb 0xc0(%rbx),%ymm0,%ymm9
  a2:	00 
  a3:	c5 7d 74 83 e0 00 00 	vpcmpeqb 0xe0(%rbx),%ymm0,%ymm8
  aa:	00 
  ab:	c5 fd 74 6b 40       	vpcmpeqb 0x40(%rbx),%ymm0,%ymm5
  b0:	c5 fd 74 73 60       	vpcmpeqb 0x60(%rbx),%ymm0,%ymm6
  b5:	c5 fd 74 1b          	vpcmpeqb (%rbx),%ymm0,%ymm3
  b9:	c5 fd 74 63 20       	vpcmpeqb 0x20(%rbx),%ymm0,%ymm4
  be:	c4 c1 45 eb d2       	vpor   %ymm10,%ymm7,%ymm2
  c3:	c4 c1 35 eb c8       	vpor   %ymm8,%ymm9,%ymm1
  c8:	c5 f5 eb cd          	vpor   %ymm5,%ymm1,%ymm1
  cc:	c5 ed eb d6          	vpor   %ymm6,%ymm2,%ymm2
  d0:	c5 f5 eb cb          	vpor   %ymm3,%ymm1,%ymm1
  d4:	c5 ed eb d4          	vpor   %ymm4,%ymm2,%ymm2
  d8:	c5 f5 eb ca          	vpor   %ymm2,%ymm1,%ymm1
  dc:	c5 fd d7 c1          	vpmovmskb %ymm1,%eax
  e0:	85 c0                	test   %eax,%eax
  e2:	74 8c                	je     70 <grep_vector_memchr+0x70>
  e4:	c5 fd d7 c3          	vpmovmskb %ymm3,%eax
  e8:	85 c0                	test   %eax,%eax
  ea:	0f 84 18 04 00 00    	je     508 <grep_vector_memchr+0x508>
  f0:	f3 0f bc c0          	tzcnt  %eax,%eax
  f4:	48 01 d8             	add    %rbx,%rax
  f7:	c5 f8 77             	vzeroupper
  fa:	c5 f8 28 74 24 50    	vmovaps 0x50(%rsp),%xmm6
 100:	c5 f8 28 7c 24 60    	vmovaps 0x60(%rsp),%xmm7
 106:	c5 78 28 44 24 70    	vmovaps 0x70(%rsp),%xmm8
 10c:	c5 78 28 8c 24 80 00 	vmovaps 0x80(%rsp),%xmm9
 113:	00 00 
 115:	c5 78 28 94 24 90 00 	vmovaps 0x90(%rsp),%xmm10
 11c:	00 00 
 11e:	48 81 c4 a0 00 00 00 	add    $0xa0,%rsp
 125:	5b                   	pop    %rbx
 126:	c3                   	ret
 127:	66 0f 1f 84 00 00 00 	nopw   0x0(%rax,%rax,1)
 12e:	00 00 
 130:	c5 f8 77             	vzeroupper
 133:	c5 f8 28 74 24 50    	vmovaps 0x50(%rsp),%xmm6
 139:	c5 f8 28 7c 24 60    	vmovaps 0x60(%rsp),%xmm7
 13f:	48 89 d9             	mov    %rbx,%rcx
 142:	c5 78 28 44 24 70    	vmovaps 0x70(%rsp),%xmm8
 148:	c5 78 28 8c 24 80 00 	vmovaps 0x80(%rsp),%xmm9
 14f:	00 00 
 151:	c5 78 28 94 24 90 00 	vmovaps 0x90(%rsp),%xmm10
 158:	00 00 
 15a:	48 81 c4 a0 00 00 00 	add    $0xa0,%rsp
 161:	5b                   	pop    %rbx
 162:	e9 00 00 00 00       	jmp    167 <grep_vector_memchr+0x167>
 167:	66 0f 1f 84 00 00 00 	nopw   0x0(%rax,%rax,1)
 16e:	00 00 
 170:	49 83 f8 1f          	cmp    $0x1f,%r8
 174:	0f 86 08 01 00 00    	jbe    282 <grep_vector_memchr+0x282>
 17a:	c5 fd 74 0b          	vpcmpeqb (%rbx),%ymm0,%ymm1
 17e:	c5 fd d7 c1          	vpmovmskb %ymm1,%eax
 182:	85 c0                	test   %eax,%eax
 184:	0f 85 66 ff ff ff    	jne    f0 <grep_vector_memchr+0xf0>
 18a:	49 8d 40 e0          	lea    -0x20(%r8),%rax
 18e:	48 8d 4b 20          	lea    0x20(%rbx),%rcx
 192:	48 83 f8 1f          	cmp    $0x1f,%rax
 196:	0f 86 b4 03 00 00    	jbe    550 <grep_vector_memchr+0x550>
 19c:	c5 fd 74 4b 20       	vpcmpeqb 0x20(%rbx),%ymm0,%ymm1
 1a1:	c5 fd d7 c1          	vpmovmskb %ymm1,%eax
 1a5:	85 c0                	test   %eax,%eax
 1a7:	0f 85 73 03 00 00    	jne    520 <grep_vector_memchr+0x520>
 1ad:	49 8d 40 c0          	lea    -0x40(%r8),%rax
 1b1:	48 8d 4b 40          	lea    0x40(%rbx),%rcx
 1b5:	48 83 f8 1f          	cmp    $0x1f,%rax
 1b9:	0f 86 91 03 00 00    	jbe    550 <grep_vector_memchr+0x550>
 1bf:	c5 fd 74 4b 40       	vpcmpeqb 0x40(%rbx),%ymm0,%ymm1
 1c4:	c5 fd d7 c1          	vpmovmskb %ymm1,%eax
 1c8:	85 c0                	test   %eax,%eax
 1ca:	0f 85 50 03 00 00    	jne    520 <grep_vector_memchr+0x520>
 1d0:	49 8d 40 a0          	lea    -0x60(%r8),%rax
 1d4:	48 8d 4b 60          	lea    0x60(%rbx),%rcx
 1d8:	48 83 f8 1f          	cmp    $0x1f,%rax
 1dc:	0f 86 6e 03 00 00    	jbe    550 <grep_vector_memchr+0x550>
 1e2:	c5 fd 74 4b 60       	vpcmpeqb 0x60(%rbx),%ymm0,%ymm1
 1e7:	c5 fd d7 c1          	vpmovmskb %ymm1,%eax
 1eb:	85 c0                	test   %eax,%eax
 1ed:	0f 85 2d 03 00 00    	jne    520 <grep_vector_memchr+0x520>
 1f3:	49 8d 40 80          	lea    -0x80(%r8),%rax
 1f7:	48 8d 8b 80 00 00 00 	lea    0x80(%rbx),%rcx
 1fe:	48 83 f8 1f          	cmp    $0x1f,%rax
 202:	0f 86 48 03 00 00    	jbe    550 <grep_vector_memchr+0x550>
 208:	c5 fd 74 8b 80 00 00 	vpcmpeqb 0x80(%rbx),%ymm0,%ymm1
 20f:	00 
 210:	c5 fd d7 c1          	vpmovmskb %ymm1,%eax
 214:	85 c0                	test   %eax,%eax
 216:	0f 85 04 03 00 00    	jne    520 <grep_vector_memchr+0x520>
 21c:	49 8d 80 60 ff ff ff 	lea    -0xa0(%r8),%rax
 223:	48 8d 8b a0 00 00 00 	lea    0xa0(%rbx),%rcx
 22a:	48 83 f8 1f          	cmp    $0x1f,%rax
 22e:	0f 86 1c 03 00 00    	jbe    550 <grep_vector_memchr+0x550>
 234:	c5 fd 74 8b a0 00 00 	vpcmpeqb 0xa0(%rbx),%ymm0,%ymm1
 23b:	00 
 23c:	c5 fd d7 c1          	vpmovmskb %ymm1,%eax
 240:	85 c0                	test   %eax,%eax
 242:	0f 85 d8 02 00 00    	jne    520 <grep_vector_memchr+0x520>
 248:	49 8d 80 40 ff ff ff 	lea    -0xc0(%r8),%rax
 24f:	48 8d 8b c0 00 00 00 	lea    0xc0(%rbx),%rcx
 256:	48 83 f8 1f          	cmp    $0x1f,%rax
 25a:	0f 86 f0 02 00 00    	jbe    550 <grep_vector_memchr+0x550>
 260:	c5 fd 74 83 c0 00 00 	vpcmpeqb 0xc0(%rbx),%ymm0,%ymm0
 267:	00 
 268:	49 81 e8 e0 00 00 00 	sub    $0xe0,%r8
 26f:	48 81 c3 e0 00 00 00 	add    $0xe0,%rbx
 276:	c5 fd d7 c0          	vpmovmskb %ymm0,%eax
 27a:	85 c0                	test   %eax,%eax
 27c:	0f 85 9e 02 00 00    	jne    520 <grep_vector_memchr+0x520>
 282:	4d 85 c0             	test   %r8,%r8
 285:	0f 84 e5 01 00 00    	je     470 <grep_vector_memchr+0x470>
 28b:	49 89 d9             	mov    %rbx,%r9
 28e:	41 bb 17 00 00 00    	mov    $0x17,%r11d
 294:	4d 8d 50 ff          	lea    -0x1(%r8),%r10
 298:	49 f7 d9             	neg    %r9
 29b:	4c 89 c9             	mov    %r9,%rcx
 29e:	83 e1 0f             	and    $0xf,%ecx
 2a1:	48 8d 41 10          	lea    0x10(%rcx),%rax
 2a5:	4c 39 d8             	cmp    %r11,%rax
 2a8:	49 0f 42 c3          	cmovb  %r11,%rax
 2ac:	49 39 c2             	cmp    %rax,%r10
 2af:	0f 82 d5 02 00 00    	jb     58a <grep_vector_memchr+0x58a>
 2b5:	48 85 c9             	test   %rcx,%rcx
 2b8:	0f 84 ee 02 00 00    	je     5ac <grep_vector_memchr+0x5ac>
 2be:	3a 13                	cmp    (%rbx),%dl
 2c0:	0f 84 f9 02 00 00    	je     5bf <grep_vector_memchr+0x5bf>
 2c6:	48 8d 43 01          	lea    0x1(%rbx),%rax
 2ca:	41 f6 c1 0e          	test   $0xe,%r9b
 2ce:	0f 84 5c 01 00 00    	je     430 <grep_vector_memchr+0x430>
 2d4:	3a 53 01             	cmp    0x1(%rbx),%dl
 2d7:	0f 84 1a fe ff ff    	je     f7 <grep_vector_memchr+0xf7>
 2dd:	48 8d 43 02          	lea    0x2(%rbx),%rax
 2e1:	4d 8d 50 fe          	lea    -0x2(%r8),%r10
 2e5:	48 83 f9 02          	cmp    $0x2,%rcx
 2e9:	0f 86 41 01 00 00    	jbe    430 <grep_vector_memchr+0x430>
 2ef:	3a 53 02             	cmp    0x2(%rbx),%dl
 2f2:	0f 84 ff fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 2f8:	48 8d 43 03          	lea    0x3(%rbx),%rax
 2fc:	4d 8d 50 fd          	lea    -0x3(%r8),%r10
 300:	41 f6 c1 0c          	test   $0xc,%r9b
 304:	0f 84 26 01 00 00    	je     430 <grep_vector_memchr+0x430>
 30a:	3a 53 03             	cmp    0x3(%rbx),%dl
 30d:	0f 84 e4 fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 313:	48 8d 43 04          	lea    0x4(%rbx),%rax
 317:	4d 8d 50 fc          	lea    -0x4(%r8),%r10
 31b:	48 83 f9 04          	cmp    $0x4,%rcx
 31f:	0f 86 0b 01 00 00    	jbe    430 <grep_vector_memchr+0x430>
 325:	3a 53 04             	cmp    0x4(%rbx),%dl
 328:	0f 84 c9 fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 32e:	48 8d 43 05          	lea    0x5(%rbx),%rax
 332:	4d 8d 50 fb          	lea    -0x5(%r8),%r10
 336:	48 83 f9 05          	cmp    $0x5,%rcx
 33a:	0f 84 f0 00 00 00    	je     430 <grep_vector_memchr+0x430>
 340:	3a 53 05             	cmp    0x5(%rbx),%dl
 343:	0f 84 ae fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 349:	48 8d 43 06          	lea    0x6(%rbx),%rax
 34d:	4d 8d 50 fa          	lea    -0x6(%r8),%r10
 351:	48 83 f9 06          	cmp    $0x6,%rcx
 355:	0f 84 d5 00 00 00    	je     430 <grep_vector_memchr+0x430>
 35b:	3a 53 06             	cmp    0x6(%rbx),%dl
 35e:	0f 84 93 fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 364:	41 83 e1 08          	and    $0x8,%r9d
 368:	48 8d 43 07          	lea    0x7(%rbx),%rax
 36c:	4d 8d 50 f9          	lea    -0x7(%r8),%r10
 370:	0f 84 ba 00 00 00    	je     430 <grep_vector_memchr+0x430>
 376:	3a 53 07             	cmp    0x7(%rbx),%dl
 379:	0f 84 78 fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 37f:	48 8d 43 08          	lea    0x8(%rbx),%rax
 383:	4d 8d 50 f8          	lea    -0x8(%r8),%r10
 387:	48 83 f9 08          	cmp    $0x8,%rcx
 38b:	0f 84 9f 00 00 00    	je     430 <grep_vector_memchr+0x430>
 391:	3a 53 08             	cmp    0x8(%rbx),%dl
 394:	0f 84 5d fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 39a:	48 8d 43 09          	lea    0x9(%rbx),%rax
 39e:	4d 8d 50 f7          	lea    -0x9(%r8),%r10
 3a2:	48 83 f9 09          	cmp    $0x9,%rcx
 3a6:	0f 84 84 00 00 00    	je     430 <grep_vector_memchr+0x430>
 3ac:	3a 53 09             	cmp    0x9(%rbx),%dl
 3af:	0f 84 42 fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 3b5:	48 8d 43 0a          	lea    0xa(%rbx),%rax
 3b9:	4d 8d 50 f6          	lea    -0xa(%r8),%r10
 3bd:	48 83 f9 0a          	cmp    $0xa,%rcx
 3c1:	74 6d                	je     430 <grep_vector_memchr+0x430>
 3c3:	3a 53 0a             	cmp    0xa(%rbx),%dl
 3c6:	0f 84 2b fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 3cc:	48 8d 43 0b          	lea    0xb(%rbx),%rax
 3d0:	4d 8d 50 f5          	lea    -0xb(%r8),%r10
 3d4:	48 83 f9 0b          	cmp    $0xb,%rcx
 3d8:	74 56                	je     430 <grep_vector_memchr+0x430>
 3da:	3a 53 0b             	cmp    0xb(%rbx),%dl
 3dd:	0f 84 14 fd ff ff    	je     f7 <grep_vector_memchr+0xf7>
 3e3:	48 8d 43 0c          	lea    0xc(%rbx),%rax
 3e7:	4d 8d 50 f4          	lea    -0xc(%r8),%r10
 3eb:	48 83 f9 0c          	cmp    $0xc,%rcx
 3ef:	74 3f                	je     430 <grep_vector_memchr+0x430>
 3f1:	3a 53 0c             	cmp    0xc(%rbx),%dl
 3f4:	0f 84 fd fc ff ff    	je     f7 <grep_vector_memchr+0xf7>
 3fa:	48 8d 43 0d          	lea    0xd(%rbx),%rax
 3fe:	4d 8d 50 f3          	lea    -0xd(%r8),%r10
 402:	48 83 f9 0d          	cmp    $0xd,%rcx
 406:	74 28                	je     430 <grep_vector_memchr+0x430>
 408:	3a 53 0d             	cmp    0xd(%rbx),%dl
 40b:	0f 84 e6 fc ff ff    	je     f7 <grep_vector_memchr+0xf7>
 411:	48 8d 43 0e          	lea    0xe(%rbx),%rax
 415:	4d 8d 50 f2          	lea    -0xe(%r8),%r10
 419:	48 83 f9 0f          	cmp    $0xf,%rcx
 41d:	75 11                	jne    430 <grep_vector_memchr+0x430>
 41f:	3a 53 0e             	cmp    0xe(%rbx),%dl
 422:	0f 84 cf fc ff ff    	je     f7 <grep_vector_memchr+0xf7>
 428:	48 8d 43 0f          	lea    0xf(%rbx),%rax
 42c:	4d 8d 50 f1          	lea    -0xf(%r8),%r10
 430:	c5 f9 6f 44 24 20    	vmovdqa 0x20(%rsp),%xmm0
 436:	c5 f9 74 04 0b       	vpcmpeqb (%rbx,%rcx,1),%xmm0,%xmm0
 43b:	c4 e2 79 17 c0       	vptest %xmm0,%xmm0
 440:	0f 85 71 01 00 00    	jne    5b7 <grep_vector_memchr+0x5b7>
 446:	49 29 c8             	sub    %rcx,%r8
 449:	49 83 f8 10          	cmp    $0x10,%r8
 44d:	74 21                	je     470 <grep_vector_memchr+0x470>
 44f:	48 83 c0 10          	add    $0x10,%rax
 453:	4d 8d 42 f0          	lea    -0x10(%r10),%r8
 457:	49 01 c0             	add    %rax,%r8
 45a:	66 0f 1f 44 00 00    	nopw   0x0(%rax,%rax,1)
 460:	3a 10                	cmp    (%rax),%dl
 462:	0f 84 8f fc ff ff    	je     f7 <grep_vector_memchr+0xf7>
 468:	48 ff c0             	inc    %rax
 46b:	49 39 c0             	cmp    %rax,%r8
 46e:	75 f0                	jne    460 <grep_vector_memchr+0x460>
 470:	31 c0                	xor    %eax,%eax
 472:	e9 80 fc ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 477:	66 0f 1f 84 00 00 00 	nopw   0x0(%rax,%rax,1)
 47e:	00 00 
 480:	89 54 24 4c          	mov    %edx,0x4c(%rsp)
 484:	48 8d 0d 00 00 00 00 	lea    0x0(%rip),%rcx        # 48b <grep_vector_memchr+0x48b>
 48b:	4c 89 44 24 40       	mov    %r8,0x40(%rsp)
 490:	c5 f8 77             	vzeroupper
 493:	e8 00 00 00 00       	call   498 <grep_vector_memchr+0x498>
 498:	4c 8b 44 24 40       	mov    0x40(%rsp),%r8
 49d:	8b 54 24 4c          	mov    0x4c(%rsp),%edx
 4a1:	85 c0                	test   %eax,%eax
 4a3:	0f 84 a4 fb ff ff    	je     4d <grep_vector_memchr+0x4d>
 4a9:	48 8d 0d 00 00 00 00 	lea    0x0(%rip),%rcx        # 4b0 <grep_vector_memchr+0x4b0>
 4b0:	e8 00 00 00 00       	call   4b5 <grep_vector_memchr+0x4b5>
 4b5:	4c 8b 44 24 40       	mov    0x40(%rsp),%r8
 4ba:	8b 54 24 4c          	mov    0x4c(%rsp),%edx
 4be:	48 89 c1             	mov    %rax,%rcx
 4c1:	31 c0                	xor    %eax,%eax
 4c3:	48 85 c9             	test   %rcx,%rcx
 4c6:	74 11                	je     4d9 <grep_vector_memchr+0x4d9>
 4c8:	0f b6 01             	movzbl (%rcx),%eax
 4cb:	83 e8 31             	sub    $0x31,%eax
 4ce:	0f 84 96 00 00 00    	je     56a <grep_vector_memchr+0x56a>
 4d4:	85 c0                	test   %eax,%eax
 4d6:	0f 94 c0             	sete   %al
 4d9:	48 8d 0d 00 00 00 00 	lea    0x0(%rip),%rcx        # 4e0 <grep_vector_memchr+0x4e0>
 4e0:	89 54 24 4c          	mov    %edx,0x4c(%rsp)
 4e4:	4c 89 44 24 40       	mov    %r8,0x40(%rsp)
 4e9:	88 05 08 00 00 00    	mov    %al,0x8(%rip)        # 4f7 <grep_vector_memchr+0x4f7>
 4ef:	e8 00 00 00 00       	call   4f4 <grep_vector_memchr+0x4f4>
 4f4:	8b 54 24 4c          	mov    0x4c(%rsp),%edx
 4f8:	4c 8b 44 24 40       	mov    0x40(%rsp),%r8
 4fd:	e9 4b fb ff ff       	jmp    4d <grep_vector_memchr+0x4d>
 502:	66 0f 1f 44 00 00    	nopw   0x0(%rax,%rax,1)
 508:	c5 fd d7 c4          	vpmovmskb %ymm4,%eax
 50c:	85 c0                	test   %eax,%eax
 50e:	74 20                	je     530 <grep_vector_memchr+0x530>
 510:	f3 0f bc c0          	tzcnt  %eax,%eax
 514:	83 c0 20             	add    $0x20,%eax
 517:	48 01 d8             	add    %rbx,%rax
 51a:	e9 d8 fb ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 51f:	90                   	nop
 520:	48 89 cb             	mov    %rcx,%rbx
 523:	e9 c8 fb ff ff       	jmp    f0 <grep_vector_memchr+0xf0>
 528:	0f 1f 84 00 00 00 00 	nopl   0x0(%rax,%rax,1)
 52f:	00 
 530:	c5 fd d7 c5          	vpmovmskb %ymm5,%eax
 534:	85 c0                	test   %eax,%eax
 536:	75 23                	jne    55b <grep_vector_memchr+0x55b>
 538:	c5 fd d7 c6          	vpmovmskb %ymm6,%eax
 53c:	85 c0                	test   %eax,%eax
 53e:	74 33                	je     573 <grep_vector_memchr+0x573>
 540:	f3 0f bc c0          	tzcnt  %eax,%eax
 544:	83 c0 60             	add    $0x60,%eax
 547:	48 01 d8             	add    %rbx,%rax
 54a:	e9 a8 fb ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 54f:	90                   	nop
 550:	49 89 c0             	mov    %rax,%r8
 553:	48 89 cb             	mov    %rcx,%rbx
 556:	e9 27 fd ff ff       	jmp    282 <grep_vector_memchr+0x282>
 55b:	f3 0f bc c0          	tzcnt  %eax,%eax
 55f:	83 c0 40             	add    $0x40,%eax
 562:	48 01 d8             	add    %rbx,%rax
 565:	e9 8d fb ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 56a:	0f b6 41 01          	movzbl 0x1(%rcx),%eax
 56e:	e9 61 ff ff ff       	jmp    4d4 <grep_vector_memchr+0x4d4>
 573:	c5 fd d7 c7          	vpmovmskb %ymm7,%eax
 577:	85 c0                	test   %eax,%eax
 579:	74 17                	je     592 <grep_vector_memchr+0x592>
 57b:	f3 0f bc c0          	tzcnt  %eax,%eax
 57f:	83 e8 80             	sub    $0xffffff80,%eax
 582:	48 01 d8             	add    %rbx,%rax
 585:	e9 6d fb ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 58a:	48 89 d8             	mov    %rbx,%rax
 58d:	e9 c5 fe ff ff       	jmp    457 <grep_vector_memchr+0x457>
 592:	c4 c1 7d d7 c2       	vpmovmskb %ymm10,%eax
 597:	85 c0                	test   %eax,%eax
 599:	74 2c                	je     5c7 <grep_vector_memchr+0x5c7>
 59b:	f3 0f bc c0          	tzcnt  %eax,%eax
 59f:	05 a0 00 00 00       	add    $0xa0,%eax
 5a4:	48 01 d8             	add    %rbx,%rax
 5a7:	e9 4b fb ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 5ac:	4d 89 c2             	mov    %r8,%r10
 5af:	48 89 d8             	mov    %rbx,%rax
 5b2:	e9 79 fe ff ff       	jmp    430 <grep_vector_memchr+0x430>
 5b7:	4d 89 d0             	mov    %r10,%r8
 5ba:	e9 98 fe ff ff       	jmp    457 <grep_vector_memchr+0x457>
 5bf:	48 89 d8             	mov    %rbx,%rax
 5c2:	e9 30 fb ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 5c7:	c4 c1 7d d7 c1       	vpmovmskb %ymm9,%eax
 5cc:	85 c0                	test   %eax,%eax
 5ce:	74 11                	je     5e1 <grep_vector_memchr+0x5e1>
 5d0:	f3 0f bc c0          	tzcnt  %eax,%eax
 5d4:	05 c0 00 00 00       	add    $0xc0,%eax
 5d9:	48 01 d8             	add    %rbx,%rax
 5dc:	e9 16 fb ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 5e1:	c4 c1 7d d7 c0       	vpmovmskb %ymm8,%eax
 5e6:	f3 0f bc c0          	tzcnt  %eax,%eax
 5ea:	05 e0 00 00 00       	add    $0xe0,%eax
 5ef:	48 01 d8             	add    %rbx,%rax
 5f2:	e9 00 fb ff ff       	jmp    f7 <grep_vector_memchr+0xf7>
 5f7:	66 0f 1f 84 00 00 00 	nopw   0x0(%rax,%rax,1)
 5fe:	00 00 

0000000000000600 <grep_vector_memchr2>:
 600:	56                   	push   %rsi
 601:	53                   	push   %rbx
 602:	48 81 ec d8 00 00 00 	sub    $0xd8,%rsp
 609:	c5 f8 29 74 24 70    	vmovaps %xmm6,0x70(%rsp)
 60f:	c5 f8 29 bc 24 80 00 	vmovaps %xmm7,0x80(%rsp)
 616:	00 00 
 618:	c5 78 29 84 24 90 00 	vmovaps %xmm8,0x90(%rsp)
 61f:	00 00 
 621:	c5 78 29 8c 24 a0 00 	vmovaps %xmm9,0xa0(%rsp)
 628:	00 00 
 62a:	c5 78 29 94 24 b0 00 	vmovaps %xmm10,0xb0(%rsp)
 631:	00 00 
 633:	c5 78 29 9c 24 c0 00 	vmovaps %xmm11,0xc0(%rsp)
 63a:	00 00 
 63c:	c5 f9 6e fa          	vmovd  %edx,%xmm7
 640:	c4 c1 79 6e f0       	vmovd  %r8d,%xmm6
 645:	48 89 cb             	mov    %rcx,%rbx
 648:	89 d6                	mov    %edx,%esi
 64a:	c4 e2 7d 78 ff       	vpbroadcastb %xmm7,%ymm7
 64f:	c4 e2 7d 78 f6       	vpbroadcastb %xmm6,%ymm6
 654:	c5 fe 7f 7c 24 40    	vmovdqu %ymm7,0x40(%rsp)
 65a:	c5 fe 7f 74 24 20    	vmovdqu %ymm6,0x20(%rsp)
 660:	0f b6 05 00 00 00 00 	movzbl 0x0(%rip),%eax        # 667 <grep_vector_memchr2+0x67>
 667:	84 c0                	test   %al,%al
 669:	0f 84 e9 03 00 00    	je     a58 <grep_vector_memchr2+0x458>
 66f:	80 3d 07 00 00 00 00 	cmpb   $0x0,0x7(%rip)        # 67d <grep_vector_memchr2+0x7d>
 676:	0f 84 8c 03 00 00    	je     a08 <grep_vector_memchr2+0x408>
 67c:	c5 f9 6e c6          	vmovd  %esi,%xmm0
 680:	c5 fe 6f 4c 24 20    	vmovdqu 0x20(%rsp),%ymm1
 686:	c4 e2 7d 78 c0       	vpbroadcastb %xmm0,%ymm0
 68b:	49 81 f9 ff 00 00 00 	cmp    $0xff,%r9
 692:	77 27                	ja     6bb <grep_vector_memchr2+0xbb>
 694:	e9 37 01 00 00       	jmp    7d0 <grep_vector_memchr2+0x1d0>
 699:	0f 1f 80 00 00 00 00 	nopl   0x0(%rax)
 6a0:	49 81 e9 00 01 00 00 	sub    $0x100,%r9
 6a7:	48 81 c3 00 01 00 00 	add    $0x100,%rbx
 6ae:	49 81 f9 ff 00 00 00 	cmp    $0xff,%r9
 6b5:	0f 86 15 01 00 00    	jbe    7d0 <grep_vector_memchr2+0x1d0>
 6bb:	c5 fe 6f 13          	vmovdqu (%rbx),%ymm2
 6bf:	c5 fd 74 da          	vpcmpeqb %ymm2,%ymm0,%ymm3
 6c3:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 6c7:	c5 e5 eb da          	vpor   %ymm2,%ymm3,%ymm3
 6cb:	c5 fe 6f 53 20       	vmovdqu 0x20(%rbx),%ymm2
 6d0:	c5 fd 74 e2          	vpcmpeqb %ymm2,%ymm0,%ymm4
 6d4:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 6d8:	c5 dd eb e2          	vpor   %ymm2,%ymm4,%ymm4
 6dc:	c5 fe 6f 53 40       	vmovdqu 0x40(%rbx),%ymm2
 6e1:	c5 fd 74 ea          	vpcmpeqb %ymm2,%ymm0,%ymm5
 6e5:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 6e9:	c5 d5 eb ea          	vpor   %ymm2,%ymm5,%ymm5
 6ed:	c5 fe 6f 53 60       	vmovdqu 0x60(%rbx),%ymm2
 6f2:	c5 fd 74 f2          	vpcmpeqb %ymm2,%ymm0,%ymm6
 6f6:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 6fa:	c5 cd eb f2          	vpor   %ymm2,%ymm6,%ymm6
 6fe:	c5 fe 6f 93 80 00 00 	vmovdqu 0x80(%rbx),%ymm2
 705:	00 
 706:	c5 55 eb de          	vpor   %ymm6,%ymm5,%ymm11
 70a:	c5 fd 74 fa          	vpcmpeqb %ymm2,%ymm0,%ymm7
 70e:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 712:	c5 c5 eb fa          	vpor   %ymm2,%ymm7,%ymm7
 716:	c5 fe 6f 93 a0 00 00 	vmovdqu 0xa0(%rbx),%ymm2
 71d:	00 
 71e:	c5 7d 74 d2          	vpcmpeqb %ymm2,%ymm0,%ymm10
 722:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 726:	c5 2d eb d2          	vpor   %ymm2,%ymm10,%ymm10
 72a:	c5 fe 6f 93 c0 00 00 	vmovdqu 0xc0(%rbx),%ymm2
 731:	00 
 732:	c4 41 25 eb da       	vpor   %ymm10,%ymm11,%ymm11
 737:	c5 7d 74 ca          	vpcmpeqb %ymm2,%ymm0,%ymm9
 73b:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 73f:	c5 35 eb ca          	vpor   %ymm2,%ymm9,%ymm9
 743:	c5 fe 6f 93 e0 00 00 	vmovdqu 0xe0(%rbx),%ymm2
 74a:	00 
 74b:	c5 7d 74 c2          	vpcmpeqb %ymm2,%ymm0,%ymm8
 74f:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 753:	c5 3d eb c2          	vpor   %ymm2,%ymm8,%ymm8
 757:	c5 e5 eb d4          	vpor   %ymm4,%ymm3,%ymm2
 75b:	c5 ed eb d7          	vpor   %ymm7,%ymm2,%ymm2
 75f:	c4 41 25 eb d8       	vpor   %ymm8,%ymm11,%ymm11
 764:	c4 c1 6d eb d1       	vpor   %ymm9,%ymm2,%ymm2
 769:	c4 c1 6d eb d3       	vpor   %ymm11,%ymm2,%ymm2
 76e:	c5 fd d7 c2          	vpmovmskb %ymm2,%eax
 772:	85 c0                	test   %eax,%eax
 774:	0f 84 26 ff ff ff    	je     6a0 <grep_vector_memchr2+0xa0>
 77a:	c5 fd d7 c3          	vpmovmskb %ymm3,%eax
 77e:	85 c0                	test   %eax,%eax
 780:	0f 84 5a 03 00 00    	je     ae0 <grep_vector_memchr2+0x4e0>
 786:	f3 0f bc c0          	tzcnt  %eax,%eax
 78a:	48 01 d8             	add    %rbx,%rax
 78d:	c5 f8 77             	vzeroupper
 790:	c5 f8 28 74 24 70    	vmovaps 0x70(%rsp),%xmm6
 796:	c5 f8 28 bc 24 80 00 	vmovaps 0x80(%rsp),%xmm7
 79d:	00 00 
 79f:	c5 78 28 84 24 90 00 	vmovaps 0x90(%rsp),%xmm8
 7a6:	00 00 
 7a8:	c5 78 28 8c 24 a0 00 	vmovaps 0xa0(%rsp),%xmm9
 7af:	00 00 
 7b1:	c5 78 28 94 24 b0 00 	vmovaps 0xb0(%rsp),%xmm10
 7b8:	00 00 
 7ba:	c5 78 28 9c 24 c0 00 	vmovaps 0xc0(%rsp),%xmm11
 7c1:	00 00 
 7c3:	48 81 c4 d8 00 00 00 	add    $0xd8,%rsp
 7ca:	5b                   	pop    %rbx
 7cb:	5e                   	pop    %rsi
 7cc:	c3                   	ret
 7cd:	0f 1f 00             	nopl   (%rax)
 7d0:	49 83 f9 1f          	cmp    $0x1f,%r9
 7d4:	0f 86 58 01 00 00    	jbe    932 <grep_vector_memchr2+0x332>
 7da:	c5 fe 6f 13          	vmovdqu (%rbx),%ymm2
 7de:	c5 fd 74 da          	vpcmpeqb %ymm2,%ymm0,%ymm3
 7e2:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 7e6:	c5 e5 eb d2          	vpor   %ymm2,%ymm3,%ymm2
 7ea:	c5 fd d7 c2          	vpmovmskb %ymm2,%eax
 7ee:	85 c0                	test   %eax,%eax
 7f0:	75 94                	jne    786 <grep_vector_memchr2+0x186>
 7f2:	49 8d 41 e0          	lea    -0x20(%r9),%rax
 7f6:	48 8d 53 20          	lea    0x20(%rbx),%rdx
 7fa:	48 83 f8 1f          	cmp    $0x1f,%rax
 7fe:	0f 86 2c 03 00 00    	jbe    b30 <grep_vector_memchr2+0x530>
 804:	c5 fe 6f 53 20       	vmovdqu 0x20(%rbx),%ymm2
 809:	c5 fd 74 da          	vpcmpeqb %ymm2,%ymm0,%ymm3
 80d:	c5 f5 74 d2          	vpcmpeqb %ymm2,%ymm1,%ymm2
 811:	c5 e5 eb d2          	vpor   %ymm2,%ymm3,%ymm2
 815:	c5 fd d7 c2          	vpmovmskb %ymm2,%eax
 819:	85 c0                	test   %eax,%eax
 81b:	0f 85 df 02 00 00    	jne    b00 <grep_vector_memchr2+0x500>
 821:	49 8d 41 c0          	lea    -0x40(%r9),%rax
 825:	48 8d 53 40          	lea    0x40(%rbx),%rdx
 829:	48 83 f8 1f          	cmp    $0x1f,%rax
 82d:	0f 86 fd 02 00 00    	jbe    b30 <grep_vector_memchr2+0x530>
 833:	c5 fe 6f 53 40       	vmovdqu 0x40(%rbx),%ymm2
 838:	c5 f5 74 da          	vpcmpeqb %ymm2,%ymm1,%ymm3
 83c:	c5 fd 74 d2          	vpcmpeqb %ymm2,%ymm0,%ymm2
 840:	c5 e5 eb d2          	vpor   %ymm2,%ymm3,%ymm2
 844:	c5 fd d7 c2          	vpmovmskb %ymm2,%eax
 848:	85 c0                	test   %eax,%eax
 84a:	0f 85 b0 02 00 00    	jne    b00 <grep_vector_memchr2+0x500>
 850:	49 8d 41 a0          	lea    -0x60(%r9),%rax
 854:	48 8d 53 60          	lea    0x60(%rbx),%rdx
 858:	48 83 f8 1f          	cmp    $0x1f,%rax
 85c:	0f 86 ce 02 00 00    	jbe    b30 <grep_vector_memchr2+0x530>
 862:	c5 fe 6f 53 60       	vmovdqu 0x60(%rbx),%ymm2
 867:	c5 f5 74 da          	vpcmpeqb %ymm2,%ymm1,%ymm3
 86b:	c5 fd 74 d2          	vpcmpeqb %ymm2,%ymm0,%ymm2
 86f:	c5 e5 eb d2          	vpor   %ymm2,%ymm3,%ymm2
 873:	c5 fd d7 c2          	vpmovmskb %ymm2,%eax
 877:	85 c0                	test   %eax,%eax
 879:	0f 85 81 02 00 00    	jne    b00 <grep_vector_memchr2+0x500>
 87f:	49 8d 41 80          	lea    -0x80(%r9),%rax
 883:	48 8d 93 80 00 00 00 	lea    0x80(%rbx),%rdx
 88a:	48 83 f8 1f          	cmp    $0x1f,%rax
 88e:	0f 86 9c 02 00 00    	jbe    b30 <grep_vector_memchr2+0x530>
 894:	c5 fe 6f 93 80 00 00 	vmovdqu 0x80(%rbx),%ymm2
 89b:	00 
 89c:	c5 f5 74 da          	vpcmpeqb %ymm2,%ymm1,%ymm3
 8a0:	c5 fd 74 d2          	vpcmpeqb %ymm2,%ymm0,%ymm2
 8a4:	c5 e5 eb d2          	vpor   %ymm2,%ymm3,%ymm2
 8a8:	c5 fd d7 c2          	vpmovmskb %ymm2,%eax
 8ac:	85 c0                	test   %eax,%eax
 8ae:	0f 85 4c 02 00 00    	jne    b00 <grep_vector_memchr2+0x500>
 8b4:	49 8d 81 60 ff ff ff 	lea    -0xa0(%r9),%rax
 8bb:	48 8d 93 a0 00 00 00 	lea    0xa0(%rbx),%rdx
 8c2:	48 83 f8 1f          	cmp    $0x1f,%rax
 8c6:	0f 86 64 02 00 00    	jbe    b30 <grep_vector_memchr2+0x530>
 8cc:	c5 fe 6f 93 a0 00 00 	vmovdqu 0xa0(%rbx),%ymm2
 8d3:	00 
 8d4:	c5 f5 74 da          	vpcmpeqb %ymm2,%ymm1,%ymm3
 8d8:	c5 fd 74 d2          	vpcmpeqb %ymm2,%ymm0,%ymm2
 8dc:	c5 e5 eb d2          	vpor   %ymm2,%ymm3,%ymm2
 8e0:	c5 fd d7 c2          	vpmovmskb %ymm2,%eax
 8e4:	85 c0                	test   %eax,%eax
 8e6:	0f 85 14 02 00 00    	jne    b00 <grep_vector_memchr2+0x500>
 8ec:	49 8d 81 40 ff ff ff 	lea    -0xc0(%r9),%rax
 8f3:	48 8d 93 c0 00 00 00 	lea    0xc0(%rbx),%rdx
 8fa:	48 83 f8 1f          	cmp    $0x1f,%rax
 8fe:	0f 86 2c 02 00 00    	jbe    b30 <grep_vector_memchr2+0x530>
 904:	c5 fe 6f 93 c0 00 00 	vmovdqu 0xc0(%rbx),%ymm2
 90b:	00 
 90c:	49 81 e9 e0 00 00 00 	sub    $0xe0,%r9
 913:	48 81 c3 e0 00 00 00 	add    $0xe0,%rbx
 91a:	c5 fd 74 c2          	vpcmpeqb %ymm2,%ymm0,%ymm0
 91e:	c5 f5 74 ca          	vpcmpeqb %ymm2,%ymm1,%ymm1
 922:	c5 fd eb c1          	vpor   %ymm1,%ymm0,%ymm0
 926:	c5 fd d7 c0          	vpmovmskb %ymm0,%eax
 92a:	85 c0                	test   %eax,%eax
 92c:	0f 85 ce 01 00 00    	jne    b00 <grep_vector_memchr2+0x500>
 932:	4d 85 c9             	test   %r9,%r9
 935:	0f 84 c2 00 00 00    	je     9fd <grep_vector_memchr2+0x3fd>
 93b:	49 89 da             	mov    %rbx,%r10
 93e:	ba 17 00 00 00       	mov    $0x17,%edx
 943:	49 f7 da             	neg    %r10
 946:	41 83 e2 0f          	and    $0xf,%r10d
 94a:	49 8d 42 10          	lea    0x10(%r10),%rax
 94e:	48 39 d0             	cmp    %rdx,%rax
 951:	48 0f 42 c2          	cmovb  %rdx,%rax
 955:	49 8d 51 ff          	lea    -0x1(%r9),%rdx
 959:	48 39 c2             	cmp    %rax,%rdx
 95c:	0f 82 08 02 00 00    	jb     b6a <grep_vector_memchr2+0x56a>
 962:	4d 85 d2             	test   %r10,%r10
 965:	0f 84 2c 02 00 00    	je     b97 <grep_vector_memchr2+0x597>
 96b:	4a 8d 0c 13          	lea    (%rbx,%r10,1),%rcx
 96f:	48 89 d8             	mov    %rbx,%rax
 972:	66 0f 1f 44 00 00    	nopw   0x0(%rax,%rax,1)
 978:	0f b6 10             	movzbl (%rax),%edx
 97b:	40 38 d6             	cmp    %dl,%sil
 97e:	0f 84 09 fe ff ff    	je     78d <grep_vector_memchr2+0x18d>
 984:	41 38 d0             	cmp    %dl,%r8b
 987:	0f 84 00 fe ff ff    	je     78d <grep_vector_memchr2+0x18d>
 98d:	48 ff c0             	inc    %rax
 990:	48 39 c8             	cmp    %rcx,%rax
 993:	75 e3                	jne    978 <grep_vector_memchr2+0x378>
 995:	4a 8d 14 0b          	lea    (%rbx,%r9,1),%rdx
 999:	48 29 ca             	sub    %rcx,%rdx
 99c:	c4 a1 79 6f 0c 13    	vmovdqa (%rbx,%r10,1),%xmm1
 9a2:	c5 f9 6f 44 24 40    	vmovdqa 0x40(%rsp),%xmm0
 9a8:	c5 f9 74 c1          	vpcmpeqb %xmm1,%xmm0,%xmm0
 9ac:	c4 e2 79 17 c0       	vptest %xmm0,%xmm0
 9b1:	0f 85 bb 01 00 00    	jne    b72 <grep_vector_memchr2+0x572>
 9b7:	c5 f9 6f 44 24 20    	vmovdqa 0x20(%rsp),%xmm0
 9bd:	c5 f9 74 c1          	vpcmpeqb %xmm1,%xmm0,%xmm0
 9c1:	c4 e2 79 17 c0       	vptest %xmm0,%xmm0
 9c6:	0f 85 a6 01 00 00    	jne    b72 <grep_vector_memchr2+0x572>
 9cc:	4d 29 d1             	sub    %r10,%r9
 9cf:	49 83 f9 10          	cmp    $0x10,%r9
 9d3:	74 28                	je     9fd <grep_vector_memchr2+0x3fd>
 9d5:	48 8d 41 10          	lea    0x10(%rcx),%rax
 9d9:	4c 8d 4a f0          	lea    -0x10(%rdx),%r9
 9dd:	49 01 c1             	add    %rax,%r9
 9e0:	0f b6 10             	movzbl (%rax),%edx
 9e3:	40 38 d6             	cmp    %dl,%sil
 9e6:	0f 84 a1 fd ff ff    	je     78d <grep_vector_memchr2+0x18d>
 9ec:	41 38 d0             	cmp    %dl,%r8b
 9ef:	0f 84 98 fd ff ff    	je     78d <grep_vector_memchr2+0x18d>
 9f5:	48 ff c0             	inc    %rax
 9f8:	49 39 c1             	cmp    %rax,%r9
 9fb:	75 e3                	jne    9e0 <grep_vector_memchr2+0x3e0>
 9fd:	31 c0                	xor    %eax,%eax
 9ff:	e9 89 fd ff ff       	jmp    78d <grep_vector_memchr2+0x18d>
 a04:	0f 1f 40 00          	nopl   0x0(%rax)
 a08:	c5 f8 77             	vzeroupper
 a0b:	c5 f8 28 74 24 70    	vmovaps 0x70(%rsp),%xmm6
 a11:	c5 f8 28 bc 24 80 00 	vmovaps 0x80(%rsp),%xmm7
 a18:	00 00 
 a1a:	89 f2                	mov    %esi,%edx
 a1c:	c5 78 28 84 24 90 00 	vmovaps 0x90(%rsp),%xmm8
 a23:	00 00 
 a25:	c5 78 28 8c 24 a0 00 	vmovaps 0xa0(%rsp),%xmm9
 a2c:	00 00 
 a2e:	48 89 d9             	mov    %rbx,%rcx
 a31:	c5 78 28 94 24 b0 00 	vmovaps 0xb0(%rsp),%xmm10
 a38:	00 00 
 a3a:	c5 78 28 9c 24 c0 00 	vmovaps 0xc0(%rsp),%xmm11
 a41:	00 00 
 a43:	48 81 c4 d8 00 00 00 	add    $0xd8,%rsp
 a4a:	5b                   	pop    %rbx
 a4b:	5e                   	pop    %rsi
 a4c:	e9 00 00 00 00       	jmp    a51 <grep_vector_memchr2+0x451>
 a51:	0f 1f 80 00 00 00 00 	nopl   0x0(%rax)
 a58:	44 89 44 24 6c       	mov    %r8d,0x6c(%rsp)
 a5d:	48 8d 0d 00 00 00 00 	lea    0x0(%rip),%rcx        # a64 <grep_vector_memchr2+0x464>
 a64:	4c 89 4c 24 60       	mov    %r9,0x60(%rsp)
 a69:	c5 f8 77             	vzeroupper
 a6c:	e8 00 00 00 00       	call   a71 <grep_vector_memchr2+0x471>
 a71:	4c 8b 4c 24 60       	mov    0x60(%rsp),%r9
 a76:	44 8b 44 24 6c       	mov    0x6c(%rsp),%r8d
 a7b:	85 c0                	test   %eax,%eax
 a7d:	0f 84 ec fb ff ff    	je     66f <grep_vector_memchr2+0x6f>
 a83:	48 8d 0d 00 00 00 00 	lea    0x0(%rip),%rcx        # a8a <grep_vector_memchr2+0x48a>
 a8a:	e8 00 00 00 00       	call   a8f <grep_vector_memchr2+0x48f>
 a8f:	4c 8b 4c 24 60       	mov    0x60(%rsp),%r9
 a94:	44 8b 44 24 6c       	mov    0x6c(%rsp),%r8d
 a99:	48 89 c2             	mov    %rax,%rdx
 a9c:	31 c0                	xor    %eax,%eax
 a9e:	48 85 d2             	test   %rdx,%rdx
 aa1:	74 11                	je     ab4 <grep_vector_memchr2+0x4b4>
 aa3:	0f b6 02             	movzbl (%rdx),%eax
 aa6:	83 e8 31             	sub    $0x31,%eax
 aa9:	0f 84 9b 00 00 00    	je     b4a <grep_vector_memchr2+0x54a>
 aaf:	85 c0                	test   %eax,%eax
 ab1:	0f 94 c0             	sete   %al
 ab4:	48 8d 0d 00 00 00 00 	lea    0x0(%rip),%rcx        # abb <grep_vector_memchr2+0x4bb>
 abb:	44 89 44 24 6c       	mov    %r8d,0x6c(%rsp)
 ac0:	4c 89 4c 24 60       	mov    %r9,0x60(%rsp)
 ac5:	88 05 08 00 00 00    	mov    %al,0x8(%rip)        # ad3 <grep_vector_memchr2+0x4d3>
 acb:	e8 00 00 00 00       	call   ad0 <grep_vector_memchr2+0x4d0>
 ad0:	44 8b 44 24 6c       	mov    0x6c(%rsp),%r8d
 ad5:	4c 8b 4c 24 60       	mov    0x60(%rsp),%r9
 ada:	e9 90 fb ff ff       	jmp    66f <grep_vector_memchr2+0x6f>
 adf:	90                   	nop
 ae0:	c5 fd d7 c4          	vpmovmskb %ymm4,%eax
 ae4:	85 c0                	test   %eax,%eax
 ae6:	74 28                	je     b10 <grep_vector_memchr2+0x510>
 ae8:	f3 0f bc c0          	tzcnt  %eax,%eax
 aec:	83 c0 20             	add    $0x20,%eax
 aef:	48 01 d8             	add    %rbx,%rax
 af2:	e9 96 fc ff ff       	jmp    78d <grep_vector_memchr2+0x18d>
 af7:	66 0f 1f 84 00 00 00 	nopw   0x0(%rax,%rax,1)
 afe:	00 00 
 b00:	48 89 d3             	mov    %rdx,%rbx
 b03:	e9 7e fc ff ff       	jmp    786 <grep_vector_memchr2+0x186>
 b08:	0f 1f 84 00 00 00 00 	nopl   0x0(%rax,%rax,1)
 b0f:	00 
 b10:	c5 fd d7 c5          	vpmovmskb %ymm5,%eax
 b14:	85 c0                	test   %eax,%eax
 b16:	75 23                	jne    b3b <grep_vector_memchr2+0x53b>
 b18:	c5 fd d7 c6          	vpmovmskb %ymm6,%eax
 b1c:	85 c0                	test   %eax,%eax
 b1e:	74 33                	je     b53 <grep_vector_memchr2+0x553>
 b20:	f3 0f bc c0          	tzcnt  %eax,%eax
 b24:	83 c0 60             	add    $0x60,%eax
 b27:	48 01 d8             	add    %rbx,%rax
 b2a:	e9 5e fc ff ff       	jmp    78d <grep_vector_memchr2+0x18d>
 b2f:	90                   	nop
 b30:	49 89 c1             	mov    %rax,%r9
 b33:	48 89 d3             	mov    %rdx,%rbx
 b36:	e9 f7 fd ff ff       	jmp    932 <grep_vector_memchr2+0x332>
 b3b:	f3 0f bc c0          	tzcnt  %eax,%eax
 b3f:	83 c0 40             	add    $0x40,%eax
 b42:	48 01 d8             	add    %rbx,%rax
 b45:	e9 43 fc ff ff       	jmp    78d <grep_vector_memchr2+0x18d>
 b4a:	0f b6 42 01          	movzbl 0x1(%rdx),%eax
 b4e:	e9 5c ff ff ff       	jmp    aaf <grep_vector_memchr2+0x4af>
 b53:	c5 fd d7 c7          	vpmovmskb %ymm7,%eax
 b57:	85 c0                	test   %eax,%eax
 b59:	74 22                	je     b7d <grep_vector_memchr2+0x57d>
 b5b:	f3 0f bc c0          	tzcnt  %eax,%eax
 b5f:	83 e8 80             	sub    $0xffffff80,%eax
 b62:	48 01 d8             	add    %rbx,%rax
 b65:	e9 23 fc ff ff       	jmp    78d <grep_vector_memchr2+0x18d>
 b6a:	48 89 d8             	mov    %rbx,%rax
 b6d:	e9 6b fe ff ff       	jmp    9dd <grep_vector_memchr2+0x3dd>
 b72:	49 89 d1             	mov    %rdx,%r9
 b75:	48 89 c8             	mov    %rcx,%rax
 b78:	e9 60 fe ff ff       	jmp    9dd <grep_vector_memchr2+0x3dd>
 b7d:	c4 c1 7d d7 c2       	vpmovmskb %ymm10,%eax
 b82:	85 c0                	test   %eax,%eax
 b84:	74 1c                	je     ba2 <grep_vector_memchr2+0x5a2>
 b86:	f3 0f bc c0          	tzcnt  %eax,%eax
 b8a:	05 a0 00 00 00       	add    $0xa0,%eax
 b8f:	48 01 d8             	add    %rbx,%rax
 b92:	e9 f6 fb ff ff       	jmp    78d <grep_vector_memchr2+0x18d>
 b97:	4c 89 ca             	mov    %r9,%rdx
 b9a:	48 89 d9             	mov    %rbx,%rcx
 b9d:	e9 fa fd ff ff       	jmp    99c <grep_vector_memchr2+0x39c>
 ba2:	c4 c1 7d d7 c1       	vpmovmskb %ymm9,%eax
 ba7:	85 c0                	test   %eax,%eax
 ba9:	74 11                	je     bbc <grep_vector_memchr2+0x5bc>
 bab:	f3 0f bc c0          	tzcnt  %eax,%eax
 baf:	05 c0 00 00 00       	add    $0xc0,%eax
 bb4:	48 01 d8             	add    %rbx,%rax
 bb7:	e9 d1 fb ff ff       	jmp    78d <grep_vector_memchr2+0x18d>
 bbc:	c4 c1 7d d7 c0       	vpmovmskb %ymm8,%eax
 bc1:	f3 0f bc c0          	tzcnt  %eax,%eax
 bc5:	05 e0 00 00 00       	add    $0xe0,%eax
 bca:	48 01 d8             	add    %rbx,%rax
 bcd:	e9 bb fb ff ff       	jmp    78d <grep_vector_memchr2+0x18d>
 bd2:	90                   	nop
 bd3:	90                   	nop
 bd4:	90                   	nop
 bd5:	90                   	nop
 bd6:	90                   	nop
 bd7:	90                   	nop
 bd8:	90                   	nop
 bd9:	90                   	nop
 bda:	90                   	nop
 bdb:	90                   	nop
 bdc:	90                   	nop
 bdd:	90                   	nop
 bde:	90                   	nop
 bdf:	90                   	nop
