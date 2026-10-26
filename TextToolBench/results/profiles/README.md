# Native Rust diagnostic stack samples

Separate release builds add debug information only. Their timings are not benchmark results. Samples are wall-clock instruction-pointer/call-stack snapshots of the owned main thread, including waiting/kernel locations. They are not ETW CPU samples or allocation counts. The sampler briefly suspends the child and can perturb execution; percentages are diagnostic evidence, not exact CPU-time shares. Three profiles per case; seeded 1–4 ms sleeps; up to 24 unwind frames; local PDBs.

## stefan / field

1338 samples across three runs; 0 samples without a caller.

| Leaf location | Samples | Percent |
|---|---:|---:|
| unknown@0x7FFD03D40000+0x161E54 | 365 | 27.3% |
| unknown@0x7FFD03D40000+0x161E34 | 244 | 18.2% |
| unknown@0x7FFD03D40000+0xCF3E5 | 55 | 4.1% |
| core::iter::adapters::map::impl$2::next<alloc::string::String,core::str::iter::SplitWhitespace,alloc::string::String (*)(ref$<str$>)> | 47 | 3.5% |
| unknown@0x7FFD03D40000+0x27413 | 25 | 1.9% |
| unknown@0x7FFD03D40000+0x32EFC | 24 | 1.8% |
| unknown@0x7FFD03D40000+0x32DBD | 23 | 1.7% |
| unknown@0x7FFD03D40000+0xCF3E2 | 12 | 0.9% |
| std::sys::alloc::windows::process_heap_alloc | 12 | 0.9% |
| unknown@0x7FFD03D40000+0x167693 | 12 | 0.9% |
| unknown@0x7FFD03D40000+0x27388 | 9 | 0.7% |
| std::alloc::__default_lib_allocator::__rdl_dealloc | 9 | 0.7% |

Inclusive caller counts are retained in observations.json; one sample can include multiple callers.

## stefan / words

436 samples across three runs; 0 samples without a caller.

| Leaf location | Samples | Percent |
|---|---:|---:|
| core::iter::adapters::map::impl$2::next<alloc::string::String,core::str::iter::SplitWhitespace,alloc::string::String (*)(ref$<str$>)> | 41 | 9.4% |
| unknown@0x7FFD03D40000+0xCF3E5 | 18 | 4.1% |
| core::hash::sip::impl$5::write<core::hash::sip::Sip13Rounds> | 13 | 3.0% |
| core::hash::BuildHasher::hash_one<std::hash::random::RandomState,ref$<str$> > | 13 | 3.0% |
| unknown@0x7FFD03D40000+0x32EFC | 11 | 2.5% |
| unknown@0x7FFD03D40000+0x27413 | 11 | 2.5% |
| std::alloc::__default_lib_allocator::__rdl_alloc | 10 | 2.3% |
| unknown@0x0+0x7FFCFDC3129B | 10 | 2.3% |
| unknown@0x7FFD03D40000+0x32DBD | 9 | 2.1% |
| unknown@0x7FFD03D40000+0x27384 | 7 | 1.6% |
| unknown@0x7FFD03D40000+0xCF3E2 | 6 | 1.4% |
| std::sys::alloc::windows::process_heap_alloc | 6 | 1.4% |

Inclusive caller counts are retained in observations.json; one sample can include multiple callers.

## stefan / lines

102 samples across three runs; 0 samples without a caller.

| Leaf location | Samples | Percent |
|---|---:|---:|
| unknown@0x7FFD03D40000+0x161E14 | 9 | 8.8% |
| unknown@0x7FFD03D40000+0x27311 | 8 | 7.8% |
| unknown@0x0+0x7FFCFDC3129B | 6 | 5.9% |
| unknown@0x7FFD03D40000+0xCF3E5 | 5 | 4.9% |
| unknown@0x7FFD03D40000+0x32EFC | 4 | 3.9% |
| core::slice::memchr::memchr_aligned | 4 | 3.9% |
| unknown@0x7FFD03D40000+0x162114 | 4 | 3.9% |
| rawk_core::evaluator::Evaluator::read_next_input_record | 4 | 3.9% |
| alloc::vec::spec_from_iter_nested::impl$0::from_iter<alloc::string::String,core::iter::adapters::map::Map<core::str::iter::Lines,rawk::execute::closure_env$0> > | 3 | 2.9% |
| core::hash::BuildHasher::hash_one<std::hash::random::RandomState,ref$<str$> > | 3 | 2.9% |
| unknown@0x7FFD03D40000+0xCF3E2 | 3 | 2.9% |
| core::hash::sip::impl$5::write<core::hash::sip::Sip13Rounds> | 3 | 2.9% |

