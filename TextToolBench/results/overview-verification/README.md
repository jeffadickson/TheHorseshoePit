# Overview verification

`results/overview.html` is a self-contained, responsive narrative and data explorer. It embeds all 427 seven-repeat summaries, keeps nine cohorts separate, and includes methods, experimental design, profiling/tuning evidence, correctness failures, coverage scope and limitations. It does not run new text-tool measurements.

The later BENCH-013 documentation update adds the detailed in-page test contract, ripgrep argument vectors and sample counts, workload semantics, statistics and the search/diff stopwatch distinction. Its verification is in `../methods-documentation-verification.json` and `methods-dom-tests.log`: four current DOM checks cover 134 views and cross-file methods fragments, and native/managed overview generation remains byte-identical. Earlier unit, mutation and source-hash artifacts below describe the original generator verification; they are historical evidence, not a new unit/mutation run for this HTML-only extension. No browser rendering was performed.

## Executed checks

- .NET 10 builds passed. The Windows x64 Native AOT generator was published and run. Its HTML SHA-256 matches the managed generator exactly; see `determinism.json`. Source-generated System.Text.Json metadata supports native compilation without reflective serialization.
- **46 NUnit tests passed**: 24 existing sed-reference tests and 22 overview tests. Overview tests cover totals, duplicates within/across sessions, missing cohorts, unknown operations, wrong-session operations/sizes, malformed JSON/templates, nonexistent directories, deterministic generation and 500 seeded valid summaries (seed **20261026**). Corrupted input must fail rather than publish a replacement page. No original timing campaign was rerun.
- **Four JavaScript DOM tests passed**, exploring **134 views** through actual select/change DOM APIs and checking numerical order, sample counts, SVG values, evidence links, offline resources, accessibility metadata and responsive/print stylesheet declarations. Two deliberate client mutations—descending ranking and mixed-session row filtering—were detected with a conforming original control.
- **Stryker.NET 5.0.0** model score: **85.58%**. There were **184 killed**, **30 surviving**, **one uncovered**, **16 compile-error** and **35 ignored** mutants; 214 were executed with a 46-test baseline. The JSON/HTML reports are retained. A first run exposed gaps in negative tests; accepted additions challenge same-session duplicates and invalid session/task substitutions. The final results above are the second run. Survivors include prose/error-message changes, ordering changes and excluded-size-boundary or defensive guards. Raw survivors remain visible; not all are asserted equivalent, and the score is not a proof of correctness.
- The StrictDoc export parsed BENCH-011 and the existing requirements. The legacy report and README link to the overview; raw measurement data and cohort labels remain unchanged.

## Coverage scope

| Scope | Line coverage | Branch coverage |
|---|---:|---:|
| `OverviewModel` | 98.38% | 93.44% |
| Generator entry point `Overview` | 100% | 90% |
| Entire Overview module, including DTO/generated serialization code | 87.54% | 84.41% |
| Embedded client JavaScript, Node test coverage | 100% | 93.75% |

JavaScript function coverage is 100%. These are distinct scopes and must not be averaged. Native AOT execution was checked for output equivalence, not instrumented for native coverage. The PowerShell build wrapper was executed; its line/branch coverage was not measured. The third-party benchmark binaries were not reinstrumented. `summary.json`, NUnit TRX/Cobertura, `dom-tests.log` and the mutation reports retain the evidence.

## Visual limitation

The browser tool's URL policy rejected opening the local `file:` URL. No alternate browser, local server, CDP, renderer or policy workaround was used. **Rendered desktop/mobile layout, screenshots and print appearance are unverified.** jsdom is a non-rendering DOM implementation: its checks establish interaction/data behavior and markup structure, not visual quality or browser layout equivalence.

The page uses system fonts, embedded CSS, inline SVG and embedded JSON, with no external scripts, stylesheet downloads or runtime fetches. It includes narrow-screen, print and reduced-motion styles, but their appearance still requires a human browser review.

## Reproduction

Run from a checkout with the existing `results/summary.json`. Rebuild with `scripts/Build-Overview.ps1 -Native` (or omit `-Native` for managed execution), run `-Mode Test`, and run `-Mode Mutation` separately. The Makefile exposes the corresponding `overview`, `overview-test` and `overview-mutation` targets. The normal `report` target regenerates both reports.

Optional DOM checks use Node **24.19.0**, pnpm **11.25.0** and jsdom **30.1.3**. jsdom is a development-only dependency chosen for standards-based HTML/DOM execution without a browser or network; it does not render CSS. Its package and transitive pnpm lockfile are retained in `scripts/overview-dom-package.json` and `scripts/overview-dom-lock.yaml`. The final HTML and generator have no Node dependency.

```powershell
$project=(Get-Location).Path
if (!(Test-Path -LiteralPath (Join-Path $project 'scripts') -PathType Container)) { throw 'Run from the checkout root' }
$tools=Join-Path $project 'tools'
if (!(Test-Path -LiteralPath $tools -PathType Container)) { New-Item -ItemType Directory -Path $tools | Out-Null }
$dev=Join-Path $tools 'overview-dom-tests'
if (!(Test-Path -LiteralPath $dev -PathType Container)) { New-Item -ItemType Directory -Path $dev | Out-Null }
Copy-Item -LiteralPath (Join-Path $project 'scripts/overview-dom-package.json') -Destination (Join-Path $dev 'package.json')
Copy-Item -LiteralPath (Join-Path $project 'scripts/overview-dom-lock.yaml') -Destination (Join-Path $dev 'pnpm-lock.yaml')
pnpm install --dir $dev --frozen-lockfile --ignore-scripts
if ($LASTEXITCODE -ne 0) { throw 'DOM dependency acquisition failed' }
node --test --experimental-test-coverage --test-coverage-include '**/overview-client.js' scripts/Test-OverviewDom.cjs
if ($LASTEXITCODE -ne 0) { throw 'DOM verification failed' }
```

The JavaScript is executed only from the generated, owned artifact with jsdom's `outside-only` execution mode; resource loading is disabled. Static evidence-link checks use filesystem existence, not browser navigation.
