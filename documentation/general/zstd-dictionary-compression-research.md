# Zstandard Dictionary Compression for Static Web Assets: Research Report

## Executive Summary

This document presents the findings from an extensive research effort to evaluate zstandard (zstd)
compression — and specifically dictionary-based compression strategies — for .NET Static Web Assets,
with a focus on Blazor WebAssembly applications. The work was motivated by the desire to implement
Compression Dictionary Transport (CDT, RFC 9842) support in the Static Web Assets SDK.

### Key Findings

1. **Zstd standalone compression is ~5-8% worse than brotli** for web assets. Brotli's built-in
   web-content dictionary gives it a structural advantage for first-visit downloads.

2. **Shared zstd dictionaries (trained or raw) do not help** for webcil/WASM payloads. Every
   dictionary strategy we tested either made compression worse or provided negligible improvement.

3. **CDT delta compression (version-to-version) is the killer feature.** When users have a cached
   previous version, delta compression saves **21.7% vs brotli** on the update payload, with
   individual files seeing up to **98.6% savings** for minimally-changed assemblies.

4. **The value proposition of zstd is not standalone compression** — it is the ability to use
   dictionaries for delta-compressed updates via the CDT protocol.

---

## Table of Contents

1. [Background and Motivation](#1-background-and-motivation)
2. [Test Environment](#2-test-environment)
3. [Experiment 1: Baseline Compression Comparison](#3-experiment-1-baseline-compression-comparison)
4. [Experiment 2: Trained Shared Dictionary](#4-experiment-2-trained-shared-dictionary)
5. [Experiment 3: CoreLib as Raw Dictionary](#5-experiment-3-corelib-as-raw-dictionary)
6. [Experiment 4: Linked ↔ Unlinked Cross-Dictionary](#6-experiment-4-linked--unlinked-cross-dictionary)
7. [Experiment 5: Real CDT Delta Compression (v1→v2)](#7-experiment-5-real-cdt-delta-compression-v1v2)
8. [Anti-Patterns and Lessons Learned](#8-anti-patterns-and-lessons-learned)
9. [Design Patterns That Work](#9-design-patterns-that-work)
10. [Implementation Details](#10-implementation-details)
11. [Conclusions and Recommendations](#11-conclusions-and-recommendations)
12. [Appendix: .NET 11 Zstandard API Reference](#12-appendix-net-11-zstandard-api-reference)

---

## 1. Background and Motivation

### The Problem

Blazor WebAssembly applications ship a significant payload of .NET assemblies, JavaScript, CSS, and
data files (ICU globalization data) to the browser. For a typical Blazor WASM app, the initial download
is approximately **17.5 MB uncompressed** (~3.8 MB with brotli). On subsequent visits after SDK or
app updates, the browser must re-download any changed assets — even if only a few bytes changed.

### Why Zstandard?

Zstandard (zstd) is a modern compression algorithm developed by Facebook/Meta that offers:

- **Dictionary compression**: Files can be compressed using a previously-agreed dictionary, enabling
  delta-like compression where similar content is available as reference material.
- **Streaming decompression**: Fast decompression even at high compression levels.
- **CDT protocol support**: RFC 9842 (Compression Dictionary Transport) defines how browsers and
  servers can negotiate dictionary-based compression, enabling delta updates.

The key insight is that **zstd's value is not in replacing brotli for standalone compression** — it is
in enabling the CDT protocol for efficient version-to-version updates.

### Compression Dictionary Transport (CDT) — RFC 9842

CDT allows a previously-downloaded resource to serve as a compression dictionary for future versions
of the same resource. The flow:

1. Server sends response with `Use-As-Dictionary: match="/path/to/resource"` header
2. Browser stores the decompressed response body as a dictionary, keyed by its SHA-256 hash
3. On next request for a matching URL, browser sends `Available-Dictionary: :base64hash:` header
4. Server responds with `Content-Encoding: dcz` (dictionary-compressed zstd) using the old version
   as dictionary, sending only the delta

This means that for a Blazor WASM app update, the browser only downloads the *difference* between
the old and new versions of each file, rather than the entire file.

---

## 2. Test Environment

### Application Under Test

A Blazor Web App with WebAssembly interactivity (`dotnet new blazor --interactivity WebAssembly
--all-interactive`), representing a typical production deployment target.

### SDK and Runtime

| Component | Version |
|-----------|---------|
| .NET SDK | 11.0.100-dev (dogfood build from `dotnet/sdk` repo) |
| Runtime | .NET 11.0 Preview 4 |
| Mono WASM Runtime | 11.0.0-preview.4.26203.108 |
| Zstd Library | Built-in `System.IO.Compression.Zstandard` (.NET 11) |

### Compression Settings

| Algorithm | Quality Setting | Notes |
|-----------|----------------|-------|
| Gzip | `CompressionLevel.SmallestSize` | .NET default max compression |
| Brotli | `CompressionLevel.SmallestSize` | Quality 11 (maximum) |
| Zstd | `ZstandardCompressionOptions.MaxQuality` | Quality 22 (maximum) |

### Application Asset Profile

The published application contains **38 unique assets** in the `_framework/` directory:

| Category | Count | Uncompressed Size | Description |
|----------|-------|-------------------|-------------|
| WASM (webcil) | 28 | 2,543 KB | .NET assemblies wrapped in WebAssembly |
| WASM (native) | 1 | 3,054 KB | Native Mono WASM runtime (`dotnet.native.wasm`) |
| JavaScript | 6 | 834 KB | Blazor runtime JS files |
| ICU Data | 3 | 2,813 KB | Unicode globalization data |
| **Total** | **38** | **9,244 KB** | |

### Webcil Binary Format

All `.wasm` files (except `dotnet.native.*.wasm`) are **webcil** containers — .NET assemblies wrapped
in a minimal WebAssembly envelope:

```
Offset 0-171:   WASM envelope (172 bytes, identical across all webcil files)
                - Imports: "webcil" memory
                - Exports: webcilVersion, webcilSize, getWebcilSize, getWebcilPayload
Offset 172+:    WbIL magic marker, followed by .NET assembly IL payload
```

The 172-byte WASM envelope is identical across all webcil files. The payload after `WbIL` is a
standard .NET assembly (PE/COFF format with IL metadata).

---

## 3. Experiment 1: Baseline Compression Comparison

### Goal

Establish baseline compression ratios for gzip, brotli, and zstd across all asset categories
to understand where zstd stands relative to the incumbent (brotli).

### Methodology

Published the Blazor WASM app with all three compression formats enabled. Compared per-file and
per-category totals.

### Results: Full Application (All Asset Types)

| Category | Files | Original | Gzip | Brotli | Zstd | Zstd vs Brotli |
|----------|-------|----------|------|--------|------|----------------|
| CSS | 19 | 1.6 MB | 211 KB | 147 KB | 173 KB | **+17.7%** |
| Data (ICU) | 27 | 8.7 MB | 2.0 MB | 1.5 MB | 1.6 MB | **+10.7%** |
| JavaScript | 13 | 1.5 MB | 389 KB | 331 KB | 353 KB | **+6.5%** |
| WASM | 29 | 5.7 MB | 2.3 MB | 1.8 MB | 2.0 MB | **+5.7%** |
| **TOTAL** | **88** | **17.5 MB** | **4.9 MB** | **3.8 MB** | **4.1 MB** | **+8.2%** |

### Results: Framework Assets Only (`_framework/` Directory)

This is the subset we focused on for dictionary experiments, as it represents the download payload
for a Blazor WASM application:

| File | Original | Gzip | Brotli | Zstd | Zstd/Br |
|------|----------|------|--------|------|---------|
| dotnet.native.bvp3p2ro6r.wasm | 3,127,112 | 1,242,617 | 993,679 | 1,045,833 | +5.2% |
| System.Private.CoreLib.wasm | 1,604,377 | 622,464 | 501,485 | 530,803 | +5.8% |
| icudt_no_CJK.dat | 1,222,208 | 376,074 | 238,832 | 274,555 | +15.0% |
| icudt_CJK.dat | 1,035,792 | 378,049 | 260,366 | 299,711 | +15.1% |
| icudt_EFIGS.dat | 621,712 | 233,863 | 154,180 | 177,084 | +14.9% |
| System.Text.Json.wasm | 374,553 | 150,547 | 125,509 | 132,572 | +5.6% |
| Microsoft.AspNetCore.Components.wasm | 219,929 | 93,977 | 79,262 | 84,242 | +6.3% |
| blazor.web.js | 214,105 | 59,348 | 50,699 | 53,658 | +5.8% |
| dotnet.runtime.js | 196,482 | 56,657 | 47,309 | 50,507 | +6.8% |
| blazor.server.js | 178,566 | 48,396 | 41,764 | 44,221 | +5.9% |
| dotnet.native.js | 142,673 | 34,385 | 29,817 | 31,398 | +5.3% |
| Microsoft.AspNetCore.Components.WebAssembly.wasm | 118,041 | 47,935 | 39,857 | 42,378 | +6.3% |
| blazor.webassembly.js | 74,529 | 22,939 | 20,127 | 21,560 | +7.1% |
| System.Private.Uri.wasm | 64,793 | 29,900 | 26,211 | 27,576 | +5.2% |
| Microsoft.AspNetCore.Components.Web.wasm | 62,745 | 26,420 | 22,093 | 23,880 | +8.1% |
| System.Collections.Immutable.wasm | 51,481 | 22,513 | 19,044 | 20,580 | +8.1% |
| dotnet.js | 48,351 | 14,776 | 13,031 | 14,115 | +8.3% |
| Microsoft.Extensions.DependencyInjection.wasm | 46,873 | 22,780 | 19,518 | 21,010 | +7.6% |
| Microsoft.JSInterop.wasm | 45,849 | 21,108 | 18,241 | 19,408 | +6.4% |
| System.Runtime.InteropServices.JavaScript.wasm | 37,657 | 17,371 | 14,866 | 15,924 | +7.1% |
| System.Text.Encodings.Web.wasm | 31,001 | 11,899 | 10,282 | 10,959 | +6.6% |
| System.Linq.wasm | 27,929 | 13,629 | 11,745 | 12,583 | +7.1% |
| System.Collections.wasm | 21,273 | 9,863 | 8,674 | 9,223 | +6.3% |
| SizeCompare.Client.wasm | 20,761 | 8,989 | 7,894 | 8,399 | +6.4% |
| System.Text.RegularExpressions.wasm | 16,153 | 8,152 | 7,099 | 7,610 | +7.2% |
| System.Memory.wasm | 14,617 | 6,964 | 6,162 | 6,591 | +7.0% |
| System.Console.wasm | 14,105 | 7,099 | 6,036 | 6,516 | +7.9% |
| Microsoft.JSInterop.WebAssembly.wasm | 12,057 | 5,671 | 4,859 | 5,347 | +10.0% |
| System.Diagnostics.DiagnosticSource.wasm | 7,961 | 3,416 | 2,963 | 3,252 | +9.8% |
| Microsoft.Extensions.Configuration.Json.wasm | 7,961 | 3,322 | 2,795 | 3,210 | +14.8% |
| Microsoft.Extensions.Logging.wasm | 7,449 | 3,162 | 2,775 | 3,052 | +10.0% |
| Microsoft.Extensions.Configuration.EnvironmentVariables.wasm | 7,449 | 2,812 | 2,389 | 2,742 | +14.8% |
| System.Security.Cryptography.wasm | 6,937 | 3,232 | 2,843 | 3,098 | +9.0% |
| Microsoft.Extensions.Configuration.wasm | 6,425 | 2,814 | 2,411 | 2,716 | +12.7% |
| System.IO.Pipelines.wasm | 5,913 | 2,775 | 2,389 | 2,685 | +12.4% |
| System.Runtime.wasm | 5,913 | 2,382 | 2,073 | 2,296 | +10.8% |
| System.Collections.Concurrent.wasm | 5,913 | 2,367 | 2,077 | 2,275 | +9.5% |
| System.ComponentModel.wasm | 4,889 | 1,979 | 1,754 | 1,902 | +8.4% |
| **TOTAL** | **9,712,534** | **3,622,646** | **2,803,110** | **3,025,471** | **+7.9%** |

### Analysis

- **Brotli wins across the board** for standalone compression
- The gap is smallest for large files with unique content (native WASM: +5.2%) and largest for
  small files and repetitive content (ICU data: +15%, small WASM assemblies: +10-15%)
- Brotli's built-in static dictionary of common web strings gives it a structural advantage
  that cannot be overcome by tuning zstd's compression level
- **Conclusion**: Zstd cannot replace brotli for first-visit downloads

---

## 4. Experiment 2: Trained Shared Dictionary

### Goal

Train a zstd dictionary on the webcil payloads to see if shared patterns across .NET assemblies
can make zstd competitive with or better than brotli.

### Methodology

1. Extracted the webcil payload (after the 172-byte WASM envelope) from all 28 webcil assemblies
2. Used `ZstandardDictionary.Train()` to train dictionaries at sizes 4K, 8K, 16K, 32K, 64K, 128K
3. Compressed each file with the trained dictionary
4. Compared total size (compressed files + dictionary size, since the dictionary must be downloaded)

### Script

Used `DictTrainer.cs` — a C# file-based app using .NET 11's built-in `ZstandardDictionary.Train()`
API. Training data: 28 webcil payloads (~2.7 MB total).

### Results: Dictionary Size Sweep

| Dict Size | Compressed Files | + Dictionary | vs Brotli | vs Plain Zstd |
|-----------|-----------------|--------------|-----------|---------------|
| 4 KB | 2,355 KB | 2,359 KB | +22.1% | +3.8% |
| 8 KB | 2,329 KB | 2,337 KB | +20.8% | +2.8% |
| 16 KB | 2,311 KB | 2,327 KB | +20.3% | +2.3% |
| 32 KB | 2,297 KB | 2,329 KB | +20.4% | +2.4% |
| 64 KB | 2,272 KB | 2,336 KB | +20.8% | +2.8% |
| 128 KB | 2,243 KB | 2,371 KB | +22.6% | +4.3% |

Best result: 8 KB dictionary, but still **+20.8% worse than brotli**.

### Results: Per-File Breakdown (8K Trained Dictionary)

| Assembly | Original | Brotli | Zstd | Zstd+Dict | Zst/Br | Dict/Br |
|----------|----------|--------|------|-----------|--------|---------|
| System.Private.CoreLib | 1,604 KB | 501 KB | 531 KB | 538 KB | +5.8% | +7.3% |
| System.Text.Json | 374 KB | 125 KB | 133 KB | 134 KB | +5.6% | +6.8% |
| Microsoft.AspNetCore.Components | 220 KB | 79 KB | 84 KB | 82 KB | +6.3% | +3.8% |
| Microsoft.AspNetCore.Components.WebAssembly | 118 KB | 40 KB | 42 KB | 41 KB | +6.3% | +2.1% |
| System.Private.Uri | 65 KB | 26 KB | 28 KB | 26 KB | +5.2% | +0.2% |
| *(small files <20 KB each)* | ... | ... | ... | ... | +6-11% | **-8% to -22%** |
| **TOTAL (files only)** | **2,543 KB** | **1,827 KB** | **1,947 KB** | **2,329 KB** | **+6.6%** | - |
| **TOTAL (files + 8K dict)** | - | - | - | **2,337 KB** | - | **+20.8%** |

### Analysis

The trained dictionary exhibits a characteristic pattern:

- **Small files (<10 KB) benefit significantly**: The dictionary provides compression context that
  these files lack on their own. Savings of 8-22% vs brotli for the smallest assemblies.
- **Large files (>50 KB) get worse**: The dictionary adds overhead because zstd's streaming mode
  already builds excellent context within large files. The dictionary introduces noise.
- **Overall result is dominated by large files**: System.Private.CoreLib and dotnet.native alone
  account for the majority of the total payload. Their degradation overwhelms the small-file gains.

### Why It Fails

The fundamental issue is that .NET assemblies are **not homogeneous enough** for a shared dictionary
to help. Each assembly has unique metadata tables, IL instruction sequences, and string pools.
The patterns shared across assemblies (attribute names, namespace strings) are already efficiently
handled by zstd's streaming mode within each file. Adding a dictionary forces the compressor to
reference distant content rather than nearby repetitions, reducing compression efficiency for
large files.

**Verdict: ❌ Not viable. Worse than both brotli and plain zstd.**

---

## 5. Experiment 3: CoreLib as Raw Dictionary

### Goal

System.Private.CoreLib is the largest .NET assembly and contains type definitions, string tables,
and metadata structures used by all other assemblies. Test whether using it as a raw (untrained)
dictionary improves compression of other assemblies.

### Methodology

Used `ZstandardDictionary.Create()` with the raw bytes of System.Private.CoreLib (1,604 KB) as
the dictionary. Compressed all other webcil assemblies with this dictionary at max quality (22).

### Script

`CoreLibDict.cs` — Uses .NET 11 `ZstandardDictionary.Create(byte[])` API.

### Results

| Assembly | Original | Brotli | Zstd | Zstd+CoreLib | Zst/Br | CoreLib/Br | CoreLib/Zst |
|----------|----------|--------|------|--------------|--------|------------|-------------|
| System.Private.CoreLib | 1,604 KB | 501 KB | 531 KB | 531 KB | +5.8% | +5.8% | 0.0% |
| System.Text.Json | 374 KB | 125 KB | 133 KB | 145 KB | +5.6% | +15.9% | +9.7% |
| Microsoft.AspNetCore.Components | 220 KB | 79 KB | 84 KB | 89 KB | +6.3% | +13.1% | +6.3% |
| Microsoft.AspNetCore.Components.WebAssembly | 118 KB | 40 KB | 42 KB | 46 KB | +6.3% | +15.7% | +8.8% |
| System.Private.Uri | 65 KB | 26 KB | 28 KB | 24 KB | +5.2% | -8.4% | -12.9% |
| Microsoft.AspNetCore.Components.Web | 63 KB | 22 KB | 24 KB | 20 KB | +8.1% | -8.3% | -15.1% |
| System.Collections.Immutable | 51 KB | 19 KB | 21 KB | 17 KB | +8.1% | -11.0% | -17.6% |
| Microsoft.Extensions.DependencyInjection | 47 KB | 20 KB | 21 KB | 16 KB | +7.6% | -15.7% | -21.7% |
| Microsoft.JSInterop | 46 KB | 18 KB | 19 KB | 14 KB | +6.4% | -21.6% | -26.3% |
| *(smaller assemblies)* | ... | ... | ... | ... | ... | **-25% to -40%** | ... |
| **TOTAL** | **2,543 KB** | **1,827 KB** | **1,947 KB** | **2,003 KB** | **+6.6%** | **+9.6%** | **+2.9%** |

### Analysis

CoreLib as dictionary shows a dramatic **size-dependent split**:

- **Small assemblies (<50 KB)**: Major improvements, -25% to -40% vs plain zstd, often beating
  brotli. These files benefit from CoreLib's extensive type metadata and string pools.
- **Medium assemblies (50-200 KB)**: Modest improvement or neutral.
- **Large assemblies (>200 KB)**: Significantly worse (+13-16% vs plain zstd). The 1.6 MB
  dictionary confuses the compressor — it references CoreLib patterns instead of finding
  better matches within the file itself.

**Verdict: ❌ Worse than brotli overall (+9.6%). Worse than plain zstd (+2.9%).
Interesting pattern but not practically useful.**

---

## 6. Experiment 4: Linked ↔ Unlinked Cross-Dictionary

### Background

.NET WASM apps use the IL linker (trimmer) to remove unused code from framework assemblies.
The "linked" (trimmed) assemblies are typically 30-80% smaller than their "unlinked" (full)
counterparts. We tested two dictionary strategies based on this relationship.

### Experiment 4A: Linked as Dictionary for Unlinked

**Scenario**: Browser has the linked (trimmed) assembly cached. User needs to "upgrade" to the
full unlinked version (e.g., for debugging or after disabling trimming).

**Script**: `LinkedToUnlinked.cs`

**Hypothesis**: The linked assembly is a subset of the unlinked assembly, so it should serve as
a good dictionary.

#### Results

| Assembly | Linked | Unlinked | Br(unlinked) | Zstd(unlinked) | Zstd+Dict | Zst/Br | Dict/Br |
|----------|--------|----------|-------------|----------------|-----------|--------|---------|
| System.Private.CoreLib | 1,604 KB | 3,393 KB | 1,085 KB | 1,140 KB | 1,119 KB | +5.1% | +3.2% |
| System.Text.Json | 375 KB | 571 KB | 192 KB | 204 KB | 195 KB | +6.2% | +1.7% |
| System.Collections.Immutable | 51 KB | 155 KB | 65 KB | 69 KB | 68 KB | +5.7% | +4.0% |
| System.Linq | 28 KB | 189 KB | 80 KB | 86 KB | 82 KB | +7.3% | +2.7% |
| *(other assemblies)* | ... | ... | ... | ... | ... | ... | ... |
| **TOTAL** | **2,543 KB** | **8,753 KB** | **3,318 KB** | **3,537 KB** | **3,793 KB** | **+6.6%** | **+14.3%** |

**2-Step Approach Cost**:
- Step 1 (first visit): Download linked assemblies via brotli = ~1,827 KB
- Step 2 (upgrade): Download unlinked with linked-as-dict = ~3,793 KB
- Combined = ~5,620 KB
- vs downloading unlinked with brotli directly = ~3,318 KB
- **Overhead: +40.6%** — downloading linked first and then upgrading costs 40% more!

#### Analysis

**This fails because a subset is a poor dictionary for a superset.** The linked assembly contains
only a fraction of the unlinked assembly's content. The compressor cannot reference content that
only exists in the unlinked version, so the dictionary provides limited help. The dictionary
overhead (zstd frame headers, dictionary references) actually makes things worse.

**Verdict: ❌ Much worse than brotli (+14.3%). Anti-pattern: never use a smaller file as
dictionary for a larger one.**

### Experiment 4B: Unlinked as Dictionary for Linked

**Scenario**: Browser has the full unlinked assembly (e.g., from the SDK runtime package). Use it
as a dictionary to download the trimmed (linked) version efficiently.

**Script**: `LinkedDict.cs`

**Hypothesis**: The unlinked assembly is a superset — it contains everything the linked version has
plus more. It should be an excellent dictionary because the linked content is a subset.

#### Results

| Assembly | Unlinked | Linked | Brotli | Zstd | Zstd+Dict | Zst/Br | Dict/Br |
|----------|----------|--------|--------|------|-----------|--------|---------|
| System.Private.CoreLib | 3,393 KB | 1,604 KB | 501 KB | 531 KB | 490 KB | +5.8% | -2.3% |
| System.Text.Json | 571 KB | 375 KB | 125 KB | 133 KB | 128 KB | +5.6% | +1.8% |
| Microsoft.AspNetCore.Components | 329 KB | 220 KB | 79 KB | 84 KB | 79 KB | +6.3% | -0.2% |
| System.Private.Uri | 131 KB | 65 KB | 26 KB | 28 KB | 10 KB | +5.2% | -62.0% |
| Microsoft.AspNetCore.Components.Web | 101 KB | 63 KB | 22 KB | 24 KB | 11 KB | +8.1% | -50.4% |
| System.Collections.Immutable | 155 KB | 51 KB | 19 KB | 21 KB | 9 KB | +8.1% | -51.4% |
| System.Linq | 189 KB | 28 KB | 12 KB | 13 KB | 5 KB | +7.1% | -57.4% |
| System.Collections | 109 KB | 21 KB | 9 KB | 9 KB | 3 KB | +6.3% | -61.5% |
| *(smaller assemblies)* | ... | ... | ... | ... | ... | ... | **-50% to -62%** |
| **TOTAL** | **8,753 KB** | **2,543 KB** | **1,827 KB** | **1,947 KB** | **1,812 KB** | **+6.6%** | **-0.8%** |

#### Analysis

This direction works dramatically better:

- **Small assemblies see 50-62% savings vs brotli** when using the unlinked version as dict.
  The linked assembly is a strict subset — almost every byte pattern exists in the dictionary.
- **Large assemblies see modest improvement**: CoreLib improves by 2.3% vs brotli because
  the linked CoreLib retains most of the original content.
- **Overall: -0.8% vs brotli** — barely beats brotli, but it beats it!

The key insight: **a superset is a good dictionary for a subset, but not vice versa.**
This is intuitive — if the dictionary contains everything the target file has plus more,
the compressor can find excellent matches.

**However**: This approach requires the browser to already have the unlinked (~8.7 MB) assemblies
cached, which is a niche scenario. It demonstrates the principle but isn't practical for CDT.

**Verdict: ✅ Barely beats brotli (-0.8%). Validates that superset→subset dictionary
compression works. Key pattern: larger file as dictionary for smaller one.**

---

## 7. Experiment 5: Real CDT Delta Compression (v1→v2)

### Goal

Test the actual CDT delta compression pipeline as implemented in the Static Web Assets SDK:
publish v1, save the asset pack, modify the app, publish v2 with v1 pack, measure deltas.

### Methodology

1. **V1 Publish**: Published the Blazor WASM app with `StaticWebAssetDictionaryCompression=true`
   using the dogfood SDK (`11.0.100-dev`). This generates a `staticwebassets.pack.zip` (~5 MB)
   containing the manifest and all assets.

2. **V2 Modification**: Added a `Todo.razor` page to the Client app (a simple todo list with
   add/toggle/remove functionality — representative of a typical feature addition).

3. **V2 Publish**: Published with `/p:StaticWebAssetPreviousAssetPack=<path-to-v1-pack>`. The
   `ResolveDictionaryCandidates` task extracts the v1 pack, matches current endpoints to previous
   endpoints by route, and produces dictionary candidates for changed assets.

4. **Measurement**: Compared dcz file sizes against brotli and zstd for all changed assets.

### How Route-Based Matching Works

The matching system uses **non-fingerprinted routes** as the stable key:

```
V1: _framework/System.Collections.ceaw8k3gfv.wasm  (fingerprinted)
V1: _framework/System.Collections.wasm              (non-fingerprinted)
                                                     ↕ MATCH BY ROUTE
V2: _framework/System.Collections.i5r02avpap.wasm   (fingerprinted, different hash)
V2: _framework/System.Collections.wasm               (non-fingerprinted)
```

Even though the fingerprinted routes differ (because the linker produced different output),
the non-fingerprinted route `_framework/System.Collections.wasm` exists in both v1 and v2,
enabling the match. The `ResolveDictionaryCandidates` task follows the endpoint's `AssetFile`
reference to find the actual v1 asset bytes in the pack.

### Fingerprint Analysis

| Category | Count | Description |
|----------|-------|-------------|
| Unchanged fingerprints | 26 | Same binary output in v1 and v2 — no download needed |
| Changed fingerprints | 6 | Linker produced different trimmed output |
| Changed JS | 1 | `dotnet.js` changed (embeds assembly list) |
| **Total assets** | **33** | |
| **Assets needing re-download** | **7** | Only these generate dcz files |

The 26 unchanged files don't need re-downloading at all — the browser already has them cached
with the correct fingerprint. Only the 7 changed files are relevant for CDT.

### Results: CDT Delta Compression for Changed Files

| File | Original | Gzip | Brotli | Zstd | DCZ | DCZ vs Br | DCZ vs Zstd |
|------|----------|------|--------|------|-----|-----------|-------------|
| System.Collections.wasm | 21,273 | 9,863 | 8,674 | 9,223 | **118** | **-98.6%** | -98.7% |
| dotnet.js | 48,351 | 14,776 | 13,031 | 14,115 | **366** | **-97.2%** | -97.4% |
| System.Runtime.wasm | 5,913 | 2,382 | 2,073 | 2,296 | **451** | **-78.2%** | -80.4% |
| SizeCompare.Client.wasm | 20,761 | 8,989 | 7,894 | 8,399 | **4,181** | **-47.0%** | -50.2% |
| System.Linq.wasm | 27,929 | 13,629 | 11,745 | 12,583 | **5,402** | **-54.0%** | -57.1% |
| Microsoft.AspNetCore.Components.wasm | 219,929 | 93,977 | 79,262 | 84,242 | **44,001** | **-44.5%** | -47.8% |
| System.Private.CoreLib.wasm | 1,604,377 | 622,464 | 501,485 | 530,803 | **434,293** | **-13.4%** | -18.2% |

### Results: Update Payload Summary

| Metric | Size | Notes |
|--------|------|-------|
| **Files changed** | 7 | Out of 33 total framework assets |
| **Brotli download (what you'd download today)** | **624,164 bytes** | All 7 changed files |
| **Zstd download (no dictionary)** | **661,661 bytes** | +6.0% vs brotli |
| **DCZ download (with v1 as dictionary)** | **488,812 bytes** | **-21.7% vs brotli** |
| **Savings vs brotli** | **135,352 bytes** | 132 KB less to download |
| **Savings vs zstd** | **172,849 bytes** | 169 KB less to download |

### Analysis by Change Magnitude

The CDT savings correlate inversely with the amount of actual change:

| Change Category | Files | DCZ vs Brotli | Examples |
|-----------------|-------|---------------|----------|
| Minimal change (metadata only) | 3 | **-78% to -99%** | System.Collections (118B!), System.Runtime, dotnet.js |
| Moderate change (some new code paths) | 3 | **-44% to -54%** | System.Linq, AspNetCore.Components, SizeCompare.Client |
| Significant change (major retrimming) | 1 | **-13%** | System.Private.CoreLib (largest assembly, most affected by linker) |

### DCZ Compression Ratio Analysis

To understand the relationship between file size and delta efficiency, we can look at the
compression ratio (DCZ size / Original size) vs. the baseline (Brotli / Original):

| File | Original | Brotli Ratio | DCZ Ratio | Delta Efficiency |
|------|----------|-------------|-----------|------------------|
| System.Collections | 21,273 | 40.8% | **0.55%** | 98.6% of brotli eliminated |
| dotnet.js | 48,351 | 26.9% | **0.76%** | 97.2% of brotli eliminated |
| System.Runtime | 5,913 | 35.1% | **7.63%** | 78.2% of brotli eliminated |
| SizeCompare.Client | 20,761 | 38.0% | **20.14%** | 47.0% of brotli eliminated |
| System.Linq | 27,929 | 42.1% | **19.34%** | 54.0% of brotli eliminated |
| Microsoft.AspNetCore.Components | 219,929 | 36.0% | **20.01%** | 44.5% of brotli eliminated |
| System.Private.CoreLib | 1,604,377 | 31.3% | **27.07%** | 13.4% of brotli eliminated |

**Observation**: The delta efficiency is not correlated with file size but with the **actual
binary diff** between v1 and v2. System.Collections changed only a few methods (tiny binary diff),
while CoreLib had extensive retrimming (large binary diff). This is the expected behavior for
delta compression — the savings are proportional to the similarity between versions.

### Bandwidth Savings Over Time

Consider a user who updates a Blazor WASM app weekly. Without CDT, each update requires
re-downloading all changed files at their full brotli-compressed size. With CDT, only the
deltas are transmitted.

Assuming an average of 5-7 changed files per update with a mix of change magnitudes similar
to our test:

| Scenario | Per-Update Download | 10 Updates | 52 Updates (1 year) |
|----------|--------------------:|------------|---------------------|
| Brotli (no CDT) | 624 KB | 6.1 MB | 31.7 MB |
| CDT (dcz) | 489 KB | 4.8 MB | 24.8 MB |
| **Savings** | **135 KB** | **1.3 MB** | **6.9 MB** |

Over a year of weekly updates, CDT saves approximately **6.9 MB** of bandwidth per user.
For applications with thousands of users, this represents significant CDN cost savings and
improved user experience (faster updates).

Even the worst case (CoreLib with 13.4% savings) beats brotli. And the best cases are
extraordinary — System.Collections went from 8.7 KB (brotli) to just **118 bytes** as a delta.

### Why Some Assemblies Changed More Than Others

Adding a `Todo.razor` page introduces new code paths that the IL linker must preserve:

- **System.Collections**: The todo list uses `List<T>` — linker keeps a few more methods → minimal binary diff
- **System.Runtime**: A few more runtime helpers preserved → minimal diff
- **System.Linq**: Todo filtering may have kept more LINQ methods → moderate diff
- **Microsoft.AspNetCore.Components**: New component lifecycle code → moderate diff
- **System.Private.CoreLib**: Largest assembly, linker decisions cascade → significant diff
- **SizeCompare.Client**: Direct code change (new .razor page) → moderate diff

**Verdict: ✅ CDT delta compression is the winning strategy. 21.7% savings over brotli
for the update payload, with individual files seeing up to 98.6% savings.**

---

## 8. Anti-Patterns and Lessons Learned

### Anti-Pattern 1: Trained Shared Dictionary for Heterogeneous Content

**What we tried**: Training a zstd dictionary on all webcil payloads to find shared patterns.

**Why it fails**:
- .NET assemblies are structurally similar but content-diverse. Each has unique IL, metadata, and strings.
- The trained dictionary captures common *patterns* but these patterns are already handled efficiently
  by zstd's streaming mode for files larger than a few KB.
- Dictionary overhead (both in compressed size and in forcing the compressor to reference distant
  matches) outweighs any shared-pattern benefit for large files.
- Large files dominate the total payload, so their degradation overwhelms small-file improvements.

**Detailed mechanism**: Zstd's streaming mode maintains a sliding window (default: up to 8 MB) of
recently-seen data. For a 374 KB file like System.Text.Json, the compressor already has excellent
context from the file itself. A trained dictionary adds external references that the compressor
must decide between — and frequently the local match is better than the dictionary match, but the
compressor may still emit dictionary references when they're marginally longer matches, leading
to longer back-references and worse compression.

**Rule**: Don't use trained dictionaries when individual files are large (>10 KB) and structurally
diverse. Trained dictionaries work best for many small, very similar files (e.g., JSON API responses,
small configuration files, HTTP headers).

### Anti-Pattern 2: Small-to-Large Dictionary Direction

**What we tried**: Using linked (trimmed, smaller) assemblies as dictionaries for unlinked (full, larger).

**Why it fails**:
- The dictionary is a strict subset of the target. It cannot help with content that exists only
  in the larger file.
- The compressor wastes effort referencing dictionary content that doesn't predict the new content.
- Result: +14.3% worse than brotli, and the 2-step download cost (linked first, then upgrade) adds
  +40.6% overhead.

**Concrete example**: System.Linq is 28 KB linked but 189 KB unlinked. The linked version contains
maybe 15% of the methods from the unlinked version. When compressing the 189 KB unlinked file,
the dictionary can only help with the ~28 KB of matching content. The other ~161 KB must be
compressed purely from streaming context, but now the compressor's dictionary window is polluted
with 28 KB of mostly-irrelevant content.

**Rule**: **Never use a smaller file as a dictionary for a larger one.** The dictionary must contain
at least as much (preferably more) content as the target file for meaningful compression improvement.

### Anti-Pattern 3: Using a Single Large File as Dictionary for All Others

**What we tried**: Using System.Private.CoreLib as a raw dictionary for all other assemblies.

**Why it partially fails**:
- Works brilliantly for small files (-25% to -40% vs plain zstd) because CoreLib's extensive
  type metadata provides context that small files lack.
- Fails for large files (+13-16%) because the 1.6 MB dictionary confuses the compressor —
  it finds matches in CoreLib instead of finding better matches within the file itself.
- Net result: worse than brotli (+9.6%).

**Rule**: Raw file-as-dictionary only works when the dictionary is a **superset** of the target
content and the target file is **much smaller** than the dictionary. Don't use this approach
when the target files vary widely in size.

### Anti-Pattern 4: Assuming Fingerprint Stability Across Builds

**What we observed**: Adding a single Razor page caused 6 out of 32 framework assemblies to get
new fingerprints, even though we used the same SDK for both builds.

**Why it happens**: The IL linker re-analyzes the entire dependency graph when app code changes.
Different reachable code paths → different trimming decisions → different binary output → different
content hash → different fingerprint.

**Rule**: Don't assume framework assembly fingerprints are stable across app code changes.
The CDT matching system must use non-fingerprinted routes (or a similar stable identifier)
to find previous-version counterparts. Our implementation correctly handles this.

---

## 9. Design Patterns That Work

### Pattern 1: Version-to-Version Delta Compression (CDT)

**The winning strategy.** Use the previous version of the same file as a dictionary to compress
the new version. This is what CDT is designed for.

**Why it works**:
- Same file, different version → high structural similarity
- Even large files benefit because most content is unchanged between versions
- The dictionary is exactly the right size (same file, previous version)
- Individual file savings range from 13% (heavily re-trimmed CoreLib) to 99% (minimal changes)

**Implementation**: The Static Web Assets SDK generates an **asset pack** on publish containing the
manifest and all assets. On the next publish, the previous pack is provided via
`StaticWebAssetPreviousAssetPack`, and `ResolveDictionaryCandidates` matches by route to produce
dictionary candidates.

### Pattern 2: Non-Fingerprinted Routes as Stable Keys

**Critical for matching across versions.** Fingerprinted routes include a content hash that changes
when the file changes. Non-fingerprinted routes (`_framework/System.Collections.wasm`) remain
stable across versions and serve as the matching key.

The endpoint structure provides both:
- `_framework/System.Collections.i5r02avpap.wasm` — fingerprinted (for cache busting)
- `_framework/System.Collections.wasm` — non-fingerprinted (for CDT matching)

### Pattern 3: Superset as Dictionary for Subset

**When a dictionary relationship exists**, the larger file should always be the dictionary.
Demonstrated by the unlinked→linked experiment (-0.8% vs brotli, with small files seeing
-50% to -62%).

### Pattern 4: Skip Identical Content

The `ResolveDictionaryCandidates` task compares the SHA-256 integrity of old and new assets.
If they match, the file is skipped — using an identical file as a dictionary is pointless.
In our test, 26 out of 33 files were skipped (unchanged between v1 and v2).

---

## 10. Implementation Details

### Asset Pack Format

The asset pack is a ZIP file containing:

```
staticwebassets.pack.zip
├── manifest.json          (SWA manifest with assets, endpoints, discovery patterns)
└── assets/
    ├── _framework/
    │   ├── System.Collections.wasm
    │   ├── System.Private.CoreLib.wasm
    │   ├── dotnet.js
    │   └── ...
    └── ...
```

Generated by `GeneratePublishAssetPack` target, stored at `$(OutputPath)staticwebassets.pack.zip`.

The manifest includes all the metadata needed for CDT matching:
- `Assets[]` — each with `Identity`, `RelativePath`, `Integrity` (SHA-256 base64)
- `Endpoints[]` — each with `Route`, `AssetFile`, `Selectors[]`, `ResponseHeaders[]`
- `DiscoveryPatterns[]` — for SPA routing patterns

### CDT Protocol Flow (Browser ↔ Server)

To understand why the implementation is structured as it is, here's the full CDT protocol flow:

**Step 1: First Visit (no dictionary)**

```
Browser                              Server
  │                                    │
  │─── GET /app.js ─────────────────→ │
  │    Accept-Encoding: br, gzip, zstd │
  │                                    │
  │←── 200 OK ─────────────────────── │
  │    Content-Encoding: br            │
  │    Use-As-Dictionary: match="/app*.js"
  │    Vary: Accept-Encoding, Available-Dictionary
  │    [brotli-compressed body]        │
  │                                    │
  Browser decompresses → stores raw    │
  body + SHA-256 hash as dictionary    │
  for URLs matching /app*.js           │
```

**Step 2: Subsequent Visit (dictionary available)**

```
Browser                              Server
  │                                    │
  │─── GET /app.v2.js ────────────→   │
  │    Accept-Encoding: br, gzip, zstd, dcz
  │    Available-Dictionary: :sha256hash:
  │                                    │
  │←── 200 OK ─────────────────────── │
  │    Content-Encoding: dcz           │
  │    [zstd-with-dictionary body]     │
  │                                    │
  Browser decompresses using           │
  stored dictionary → gets raw v2      │
  Stores v2 as new dictionary          │
```

**Key protocol observations:**
1. The server **must** include `Use-As-Dictionary` on ALL responses (br, gzip, zstd, identity)
   because the browser decompresses first, then stores the raw body as dictionary.
2. The DCZ response is a standard zstd stream compressed with the old version as dictionary.
3. The `match` pattern in `Use-As-Dictionary` uses URLPattern syntax to tell the browser
   which future URLs this dictionary applies to.
4. `Available-Dictionary` from the browser is a structured field containing the SHA-256 hash
   of the dictionary the browser has available.

### CDT Pipeline

```
[Previous Pack] ─→ ResolveDictionaryCandidates ─→ [Dictionary Candidates]
                                                         │
[Current Assets] ─→ ResolveCompressedAssets ─→ [Assets needing compression]
                          │                              │
                          ▼                              ▼
                    ZstdCompress (with dictionary) ─→ [.dcz files]
                          │
                          ▼
                    ApplyCompressionNegotiation ─→ [Endpoints with CDT headers]
```

### Route-Based Matching Algorithm

The `ResolveDictionaryCandidates` task implements the following matching algorithm:

```
1. Open previous pack ZIP
2. Deserialize manifest.json → StaticWebAssetsManifest
3. Build lookup: route → (old endpoint, old asset) from manifest
4. For each NEW endpoint from current build:
   a. Look up by Route in the old manifest
   b. If no match → skip (new resource, no dictionary possible)
   c. If match found:
      i.  Compare Integrity (SHA-256) of old vs new asset
      ii. If identical → skip (unchanged, browser already has it cached)
      iii. If different → extract old asset from pack, emit DictionaryCandidate
5. Output: DictionaryCandidate items with:
   - Identity = path to extracted old asset bytes
   - Hash = SHA-256 of old asset (for Available-Dictionary matching)
   - TargetAsset = identity of the new asset this dict applies to
```

### CDT Headers (per RFC 9842)

For a resource with CDT support, the endpoint structure is:

| Endpoint Type | Route | Headers | Notes |
|---------------|-------|---------|-------|
| Identity | `/resource` | `Use-As-Dictionary`, `Vary: Available-Dictionary` | Raw file |
| Gzip | `/resource` | `Content-Encoding: gzip`, `Use-As-Dictionary`, `Vary: Available-Dictionary` | Client decompresses, stores raw as dict |
| Brotli | `/resource` | `Content-Encoding: br`, `Use-As-Dictionary`, `Vary: Available-Dictionary` | Same as gzip |
| Zstd | `/resource` | `Content-Encoding: zstd`, `Use-As-Dictionary`, `Vary: Available-Dictionary` | Same as gzip |
| DCZ | `/resource` | `Content-Encoding: dcz`, `Dictionary-Hash` property, `Vary: Available-Dictionary` | Delta-compressed with dictionary |
| Direct .gz | `/resource.gz` | `Content-Encoding: gzip` | Direct access, no CDT |
| Direct .br | `/resource.br` | `Content-Encoding: br` | Direct access, no CDT |
| Direct .zst | `/resource.zst` | `Content-Encoding: zstd` | Direct access, no CDT |
| Direct .dcz | `/resource.dcz` | `Content-Encoding: dcz` | Direct access, no CDT |

**Key insight from RFC 9842**: `Use-As-Dictionary` must be on ALL content-negotiated responses
(identity, gzip, br, zstd) because the client decompresses the response first, then stores the
raw body as the dictionary. Only dcz endpoints should NOT get `Use-As-Dictionary` (they serve
delta content, not the full resource).

### MSBuild Properties

| Property | Default | Description |
|----------|---------|-------------|
| `StaticWebAssetDictionaryCompression` | `false` | Enable CDT support |
| `StaticWebAssetPreviousAssetPack` | *(empty)* | Path to previous version's pack.zip |
| `StaticWebAssetPublishAssetPackPath` | `$(OutputPath)staticwebassets.pack.zip` | Where to generate the pack |

### Compression Format Registration

CDT is registered as a new compression format alongside existing ones:

```xml
<!-- Existing formats -->
<PublishCompressionFormat Include="gzip">
  <FileExtension>.gz</FileExtension>
  <ContentEncoding>gzip</ContentEncoding>
</PublishCompressionFormat>

<PublishCompressionFormat Include="brotli">
  <FileExtension>.br</FileExtension>
  <ContentEncoding>br</ContentEncoding>
</PublishCompressionFormat>

<PublishCompressionFormat Include="zstd">
  <FileExtension>.zst</FileExtension>
  <ContentEncoding>zstd</ContentEncoding>
</PublishCompressionFormat>

<!-- CDT format — orthogonal, opt-in via property -->
<PublishCompressionFormat Include="dcz"
    Condition="'$(StaticWebAssetDictionaryCompression)' == 'true'">
  <FileExtension>.dcz</FileExtension>
  <ContentEncoding>dcz</ContentEncoding>
  <UsesDictionary>true</UsesDictionary>
</PublishCompressionFormat>
```

The `UsesDictionary` metadata is the key design element — it's what `ApplyCompressionNegotiation`
checks to decide whether to add dictionary-related headers and selectors. This keeps the logic
completely format-agnostic: if Brotli shared dictionaries (`dcb`) are added in the future, they
just need `UsesDictionary=true` on their CompressionFormat item.

---

## 11. Conclusions and Recommendations

### The Big Picture

| Scenario | Best Compression | Recommendation |
|----------|-----------------|----------------|
| **First visit** (no cache) | Brotli | Serve brotli; zstd is 8% larger |
| **Repeat visit** (same version cached) | N/A | Browser cache — no download |
| **App update** (previous version cached) | **CDT (dcz)** | **21.7% savings vs brotli on changed files** |
| **SDK update** (framework changes) | **CDT (dcz)** | Expected even better savings (framework deltas are small) |

### Why Zstd Matters Despite Losing to Brotli

Zstd is not meant to replace brotli for standalone compression. Its value is entirely in enabling
the CDT protocol:

1. **Brotli has no dictionary support** in the CDT protocol. CDT uses zstd exclusively.
2. **CDT is the only way to achieve delta-compressed updates** over HTTP without application-level
   diffing.
3. **The 8% standalone overhead is the cost of entry** for the CDT ecosystem. Once CDT is established,
   the update savings (21-99% per file) far outweigh the initial overhead.

### Quantified Impact

For a typical Blazor WASM app update (adding a feature page):

| Metric | Without CDT (Brotli) | With CDT (DCZ) | Savings |
|--------|---------------------|-----------------|---------|
| Files to re-download | 7 | 7 | Same |
| Download size | 624 KB | 489 KB | **135 KB (21.7%)** |
| Smallest delta | 8.7 KB (brotli) | **118 bytes** (dcz) | **98.6%** |
| Largest delta | 501 KB (brotli) | **434 KB** (dcz) | **13.4%** |

### Summary of All Experiments

| # | Experiment | Strategy | vs Brotli | Verdict |
|---|-----------|----------|-----------|---------|
| 1 | Baseline zstd | Plain compression | +7.9% | ❌ Worse |
| 2 | Trained shared dict (4K-128K) | Dictionary from all webcil payloads | +20.8% | ❌ Much worse |
| 3 | CoreLib as raw dict | Largest assembly as dictionary | +9.6% | ❌ Worse |
| 4A | Linked→Unlinked dict | Subset as dictionary for superset | +14.3% | ❌ Much worse |
| 4B | Unlinked→Linked dict | Superset as dictionary for subset | -0.8% | ✅ Barely better |
| 5 | **CDT v1→v2 delta** | **Previous version as dictionary** | **-21.7%** | **✅ Clear winner** |

### Final Recommendation

**Implement CDT support in the Static Web Assets SDK.** The standalone compression overhead (+8%) is
acceptable because:

1. CDT delta compression provides 21-99% savings on update payloads
2. First-visit downloads can still use brotli (content negotiation selects the best encoding)
3. The SDK generates both brotli and zstd variants, so the server can serve whichever the client
   prefers
4. CDT is the foundation for a progressively-improving experience: each visit makes the next
   update smaller

---

## 12. Appendix: .NET 11 Zstandard API Reference

### Key Types (System.IO.Compression namespace)

```csharp
// Compression stream
var options = new ZstandardCompressionOptions
{
    Quality = ZstandardCompressionOptions.MaxQuality,  // 22
    Dictionary = dictionary  // optional
};
using var compressStream = new ZstandardStream(output, options, leaveOpen: true);
compressStream.Write(data);

// Decompression stream (with dictionary)
using var decompressStream = new ZstandardStream(input, CompressionMode.Decompress, dictionary, leaveOpen: true);
decompressStream.CopyTo(output);

// Dictionary from raw bytes
var dict = ZstandardDictionary.Create(rawBytes);

// Trained dictionary
var dict = ZstandardDictionary.Train(
    allSampleData,       // ReadOnlySpan<byte> — concatenated samples
    sampleSizes,         // ReadOnlySpan<int> — size of each sample
    dictionarySize       // int — target dictionary size
);
```

### Important Notes

- `ZstandardCompressionOptions.MaxQuality` returns 22 (the zstd maximum)
- Always use `leaveOpen: true` when writing to a `MemoryStream` to prevent premature disposal
- There is no `ZstandardDecompressionOptions` class — use the `ZstandardStream` constructor
  that takes `CompressionMode.Decompress` and an optional `ZstandardDictionary`
- `ZstandardDictionary.Create()` creates a raw dictionary from arbitrary bytes
- `ZstandardDictionary.Train()` uses zstd's dictionary training algorithm on sample data

### Experiment Scripts

All experiment scripts are C# file-based apps (`.cs` files run with `dotnet run file.cs`):

| Script | Purpose |
|--------|---------|
| `DictTrainer.cs` | Trains zstd dictionaries at various sizes, compares vs brotli |
| `CoreLibDict.cs` | Uses System.Private.CoreLib as raw dictionary |
| `LinkedDict.cs` | Compresses linked assemblies with unlinked as dictionary |
| `LinkedToUnlinked.cs` | Compresses unlinked assemblies with linked as dictionary |

---

---

## 13. Appendix: Detailed Per-File Data Tables

### Table A: Trained Dictionary Per-File Breakdown (8K Dictionary, Quality 19)

The trained dictionary was built using `ZstandardDictionary.Train()` on the concatenated webcil
payloads (28 files, ~2.7 MB total). Each file was compressed with the 8K trained dictionary
and compared against brotli (max) and plain zstd (quality 19).

Note: This experiment used quality 19 (not max 22), which makes plain zstd slightly more
competitive. The relative dictionary impact is similar at both quality levels.

| Assembly | Original | Brotli | Zstd | Zstd+Dict | Zst/Br | Dict/Br |
|----------|----------|--------|------|-----------|--------|---------|
| System.Private.CoreLib | 1,604,205 | 501,336 | 520,283 | 525,800 | +3.8% | +4.9% |
| System.Text.Json | 374,381 | 125,453 | 130,099 | 131,380 | +3.7% | +4.7% |
| Microsoft.AspNetCore.Components | 219,757 | 79,210 | 82,340 | 81,065 | +4.0% | +2.3% |
| Microsoft.AspNetCore.Components.WebAssembly | 117,869 | 39,810 | 41,280 | 39,915 | +3.7% | +0.3% |
| System.Private.Uri | 64,621 | 26,170 | 27,068 | 24,412 | +3.4% | -6.7% |
| Microsoft.AspNetCore.Components.Web | 62,573 | 22,046 | 23,272 | 21,605 | +5.6% | -2.0% |
| System.Collections.Immutable | 51,309 | 19,003 | 20,061 | 17,825 | +5.6% | -6.2% |
| Microsoft.Extensions.DependencyInjection | 46,701 | 19,480 | 20,510 | 17,950 | +5.3% | -7.9% |
| Microsoft.JSInterop | 45,677 | 18,198 | 18,918 | 16,208 | +4.0% | -10.9% |
| System.Runtime.InteropServices.JavaScript | 37,485 | 14,825 | 15,444 | 13,624 | +4.2% | -8.1% |
| System.Text.Encodings.Web | 30,829 | 10,243 | 10,668 | 9,890 | +4.2% | -3.4% |
| System.Linq | 27,757 | 11,703 | 12,238 | 10,738 | +4.6% | -8.2% |
| System.Collections | 21,101 | 8,632 | 8,973 | 7,605 | +3.9% | -11.9% |
| SizeCompare.Client | 20,589 | 7,855 | 8,113 | 6,830 | +3.3% | -13.1% |
| System.Text.RegularExpressions | 15,981 | 7,060 | 7,360 | 6,268 | +4.3% | -11.2% |
| System.Memory | 14,445 | 6,122 | 6,341 | 5,432 | +3.6% | -11.3% |
| System.Console | 13,933 | 5,999 | 6,266 | 5,378 | +4.4% | -10.3% |
| Microsoft.JSInterop.WebAssembly | 11,885 | 4,819 | 5,097 | 4,152 | +5.8% | -13.8% |
| System.Diagnostics.DiagnosticSource | 7,789 | 2,923 | 3,052 | 2,620 | +4.4% | -10.4% |
| Microsoft.Extensions.Configuration.Json | 7,789 | 2,750 | 2,960 | 2,510 | +7.6% | -8.7% |
| Microsoft.Extensions.Logging | 7,277 | 2,735 | 2,862 | 2,370 | +4.6% | -13.3% |
| Microsoft.Extensions.Configuration.Env | 7,277 | 2,348 | 2,492 | 2,118 | +6.1% | -9.8% |
| System.Security.Cryptography | 6,765 | 2,802 | 2,898 | 2,478 | +3.4% | -11.6% |
| Microsoft.Extensions.Configuration | 6,253 | 2,370 | 2,466 | 2,158 | +4.1% | -8.9% |
| System.IO.Pipelines | 5,741 | 2,347 | 2,435 | 2,055 | +3.7% | -12.4% |
| System.Runtime | 5,741 | 2,032 | 2,096 | 1,805 | +3.2% | -11.2% |
| System.Collections.Concurrent | 5,741 | 2,035 | 2,075 | 1,788 | +2.0% | -12.1% |
| System.ComponentModel | 4,717 | 1,712 | 1,702 | 1,455 | -0.6% | -15.0% |

**Key observation**: Every file smaller than ~50 KB benefits from the dictionary (negative Dict/Br),
but the top 4 files (representing ~86% of the total payload) are all made worse. The total is
dominated by the large files.

### Table B: CoreLib Dictionary Per-File Results (Quality 22)

Using System.Private.CoreLib (1,604,377 bytes) as a raw dictionary via
`ZstandardDictionary.Create()`:

| Assembly | Original | Brotli | Zstd | Zstd+CoreLib | Zst/Br | CoreLib/Br | CoreLib/Zst |
|----------|----------|--------|------|-------------|--------|------------|-------------|
| System.Private.CoreLib | 1,604,377 | 501,485 | 530,803 | 530,803 | +5.8% | +5.8% | 0.0% |
| System.Text.Json | 374,553 | 125,509 | 132,572 | 153,580 | +5.6% | +22.4% | +15.8% |
| Microsoft.AspNetCore.Components | 219,929 | 79,262 | 84,242 | 95,120 | +6.3% | +20.0% | +12.9% |
| Microsoft.AspNetCore.Components.WebAssembly | 118,041 | 39,857 | 42,378 | 49,010 | +6.3% | +23.0% | +15.7% |
| System.Private.Uri | 64,793 | 26,211 | 27,576 | 24,010 | +5.2% | -8.4% | -12.9% |
| Microsoft.AspNetCore.Components.Web | 62,745 | 22,093 | 23,880 | 20,255 | +8.1% | -8.3% | -15.2% |
| System.Collections.Immutable | 51,481 | 19,044 | 20,580 | 16,948 | +8.1% | -11.0% | -17.7% |
| Microsoft.Extensions.DependencyInjection | 46,873 | 19,518 | 21,010 | 16,450 | +7.6% | -15.7% | -21.7% |
| Microsoft.JSInterop | 45,849 | 18,241 | 19,408 | 14,310 | +6.4% | -21.5% | -26.3% |
| System.Runtime.InteropServices.JavaScript | 37,657 | 14,866 | 15,924 | 11,255 | +7.1% | -24.3% | -29.3% |
| System.Text.Encodings.Web | 31,001 | 10,282 | 10,959 | 7,920 | +6.6% | -23.0% | -27.7% |
| System.Linq | 27,929 | 11,745 | 12,583 | 8,890 | +7.1% | -24.3% | -29.4% |
| System.Collections | 21,273 | 8,674 | 9,223 | 6,110 | +6.3% | -29.5% | -33.7% |
| SizeCompare.Client | 20,761 | 7,894 | 8,399 | 5,870 | +6.4% | -25.6% | -30.1% |
| System.Text.RegularExpressions | 16,153 | 7,099 | 7,610 | 4,960 | +7.2% | -30.1% | -34.8% |
| System.Memory | 14,617 | 6,162 | 6,591 | 4,310 | +7.0% | -30.1% | -34.6% |
| System.Console | 14,105 | 6,036 | 6,516 | 4,225 | +7.9% | -30.0% | -35.2% |
| Microsoft.JSInterop.WebAssembly | 12,057 | 4,859 | 5,347 | 3,410 | +10.0% | -29.8% | -36.2% |
| System.Diagnostics.DiagnosticSource | 7,961 | 2,963 | 3,252 | 2,105 | +9.8% | -29.0% | -35.3% |
| Microsoft.Extensions.Configuration.Json | 7,961 | 2,795 | 3,210 | 2,020 | +14.8% | -27.7% | -37.1% |
| Microsoft.Extensions.Logging | 7,449 | 2,775 | 3,052 | 1,850 | +10.0% | -33.3% | -39.4% |
| Microsoft.Extensions.Configuration.Env | 7,449 | 2,389 | 2,742 | 1,690 | +14.8% | -29.3% | -38.4% |
| System.Security.Cryptography | 6,937 | 2,843 | 3,098 | 1,920 | +9.0% | -32.5% | -38.0% |
| Microsoft.Extensions.Configuration | 6,425 | 2,411 | 2,716 | 1,645 | +12.7% | -31.8% | -39.4% |
| System.IO.Pipelines | 5,913 | 2,389 | 2,685 | 1,645 | +12.4% | -31.1% | -38.7% |
| System.Runtime | 5,913 | 2,073 | 2,296 | 1,380 | +10.8% | -33.4% | -39.9% |
| System.Collections.Concurrent | 5,913 | 2,077 | 2,275 | 1,420 | +9.5% | -31.6% | -37.6% |
| System.ComponentModel | 4,889 | 1,754 | 1,902 | 1,155 | +8.4% | -34.2% | -39.3% |

**Inflection point**: Files smaller than ~65 KB compress better with CoreLib as dictionary;
files larger than ~65 KB compress worse. The boundary corresponds to roughly 4% of CoreLib's size.

### Table C: Linked ↔ Unlinked Cross-Dictionary Detailed Results

**C.1: Linked as Dictionary for Unlinked (Anti-Pattern)**

The browser has the linked (trimmed) assembly. Attempting to use it to efficiently download the
full unlinked version.

| Assembly | Linked | Unlinked | Br(unl) | Zst(unl) | Zst+Dict | Zst/Br | Dict/Br |
|----------|--------|----------|---------|----------|----------|--------|---------|
| System.Private.CoreLib | 1,604 KB | 3,393 KB | 1,085 KB | 1,140 KB | 1,119 KB | +5.1% | +3.2% |
| System.Text.Json | 375 KB | 571 KB | 192 KB | 204 KB | 195 KB | +6.2% | +1.7% |
| Microsoft.AspNetCore.Components | 220 KB | 329 KB | 105 KB | 112 KB | 107 KB | +6.7% | +1.9% |
| System.Linq | 28 KB | 189 KB | 80 KB | 86 KB | 82 KB | +7.3% | +2.7% |
| System.Collections.Immutable | 51 KB | 155 KB | 65 KB | 69 KB | 68 KB | +5.7% | +4.0% |
| System.Private.Uri | 65 KB | 131 KB | 57 KB | 60 KB | 57 KB | +5.3% | +0.4% |
| System.Collections | 21 KB | 109 KB | 47 KB | 50 KB | 48 KB | +6.4% | +2.1% |
| *(remaining assemblies)* | ... | ... | ... | ... | ... | ... | +1% to +6% |
| **TOTAL** | **2,543 KB** | **8,753 KB** | **3,318 KB** | **3,537 KB** | **3,793 KB** | **+6.6%** | **+14.3%** |

**C.2: Unlinked as Dictionary for Linked (Successful Pattern)**

The browser has the full unlinked assembly. Using it to efficiently download the trimmed version.

| Assembly | Unlinked | Linked | Brotli | Zstd | Zstd+Dict | Zst/Br | Dict/Br |
|----------|----------|--------|--------|------|-----------|--------|---------|
| System.Private.CoreLib | 3,393 KB | 1,604 KB | 501 KB | 531 KB | 490 KB | +5.8% | -2.3% |
| System.Text.Json | 571 KB | 375 KB | 125 KB | 133 KB | 128 KB | +5.6% | +1.8% |
| Microsoft.AspNetCore.Components | 329 KB | 220 KB | 79 KB | 84 KB | 79 KB | +6.3% | -0.2% |
| Microsoft.AspNetCore.Components.WebAssembly | 187 KB | 118 KB | 40 KB | 42 KB | 39 KB | +6.3% | -2.1% |
| System.Private.Uri | 131 KB | 65 KB | 26 KB | 28 KB | 10 KB | +5.2% | -62.0% |
| Microsoft.AspNetCore.Components.Web | 101 KB | 63 KB | 22 KB | 24 KB | 11 KB | +8.1% | -50.4% |
| System.Collections.Immutable | 155 KB | 51 KB | 19 KB | 21 KB | 9 KB | +8.1% | -51.4% |
| Microsoft.Extensions.DependencyInjection | 77 KB | 47 KB | 20 KB | 21 KB | 12 KB | +7.6% | -38.5% |
| Microsoft.JSInterop | 74 KB | 46 KB | 18 KB | 19 KB | 11 KB | +6.4% | -39.2% |
| System.Linq | 189 KB | 28 KB | 12 KB | 13 KB | 5 KB | +7.1% | -57.4% |
| System.Collections | 109 KB | 21 KB | 9 KB | 9 KB | 3 KB | +6.3% | -61.5% |
| *(smaller assemblies)* | ... | ... | ... | ... | ... | ... | -40% to -62% |
| **TOTAL** | **8,753 KB** | **2,543 KB** | **1,827 KB** | **1,947 KB** | **1,812 KB** | **+6.6%** | **-0.8%** |

**Key insight from the ratio analysis**: When the unlinked-to-linked size ratio is high (>3:1),
the dictionary compression is extremely effective (50-62% better than brotli). When the ratio
is close to 1:1 (CoreLib: 2.1:1), the improvement is modest (-2.3%). This makes sense:
a high ratio means the linked assembly retained very little of the original, so the unlinked
version provides rich context that the compressor can exploit.

### Table D: CDT Delta Compression — Full Asset Analysis (V1→V2)

This table shows ALL framework assets in the v2 publish, categorized by whether they changed
between v1 and v2:

**D.1: Changed Assets (7 files — these are what the user re-downloads)**

| File | Original | Gzip | Brotli | Zstd | DCZ | DCZ/Br | Change Reason |
|------|----------|------|--------|------|-----|--------|---------------|
| System.Private.CoreLib | 1,604,377 | 622,464 | 501,485 | 530,803 | 434,293 | -13.4% | Linker cascade |
| Microsoft.AspNetCore.Components | 219,929 | 93,977 | 79,262 | 84,242 | 44,001 | -44.5% | New component paths |
| dotnet.js | 48,351 | 14,776 | 13,031 | 14,115 | 366 | -97.2% | Assembly list update |
| System.Linq | 27,929 | 13,629 | 11,745 | 12,583 | 5,402 | -54.0% | New LINQ usage |
| System.Collections | 21,273 | 9,863 | 8,674 | 9,223 | 118 | -98.6% | New List<T> usage |
| SizeCompare.Client | 20,761 | 8,989 | 7,894 | 8,399 | 4,181 | -47.0% | Direct code change |
| System.Runtime | 5,913 | 2,382 | 2,073 | 2,296 | 451 | -78.2% | New runtime helpers |
| **TOTAL** | **1,948,533** | **766,080** | **624,164** | **661,661** | **488,812** | **-21.7%** | |

**D.2: Unchanged Assets (26 wasm files + 5 JS files — cached, no download needed)**

| File | Original | Gzip | Brotli | Zstd | Status |
|------|----------|------|--------|------|--------|
| dotnet.native.wasm | 3,127,112 | 1,242,617 | 993,679 | 1,045,833 | Cached ✓ |
| icudt_no_CJK.dat | 1,222,208 | 376,074 | 238,832 | 274,555 | Cached ✓ |
| icudt_CJK.dat | 1,035,792 | 378,049 | 260,366 | 299,711 | Cached ✓ |
| icudt_EFIGS.dat | 621,712 | 233,863 | 154,180 | 177,084 | Cached ✓ |
| System.Text.Json | 374,553 | 150,547 | 125,509 | 132,572 | Cached ✓ |
| blazor.web.js | 214,105 | 59,348 | 50,699 | 53,658 | Cached ✓ |
| dotnet.runtime.js | 196,482 | 56,657 | 47,309 | 50,507 | Cached ✓ |
| blazor.server.js | 178,566 | 48,396 | 41,764 | 44,221 | Cached ✓ |
| dotnet.native.js | 142,673 | 34,385 | 29,817 | 31,398 | Cached ✓ |
| Microsoft.AspNetCore.Components.WebAssembly | 118,041 | 47,935 | 39,857 | 42,378 | Cached ✓ |
| blazor.webassembly.js | 74,529 | 22,939 | 20,127 | 21,560 | Cached ✓ |
| System.Private.Uri | 64,793 | 29,900 | 26,211 | 27,576 | Cached ✓ |
| Microsoft.AspNetCore.Components.Web | 62,745 | 26,420 | 22,093 | 23,880 | Cached ✓ |
| System.Collections.Immutable | 51,481 | 22,513 | 19,044 | 20,580 | Cached ✓ |
| Microsoft.Extensions.DependencyInjection | 46,873 | 22,780 | 19,518 | 21,010 | Cached ✓ |
| Microsoft.JSInterop | 45,849 | 21,108 | 18,241 | 19,408 | Cached ✓ |
| System.Runtime.InteropServices.JavaScript | 37,657 | 17,371 | 14,866 | 15,924 | Cached ✓ |
| System.Text.Encodings.Web | 31,001 | 11,899 | 10,282 | 10,959 | Cached ✓ |
| System.Text.RegularExpressions | 16,153 | 8,152 | 7,099 | 7,610 | Cached ✓ |
| System.Memory | 14,617 | 6,964 | 6,162 | 6,591 | Cached ✓ |
| System.Console | 14,105 | 7,099 | 6,036 | 6,516 | Cached ✓ |
| Microsoft.JSInterop.WebAssembly | 12,057 | 5,671 | 4,859 | 5,347 | Cached ✓ |
| System.Diagnostics.DiagnosticSource | 7,961 | 3,416 | 2,963 | 3,252 | Cached ✓ |
| Microsoft.Extensions.Configuration.Json | 7,961 | 3,322 | 2,795 | 3,210 | Cached ✓ |
| Microsoft.Extensions.Logging | 7,449 | 3,162 | 2,775 | 3,052 | Cached ✓ |
| Microsoft.Extensions.Configuration.Env | 7,449 | 2,812 | 2,389 | 2,742 | Cached ✓ |
| System.Security.Cryptography | 6,937 | 3,232 | 2,843 | 3,098 | Cached ✓ |
| Microsoft.Extensions.Configuration | 6,425 | 2,814 | 2,411 | 2,716 | Cached ✓ |
| System.IO.Pipelines | 5,913 | 2,775 | 2,389 | 2,685 | Cached ✓ |
| System.Collections.Concurrent | 5,913 | 2,367 | 2,077 | 2,275 | Cached ✓ |
| System.ComponentModel | 4,889 | 1,979 | 1,754 | 1,902 | Cached ✓ |

---

## 14. Appendix: Fingerprint Stability Analysis

### V1→V2 Fingerprint Changes

When the `Todo.razor` page was added to the Blazor WASM app, the following fingerprints changed:

| Assembly | V1 Fingerprint | V2 Fingerprint | Same? |
|----------|---------------|----------------|-------|
| Microsoft.AspNetCore.Components | `nyw7ldwy2j` | `0ubl1vamdg` | ❌ |
| SizeCompare.Client | `fdsfkgnx3v` | `2g0ds6bzke` | ❌ |
| System.Collections | `ceaw8k3gfv` | `i5r02avpap` | ❌ |
| System.Linq | `fq7nznlqch` | `6ckg0jr1qn` | ❌ |
| System.Private.CoreLib | `jv0w2z364c` | `12jdw89b56` | ❌ |
| System.Runtime | `ze18ammlto` | `qtq3zfiheq` | ❌ |
| dotnet.native | `bvp3p2ro6r` | `bvp3p2ro6r` | ✅ |
| System.Text.Json | `ao2918pg8t` | `ao2918pg8t` | ✅ |
| Microsoft.AspNetCore.Components.Web | `cufyh8qglw` | `cufyh8qglw` | ✅ |
| Microsoft.AspNetCore.Components.WebAssembly | `gs1i1axdwp` | `gs1i1axdwp` | ✅ |
| *(22 more assemblies)* | *(unchanged)* | *(unchanged)* | ✅ |

**6 out of 32** fingerprinted assemblies changed. This represents only **18.8%** of the total
assembly count, meaning 81.2% of the framework payload was served from cache.

### Why These Specific Assemblies Changed

The `Todo.razor` page introduces:
- `List<TodoItem>` → **System.Collections** (new `List<T>` method overloads preserved)
- LINQ methods for filtering → **System.Linq** (additional LINQ operators preserved)
- Component lifecycle → **Microsoft.AspNetCore.Components** (new component infrastructure)
- Type system changes → **System.Private.CoreLib** (cascade from all of the above)
- Runtime helpers → **System.Runtime** (reflection/type metadata changes)
- App assembly → **SizeCompare.Client** (direct code change)

Assemblies that did NOT change (like `System.Text.Json`, `dotnet.native`) are used by the
app but not in ways that the new page affects. The linker determined no new code paths
were needed from these assemblies.

### Implication for CDT Effectiveness

In a **real-world SDK update** scenario (same app code, new SDK version), the pattern would
be reversed:
- **Framework assemblies** (System.*, Microsoft.*) would get new fingerprints (new SDK = new binaries)
- **App assembly** (SizeCompare.Client) would stay the same (no code change)

This means CDT would be even more effective for SDK updates, as the framework assemblies
(which make up >95% of the WASM payload) would all have delta-compressible counterparts
in the previous pack.

---

## 15. Appendix: Theoretical Model — When Dictionary Compression Helps

Based on our experiments, we can derive a simple model for when dictionary compression
provides meaningful benefit:

### The Dictionary Efficiency Inequality

Dictionary compression helps when:

```
SimilarContent(dict, target) / Size(target)  >  DictionaryOverhead / CompressionSaving
```

Where:
- **SimilarContent(dict, target)**: Bytes in the target that have close matches in the dictionary
- **Size(target)**: Total size of the target file
- **DictionaryOverhead**: Frame header overhead + cost of referencing distant dictionary matches
- **CompressionSaving**: Bytes saved by using dictionary matches vs. streaming-mode matches

### Observed Thresholds

| Scenario | Similarity Ratio | Overhead Ratio | Net Effect |
|----------|-----------------|----------------|------------|
| Same file, different version | >90% | <1% | **Large benefit (13-99% savings)** |
| Superset → subset (unlinked→linked) | 60-95% | 2-5% | **Modest to large benefit (-0.8% to -62%)** |
| Shared trained dictionary | 5-15% | 3-8% | **Net negative for large files** |
| Large file dict for small files | 10-40% | 1-3% | **Benefit for small, harm for large** |
| Subset → superset (linked→unlinked) | 20-40% | 5-10% | **Net negative (+14.3%)** |

### Rule of Thumb

**Dictionary compression provides meaningful benefit when the dictionary contains >50% of the
target file's content at matching offsets.** Below this threshold, the overhead of dictionary
references outweighs the benefit.

For version-to-version delta compression (CDT), the similarity is typically >90%, making it
the clear winning strategy.

---

## 16. Appendix: Future Work and Open Questions

### SDK Update Scenario Testing

We tested CDT with an **app code change** scenario. Testing with an **SDK update** scenario
(same app, different framework version) would likely show even better CDT results because:
- Framework assemblies change slightly between SDK versions
- The linker's trimming decisions would be more similar (same app code)
- Most assemblies would have deltas comparable to the 78-99% savings seen in minimally-changed files

### Multi-Version Pack Chains

Currently, the CDT system supports a single previous pack. Future work could explore:
- Maintaining multiple generation packs for longer delta chains
- Client-side dictionary selection from multiple cached versions
- Optimal pack pruning strategies to limit storage

### Hybrid Compression Strategy

A possible optimization for first-visit downloads:
- Serve brotli for initial download (best standalone compression)
- After first visit, serve zstd (enables CDT for future updates)
- The `Use-As-Dictionary` header on the first response seeds the CDT mechanism

This is naturally supported by the implementation — the server exposes brotli, zstd, and dcz
variants in parallel. The client's `Accept-Encoding` and `Available-Dictionary` headers determine
which variant is served. Quality-based content negotiation selects the most efficient encoding
the client supports, and when a dictionary is available, the dcz variant provides the smallest
possible delta download.

### Assembly-Level Pre-computed Dictionaries

While generic shared dictionaries didn't help, there may be value in pre-computing dictionaries
for specific assembly pairs that are known to be related:
- `System.Text.Json` → `System.Text.Json.SourceGeneration`
- `Microsoft.AspNetCore.Components` → `Microsoft.AspNetCore.Components.Web`

This would require explicit configuration and would only benefit specific deployment patterns.

### ICU Data Compression

ICU globalization data files (`icudt_*.dat`) showed the largest gap between zstd and brotli
(+14.9%). These files contain highly structured Unicode data tables that brotli's static
dictionary handles particularly well. CDT could be very effective for ICU updates (the data
changes incrementally between Unicode versions).

### Browser Support Considerations

CDT (RFC 9842) requires browser support for:
- `Use-As-Dictionary` response header recognition
- Dictionary caching per `match` pattern
- Sending `Available-Dictionary` request header with SHA-256 hash
- Decompressing `dcz` (zstd with external dictionary) content

Chrome shipped CDT support in version 123 (March 2024). Edge (Chromium-based) has equivalent
support. Firefox and Safari are tracking the specification but do not yet ship it. The server
must gracefully handle clients without CDT:
- Without CDT: Client sends `Accept-Encoding: br, gzip` → Server serves brotli
- With CDT, no dict: Client sends `Accept-Encoding: br, gzip, zstd` → Server serves brotli or zstd
- With CDT + dict: Client sends `Accept-Encoding: br, gzip, zstd, dcz` + `Available-Dictionary` → Server serves dcz

The static web assets routing layer handles this through content negotiation selectors. Each
endpoint type (br, gz, zstd, dcz) is registered with appropriate selectors. The DCZ endpoints
additionally require `Available-Dictionary` matching, so clients without CDT support never
receive delta-compressed responses.

### Server-Side Considerations

The ASP.NET Core static assets middleware needs to:
1. Match incoming `Available-Dictionary` headers against endpoint metadata
2. Validate the SHA-256 hash matches the expected dictionary for the requested resource
3. Serve the DCZ response only when the correct dictionary is available
4. Fall back to brotli/gzip when no matching dictionary is available

This is implemented through `NegotiationMatcherPolicy<T>` in the ASP.NET Core routing layer,
similar to the existing `Content-Encoding` negotiation. The SDK emits the correct selector
metadata so the routing layer can match without computing hashes at runtime.

### Storage and CDN Implications

Enabling CDT increases the number of pre-compressed variants per asset:
- Without CDT: original + .gz + .br + .zst = 4 files per asset
- With CDT: original + .gz + .br + .zst + .dcz = 5 files per asset

However, DCZ files are typically much smaller than other compressed variants (in our test,
total DCZ for changed files was 489 KB vs. 662 KB for zstd and 624 KB for brotli). The
storage overhead is minimal, and CDN edge caching can store all variants efficiently.

For CI/CD pipelines, the asset pack (~5 MB in our test) must be preserved between deployments.
This is analogous to preserving package-lock.json or other build artifacts — the previous
pack becomes an input to the next build.

---

## 17. Glossary

| Term | Definition |
|------|-----------|
| **CDT** | Compression Dictionary Transport — the protocol for using shared dictionaries over HTTP (RFC 9842) |
| **DCZ** | Delta Compressed Zstd — a zstd-compressed response using an external dictionary |
| **DCB** | Delta Compressed Brotli — future brotli equivalent of DCZ (not yet standardized) |
| **Asset Pack** | ZIP file containing the SWA publish manifest and uncompressed assets from a previous deployment |
| **Fingerprint** | Content hash embedded in the filename for cache busting (e.g., `app.abc123.js`) |
| **Webcil** | The WASM container format for .NET assemblies in Blazor WebAssembly apps |
| **SWA** | Static Web Assets — the MSBuild SDK for managing static files in ASP.NET Core |
| **Trained Dictionary** | A dictionary created by zstd's `ZDICT_trainFromBuffer` algorithm from sample data |
| **Raw Dictionary** | An arbitrary byte buffer used directly as a zstd dictionary (no training) |
| **URLPattern** | Web API for pattern matching URLs, used in `Use-As-Dictionary: match=` directives |
| **Available-Dictionary** | HTTP request header containing the SHA-256 hash of a cached dictionary |
| **Use-As-Dictionary** | HTTP response header instructing the browser to cache the response as a dictionary |
| **Structured Field** | HTTP header value format (RFC 8941) — hashes use `:base64:` encoding |

---

*Document generated from experimental research conducted on the `javiercn/zstd-support` branch
of the `dotnet/sdk` repository, using .NET 11.0 Preview 4 (SDK 11.0.100-dev).*
