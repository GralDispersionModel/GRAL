# Optimization regression checks

Build the original V2701 core in a separate checkout and build this branch into a different directory. Use .NET 10. Run each harness against the original and patched assemblies, then compare the result JSON hashes.

```powershell
dotnet build src/GRAL.csproj -c Release -o artifacts/patched
dotnet build tests/Optimizations -c Release -o artifacts/harness
dotnet artifacts/harness/Optimizations.dll ../baseline/src/bin/Release/net10.0/GRAL.dll artifacts/baseline.json
dotnet artifacts/harness/Optimizations.dll artifacts/patched/GRAL.dll artifacts/patched.json
```

The harness loads production methods from the supplied assembly. Use a fresh process for each measurement. Timings describe the changed stage and vary by machine; they are not total-model speedup estimates.

The harness checks randomized source mappings and Int32 boundaries, then compares actual starting coordinates, masses and source identities for all four source types. The full-run script generates small flat-terrain cases with fixed random seeds and one worker:

```powershell
py -3 tests/Optimizations/validation.py --baseline ../baseline/src/bin/Release/net10.0/GRAL.dll --patched artifacts/patched/GRAL.dll --output artifacts/full-regression
```

The output directory must be new. Python uses only its standard library. Steady and transient cases exercise zero receptors, the stack/heap boundary, and a larger receptor set. Compare concentration and deposition payloads after ZIP extraction, and compare receptor files directly.
