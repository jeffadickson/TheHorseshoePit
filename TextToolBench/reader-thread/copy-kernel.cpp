// SPDX-License-Identifier: GPL-3.0-or-later
// READAHEAD-011: isolate copying with a reused 256 KiB destination, plus a
// separate immediate-consumption scenario. GNU grep timing remains the result
// that decides usefulness; this finite kernel is diagnostic evidence only.
#include <algorithm>
#include <array>
#include <chrono>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <iterator>
#include <random>
#include <stdexcept>
#include <vector>
extern "C" void copy_memcpy(void *, const void *, size_t);
extern "C" void copy_avx2(void *, const void *, size_t);
extern "C" void copy_rep(void *, const void *, size_t);
struct Cell { int kind; bool consume; };
int main(int argc, char **argv) {
    try {
        if (argc != 3) throw std::invalid_argument("Expected corpus path and summary path");
        std::ifstream input(argv[1], std::ios::binary);
        if (!input) throw std::runtime_error("Cannot open corpus");
        const std::vector<char> bytes((std::istreambuf_iterator<char>(input)), {});
        if (bytes.empty()) throw std::runtime_error("Expected a nonempty corpus");
        constexpr size_t chunk = 262144;
        std::vector<char> output(chunk);
        const auto newlines = static_cast<size_t>(std::count(bytes.begin(), bytes.end(), '\n'));
        using Copy = void (*)(void *, const void *, size_t);
        const Copy copies[] {copy_memcpy, copy_avx2, copy_rep};
        const char *names[] {"memcpy", "avx2", "rep"};
        std::vector<Cell> cells {{0,false},{1,false},{2,false},{0,true},{1,true},{2,true}};
        std::mt19937 random(20261026);
        constexpr int passes = 16;
        std::array<std::vector<double>, 6> measured;
        std::puts("Round,Engine,Task,Passes,BytesPerPass,MillisecondsPerPass");
        for (int round = -2; round < 15; ++round) {
            std::shuffle(cells.begin(), cells.end(), random);
            for (auto cell : cells) {
                size_t matches = 0;
                const auto start = std::chrono::steady_clock::now();
                for (int pass = 0; pass < passes; ++pass)
                    for (size_t offset = 0; offset < bytes.size(); offset += chunk) {
                        const auto count = std::min(chunk, bytes.size() - offset);
                        copies[cell.kind](output.data(), bytes.data() + offset, count);
                        if (cell.consume)
                            matches += static_cast<size_t>(std::count(output.data(), output.data() + count, '\n'));
                    }
                const auto stop = std::chrono::steady_clock::now();
                // Validate the final buffer outside timing. The consume arm
                // also verifies the aggregate independent newline reference.
                // All individual copy lengths/alignment combinations receive
                // stronger bytewise checks in copy-check.cpp, not this timer.
                const auto last = (bytes.size() - 1) / chunk * chunk;
                if (std::memcmp(output.data(), bytes.data() + last, bytes.size() - last) != 0
                    || matches != (cell.consume ? passes * newlines : 0))
                    throw std::runtime_error("Copy kernel differs from reference");
                const double ms = std::chrono::duration<double, std::milli>(stop - start).count() / passes;
                if (round >= 0) measured[cell.kind * 2 + cell.consume].push_back(ms);
                std::printf("%d,%s,%s,%d,%zu,%.9f\n", round, names[cell.kind],
                    cell.consume ? "copy-consume" : "copy", passes, bytes.size(), ms);
            }
        }
        std::ofstream summary(argv[2]);
        if (!summary) throw std::runtime_error("Cannot write kernel summary");
        summary.precision(12);
        summary << "Task,Engine,N,MedianMs,P25Ms,P75Ms\n";
        for (size_t i = 0; i < measured.size(); ++i) {
            auto &values = measured[i];
            std::sort(values.begin(), values.end());
            // Same linearly interpolated quantile convention as Experiment.cs.
            auto quantile = [&values](double probability) {
                const auto index = probability * static_cast<double>(values.size() - 1);
                const auto low = static_cast<size_t>(index);
                const auto high = std::min(low + 1, values.size() - 1);
                return values[low] + (values[high] - values[low]) * (index - low);
            };
            summary << (i % 2 ? "copy-consume" : "copy") << ',' << names[i / 2] << ',' << values.size()
                << ',' << quantile(0.5) << ',' << quantile(0.25) << ',' << quantile(0.75) << '\n';
        }
        if (!summary) throw std::runtime_error("Kernel summary write failed");
        return 0;
    } catch (const std::exception &error) { std::fprintf(stderr, "%s\n", error.what()); return 1; }
}
