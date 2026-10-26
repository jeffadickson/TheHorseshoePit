//! Controlled fixture to verify that native sampling finds a known hot function and its caller.
use std::hint::black_box;
use std::time::{Duration, Instant};

/// Keep a named, uninlined function busy long enough for reproducible native stack sampling.
#[unsafe(no_mangle)]
#[inline(never)]
pub extern "C" fn profiler_canary_hot() -> u64 {
    let start = Instant::now();
    let mut value = 0x12345678_u64;
    while start.elapsed() < Duration::from_secs(2) {
        for index in 0..100_000_u64 {
            value = black_box(value.rotate_left(7).wrapping_add(index));
        }
    }
    value
}

fn main() {
    black_box(profiler_canary_hot());
}
