# Source groups through 1295

The core accepts unique IDs in 1..1295 and maps them to compact internal indices. Source and particle IDs use int. Emission totals and decay arrays follow the selected group count. Use the [companion GUI PR #96](https://github.com/GralDispersionModel/GUI/pull/96) with [core PR #54](https://github.com/GralDispersionModel/GRAL/pull/54).

[FilenameProtocol.md](FilenameProtocol.md) defines the shared two-character tokens. Existing names for IDs 1..99 are unchanged. The original Int32 extension remains on [codex/archive-source-groups-int32-20260915](https://github.com/Borealis-Thoon/GRAL/tree/codex/archive-source-groups-int32-20260915).

## Concentration storage

Large source-group cells allocate blocks of 32 values when a nonzero value is written. Cells that fill at least three quarters of their block capacity switch to a dense array. Clearing a large cell releases its blocks. Legacy cells retain their original float[] or double[] allocation, including the transient guard slot.

This applies to concentration slices, deposition, odour gradients, and both transient fields. It preserves source/particle traversal, cell locks, accumulation expressions, RNG settings, and output/checkpoint phases. It does not implement a group-by-group ring buffer. Such a scheduler would need separate review of transient carryover, particle order and performance.

The memory benefit depends on occupancy. In an isolated storage test with 20000 cells and 1295 groups, one occupied group per cell used 10.50 MiB versus 99.48 MiB for the previous dense arrays. Fully occupied cells used 100.24 MiB versus 99.48 MiB. At 99 groups, both used 8.22 MiB. These are managed storage measurements, not whole-model RAM figures.

The dense buffer-only workload was about 2.5 to 2.7 times slower with extended groups. In the small simulation comparisons, median total times increased by about 0 to 19 percent. This tradeoff needs production-domain profiling before adoption; memory savings are not a performance guarantee.

## Reproduce the tests

Use .NET 10 SDK and Python 3. The GUI integration harness additionally needs Windows Desktop.

```powershell
dotnet build src/GRAL.csproj -c Release -o artifacts/source-groups/core
dotnet build tests/SourceGroups/SourceGroups.csproj -c Release -o artifacts/source-groups/tests
dotnet artifacts/source-groups/tests/SourceGroups.dll artifacts/source-groups/components
py -3 tests/SourceGroups/validation.py --original path/to/unmodified/GRAL.dll --patched artifacts/source-groups/core/GRAL.dll --output artifacts/source-groups/simulations
```

Use a new output directory. The component harness checks 17 cases, including storage reuse and locked parallel accumulation, four source readers, temporal mapping, decay, and concentration/deposition/odour filenames. The simulation comparison checks 297 identical legacy payloads, 100/300/1295 groups, 101 full hourly steps, rejected inputs, and transient carryover.

For the memory comparison, build the dense filename-compatible revision 0288968f47ae632ec44b2cb6675a3f614053caae in a separate checkout, then run:

```powershell
py -3 tests/SourceGroups/memory_validation.py --dense path/to/dense/GRAL.dll --patched artifacts/source-groups/core/GRAL.dll --harness artifacts/source-groups/tests/SourceGroups.dll --output artifacts/source-groups/memory
```

This script measures storage in isolated processes, repeats four simulation comparisons three times per build, and compares equivalent restarts in both directions. Normal, transient, and odour payloads match the dense implementation in the supplied fixtures. Deposition data and filenames are covered by component tests; physical deposition and building-volume corrections need broader model validation.

## Limits and restart contract

These are small synthetic cases on Windows/.NET 10.0.9 with one worker and the existing reproducible option. They do not establish annual-scale capacity, physical convergence, cross-device equality, or coverage of complex terrain/buildings.

Dense plumes still need memory proportional to the group count. Receptor arrays, meteorology, particles and output volume are not made sparse. One group per hour now has a maximum of 1295 distinct group IDs.

The existing checkpoint format stores counts, not the ordered external IDs. Restart only with the same ordered groups and unchanged inputs. Dense and sparse storage give identical results when resuming the same checkpoint. An uninterrupted run is not generally byte-identical to a restart: the pre-existing checkpoint writer filters stored concentrations and omits the upper boundary level. The comparison records this difference for both implementations. Checkpoint format and filtering are unchanged by this patch.

Missing selected temporal columns retain factor 1. Unselected columns are ignored after validating their IDs. Selected factors must be finite and nonnegative. Duplicate headers and unsupported IDs are rejected.
