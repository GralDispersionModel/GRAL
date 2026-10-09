# Receptor history memory

Both receptor writers copy existing history one row at a time into a temporary file in the destination directory. They replace the destination only after both streams close. Memory no longer grows with the retained history; the current row and fixed headers still occupy memory. Each update still reads and rewrites the file, so this change does not remove cumulative disk I/O.

The row replacement, missing-row padding, later rows, statistical-error filtering, UTF-8 steady output and UTF-16 transient output follow the existing formats. Read/write or replacement failures leave the old destination intact. This does not provide a power-loss durability guarantee.

## Reproduce

Use Windows x64, .NET 10 SDK and Python 3. Build an unchanged V2701 checkout and this branch into separate directories, then run:

```powershell
dotnet build src/GRAL.csproj -c Release -o out/core
dotnet build tests/ReceptorHistory/Memory.csproj -c Release -o out/tests
out/tests/Memory.exe out/core/GRAL.dll out/unit-results
py -3 tests/ReceptorHistory/validation.py --baseline <baseline/GRAL.dll> --patched out/core/GRAL.dll --output out/model-results
```

The helper suite has 166 checks, including encoding, missing/short histories, row replacement, padding, statistics, locked files and cleanup. The model suite runs both engines in steady and transient modes with 0, 2, 255, 256 and 1,000 receptors. All 80 concentration/deposition payloads and 16 receptor files matched byte for byte in 20 model executions.

For a fresh-process memory comparison, run the helper executable with a final argument of `baseline` or `stream`, using a new output folder each time. It creates 12,000 rows of 4,096 characters in UTF-16 (about 94 MiB). Run each mode three times. The legacy comparator uses the same row transformation with a retained List<string>.

Median process peak working set was 136,732,672 bytes before and 88,526,848 bytes after (35.26% lower). All six output hashes matched. Total allocated bytes changed little, because rows still have to be decoded and written. These are output-writer measurements on one Windows machine, not total-model or annual-run RAM estimates.
