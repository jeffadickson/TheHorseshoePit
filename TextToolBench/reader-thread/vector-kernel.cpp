// SPDX-License-Identifier: GPL-3.0-or-later
// In-memory comparison isolates search work from process startup and file I/O.
// File loading and independent expected counts are outside the timed regions.
#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <iterator>
#include <random>
#include <stdexcept>
#include <vector>
extern "C" void *grep32_memchr(const void *, int, size_t);
extern "C" void *grep32_memchr2(const void *, int, int, size_t);
extern "C" void *grep256_memchr(const void *, int, size_t);
extern "C" void *grep256_memchr2(const void *, int, int, size_t);
// The vector flag is enabled for the campaign; keep the scalar symbol valid.
extern "C" void *memchr2(const void *buffer, int a, int b, size_t n) {
    const auto *p = static_cast<const unsigned char *>(buffer);
    for (size_t i = 0; i < n; ++i)
        if (p[i] == static_cast<unsigned char>(a) || p[i] == static_cast<unsigned char>(b))
            return const_cast<unsigned char *>(p + i);
    return nullptr;
}
struct Cell { int block, task; };
int main(int argc, char **argv) {
    try {
        if (argc != 2) throw std::invalid_argument("Expected corpus path");
        std::ifstream input(argv[1], std::ios::binary);
        if (!input) throw std::runtime_error("Cannot open corpus");
        const std::vector<char> bytes((std::istreambuf_iterator<char>(input)), {});
        if (bytes.empty()) throw std::runtime_error("Expected a nonempty corpus");
        // Independent references establish the absent-byte assumptions and
        // expected number of newline matches for this exact corpus.
        if (std::find(bytes.begin(), bytes.end(), '\0') != bytes.end()
            || std::find(bytes.begin(), bytes.end(), static_cast<char>(0xff)) != bytes.end())
            throw std::runtime_error("Corpus does not satisfy absent-byte tasks");
        const auto newline_count = static_cast<size_t>(std::count(bytes.begin(), bytes.end(), '\n'));
        std::vector<Cell> cells {{32,0},{256,0},{32,1},{256,1},{32,2},{256,2}};
        std::mt19937 random(20261026);
        constexpr int passes = 16;
        std::puts("Round,Block,Task,Passes,BytesPerPass,MillisecondsPerPass");
        for (int round = -2; round < 15; ++round) {
            std::shuffle(cells.begin(), cells.end(), random);
            for (auto cell : cells) {
                const auto one = cell.block == 32 ? grep32_memchr : grep256_memchr;
                const auto two = cell.block == 32 ? grep32_memchr2 : grep256_memchr2;
                size_t matches = 0;
                const auto start = std::chrono::steady_clock::now();
                for (int pass = 0; pass < passes; ++pass) {
                    if (cell.task == 0) matches += one(bytes.data(), 0, bytes.size()) != nullptr;
                    else if (cell.task == 1) matches += two(bytes.data(), 0, 255, bytes.size()) != nullptr;
                    else {
                        const char *p = bytes.data(), *end = p + bytes.size();
                        while (p < end) {
                            const auto *found = static_cast<const char *>(one(p, '\n', static_cast<size_t>(end - p)));
                            if (!found) break;
                            ++matches;
                            p = found + 1;
                        }
                    }
                }
                const auto stop = std::chrono::steady_clock::now();
                if (matches != (cell.task == 2 ? passes * newline_count : 0))
                    throw std::runtime_error("Kernel output differs from reference");
                const double ms = std::chrono::duration<double, std::milli>(stop - start).count() / passes;
                const char *task = cell.task == 0 ? "one-absent" : cell.task == 1 ? "two-absent" : "newline-scan";
                std::printf("%d,%d,%s,%d,%zu,%.9f\n", round, cell.block, task, passes, bytes.size(), ms);
            }
        }
        return 0;
    } catch (const std::exception &error) {
        std::fprintf(stderr, "%s\n", error.what());
        return 1;
    }
}
