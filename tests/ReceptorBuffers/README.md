# Bounded receptor buffers

The particle drivers use a cleared stack buffer for up to 255 receptors plus the existing index-zero slot. No receptors require an empty buffer; larger sets use a zero-initialized heap array. Stack use is capped at 2 KiB per call.

The steady driver previously allocated an array for every particle. The transient driver used an unbounded stack allocation without an explicit clear. Source lookup, particle counts, physical calculations and summation order are unchanged.

Build this branch and an unchanged V2701 checkout with .NET 10, then run on Windows:

```powershell
py -3 tests/ReceptorBuffers/validation.py --baseline <baseline/GRAL.dll> --patched <patched/GRAL.dll> --output <new-results-folder>
```

The fixture exercises point, line, area and portal sources with deposition in steady and transient modes, with 0, 2, 255, 256 and 1,000 receptors. Twenty model executions produced 80 concentration/deposition payloads and 16 receptor files identical to the baseline. This checks the stack/heap boundary and numerical output. It does not measure total-run speedup.
