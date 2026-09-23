# Memory implementation plan

The main constraint is concentration storage that grows with grid cells times source groups. The proposal must also handle cells where most groups overlap, and preserve transient carryover when a group stops emitting.

## Review order

1. Review the retained-memory probe and the single-cell state transitions in `SourceGroupBuffer.cs`. Existing cells with at most 100 slots use ordinary arrays. Extended cells begin empty, hold one block directly, allocate a directory only when a second distinct block is written, and become dense at the existing threshold. Block size stays 32. The latest follow-up changes only this storage file and its tests.
2. Compare sparse, clustered and fully occupied cases before deciding whether the storage layer is acceptable. Review the caller changes in the original PR separately from numeric group IDs and the two-character filename codec. IDs and filenames are not the mechanism that reduces RAM.
3. Require equivalent full-model payloads and equivalent checkpoint restart paths. Keep ordered group IDs and scientific inputs unchanged. Validate production memory and time on representative domains before raising operational group counts.

## Follow-up measurement

10,000 cells, 1,295 float slots per cell, three fresh processes per condition; medians after full GC:

| Occupancy per cell | Previous 32-slot blocks | Direct first block |
| --- | ---: | ---: |
| Empty | 0.46 MiB | 0.46 MiB |
| One group | 5.252 MiB | 1.902 MiB |
| 31 adjacent groups in one block | 5.252 MiB | 1.902 MiB |
| 31 groups in distinct blocks | 50.125 MiB | 50.125 MiB |
| All 1,295 groups | 50.125 MiB | 50.125 MiB |

The one-block case uses 63.78% less retained storage than the preceding sparse implementation. Empty cells keep the same field count. This is a cell-storage measurement, not total-process RAM. The fully occupied case still has the earlier sparse-wrapper overhead; the previously reported dense-loop and small-model slowdowns remain relevant.

Eight-slot and 16-slot block experiments reduced isolated occupancy further, but the 31-adjacent-group case rose to 15.141 MiB and 8.523 MiB, respectively. They are not included. Dictionaries, tile-level allocation and source-group batching need broader changes and have not been applied. Deleting currently inactive groups would lose transient carryover.

The component suite now has 163,392 assertions in 17 cases, including first writes at block edges, writes to another block, zero writes, dense conversion, clearing and stable lock identity. Existing filename, routing, accumulation and checkpoint tests remain in this directory. A separate output-history PR handles receptor memory; particle source lookup and allocation proposals remain separate.