Inclusive caller counts are retained in observations.json; one sample can include multiple callers.

## quinn / field

831 samples across three runs; 0 samples without a caller.

| Leaf location | Samples | Percent |
|---|---:|---:|
| unknown@0x7FFD03D40000+0x161E54 | 356 | 42.8% |
| unknown@0x7FFD03D40000+0x161E34 | 287 | 34.5% |
| awk_rs::interpreter::Interpreter::split_fields | 27 | 3.2% |
| unknown@0x7FFD03D40000+0x161E14 | 18 | 2.2% |
| awk_rs::interpreter::Interpreter::execute_stmt<std::io::stdio::StdoutLock> | 10 | 1.2% |
| std::sys::alloc::windows::process_heap_alloc | 6 | 0.7% |
| awk_rs::run | 5 | 0.6% |
| std::io::stdio::impl$19::write_all | 5 | 0.6% |
| awk_rs::interpreter::Interpreter::get_variable | 4 | 0.5% |
| unknown@0x7FFD03D40000+0x32DBD | 4 | 0.5% |
| alloc::io::buf_read::default_read_until<alloc::io::buffered::bufreader::BufReader<std::fs::File> > | 4 | 0.5% |
| unknown@0x7FFD03D40000+0x32EFC | 4 | 0.5% |

Inclusive caller counts are retained in observations.json; one sample can include multiple callers.

## quinn / words

220 samples across three runs; 0 samples without a caller.

| Leaf location | Samples | Percent |
|---|---:|---:|
| awk_rs::interpreter::Interpreter::split_fields | 31 | 14.1% |
| unknown@0x7FFD03D40000+0x161E14 | 15 | 6.8% |
| unknown@0x7FFD03D40000+0x32DBD | 12 | 5.5% |
| unknown@0x7FFD03D40000+0x27413 | 11 | 5.0% |
| awk_rs::interpreter::Interpreter::eval_expr_with_output<std::io::stdio::StdoutLock> | 8 | 3.6% |
| std::sys::alloc::windows::process_heap_alloc | 7 | 3.2% |
| unknown@0x7FFD03D40000+0x32EA5 | 5 | 2.3% |
| core::str::converts::from_utf8 | 4 | 1.8% |
| core::hash::sip::impl$5::write<core::hash::sip::Sip13Rounds> | 4 | 1.8% |
| unknown@0x7FFD03D40000+0x2739E | 4 | 1.8% |
| awk_rs::interpreter::Interpreter::get_variable | 4 | 1.8% |
| unknown@0x7FFD02DB0000+0x2346D | 3 | 1.4% |

Inclusive caller counts are retained in observations.json; one sample can include multiple callers.

## quinn / lines

163 samples across three runs; 0 samples without a caller.

| Leaf location | Samples | Percent |
|---|---:|---:|
| awk_rs::interpreter::Interpreter::split_fields | 27 | 16.6% |
| unknown@0x7FFD03D40000+0x161E14 | 18 | 11.0% |
| unknown@0x7FFD03D40000+0x32DBD | 8 | 4.9% |
| alloc::io::buf_read::default_read_until<alloc::io::buffered::bufreader::BufReader<std::fs::File> > | 7 | 4.3% |
| unknown@0x7FFD03D40000+0x27413 | 6 | 3.7% |
| std::sys::alloc::windows::process_heap_alloc | 4 | 2.5% |
| unknown@0x7FFD03D40000+0x2736D | 4 | 2.5% |
| unknown@0x7FFD03D40000+0x328B0 | 2 | 1.2% |
| unknown@0x7FFD03D40000+0x27388 | 2 | 1.2% |
| alloc::raw_vec::impl$4::reserve::do_reserve_and_handle<alloc::alloc::Global> | 2 | 1.2% |
| unknown@0x7FFD03D40000+0x27529 | 2 | 1.2% |
| unknown@0x7FFD03D40000+0x27384 | 2 | 1.2% |

Inclusive caller counts are retained in observations.json; one sample can include multiple callers.

