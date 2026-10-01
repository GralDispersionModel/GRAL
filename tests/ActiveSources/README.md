# Active-source particle allocation

GRAL allocates the configured `TPS * TAUS` particle budget before each weather situation's source release. A source participates when its base emission is positive and its current source-group factor is positive. Deposition-only partitions also need a positive physical deposition emission rate. Sources outside the domain or outside the selected groups remain subject to the existing reader filters.

The active sources retain their original base-emission sampling weights and type-specific particle minima. Positive temporal factors still multiply particle mass once, in `Zeitschleife`; this change does not reweight the allocation by their magnitudes. The per-source minima and rounding can make the allocated total differ from `TPS * TAUS`.

An all-off situation releases zero new source particles. `ParallelTransientParticleDriver` continues to transport the stored plume, including groups with no current emissions. Its reference particle mass remains based on the unmodulated sources. Transient deposition averages are initialized from the unmodulated source allocation, so a group that first emits later, or has a plume loaded from a checkpoint, retains its deposition parameters.

The particle arrays are reused at the same allocation size and resized otherwise. This patch does not change source-group limits, filename encoding, random-number generators, checkpoint formats, or the transient concentration arrays.

## Reproducing the allocation problem

For each of the four source types, use base rates `[2, 0, 5, 3]`, groups `[1, 1, 2, 1]`, a current factor of 1 for group 1 and 0 for group 2, and a budget of 3000 particles. The original V2701 engine allocates 3062 particles: 1500 to active sources, 1500 to the inactive group and 62 to zero-rate sources. It discards the inactive group's particles only later, after initial coordinates have been calculated. An all-zero base inventory raises `OverflowException` during allocation.

The patched engine allocates 3000 particles to the active sources and none to the inactive or zero-rate sources. Changing the active group on the next step reallocates the original configured budget, without retaining the previous counts or compounding the per-source minima.

## Validation

The standalone C# harness loads a built engine by reflection, allowing the same test executable to inspect the original and patched assemblies. It uses no test-framework packages.

```powershell
# Run from the repository root, with .NET 10 SDK and Python 3.
dotnet build src/GRAL.csproj -c Release -o artifacts/active
dotnet build tests/ActiveSources/ActiveSources.csproj -c Release -o artifacts/harness
dotnet artifacts/harness/ActiveSources.dll artifacts/active/GRAL.dll artifacts/component-results

# Build the unmodified V2701 reference in a separate worktree.
git worktree add --detach ../gral-active-reference 1517026eb0291066845e84a1c923ba51f4371d9c
dotnet build ../gral-active-reference/src/GRAL.csproj -c Release -o artifacts/reference
dotnet artifacts/harness/ActiveSources.dll artifacts/reference/GRAL.dll artifacts/baseline-reproduction --baseline
py -3 tests/ActiveSources/validation.py --baseline artifacts/reference/GRAL.dll --patched artifacts/active/GRAL.dll --output artifacts/integration-results
```

The output directories must be new. On systems without the Windows Python launcher, use the installed Python 3 interpreter in place of `py -3`. Pass absolute engine paths if running the integration script from a different working directory.

Verified on Windows with .NET SDK 10.0.301 and runtime 10.0.9:

- 12 component cases and 52 assertions cover all four source types, zero rates, time switching, all-off and reactivation, configured-budget reuse, minima, sparse/reordered group IDs, temporal bounds, deposition-only partitions and array reuse.
- 22 model executions pass 16 integration checks. They cover point, line, area and tunnel sources, transient concentration and deposition carryover, a nonfirst starting hour, small allocations, and both fixed-seed and normal random-number modes with two workers.
- All-positive transient and steady comparisons match the original engine's 20 concentration/deposition payloads byte-for-byte. The first-step active plume also matches a separate input with the inactive source records physically removed, with geometry preserved.
- An identical checkpoint is resumed by both engines at situation 5, when all emissions are off. The four recomputed concentration/deposition payloads match; the earlier 16 payloads stay unchanged. The patched engine allocates zero new source particles and still transports and deposits the stored plume. This fixture removes `KeepAndReadTransientTempFiles.dat` before resuming because that override otherwise reads the checkpoint while starting again at the configured first situation. It verifies the actual first resumed situation in the log.

## Interpretation

When only some sources emit, they receive more of the particle budget. Their total emitted mass is preserved, but their Monte Carlo samples and individual concentration values can change. Existing per-source minima still apply to active sources. This change can improve use of the sampling budget; it does not establish lower runtime or statistical convergence on a production domain.

The tests use small synthetic flat domains. Complex terrain, buildings, production-scale convergence and cross-device numerical identity require separate validation. The existing checkpoint filtering and source-group order requirements still apply.

This PR targets V2701 independently of source-group PR #54. A local combination with #54 passed its 17 component tests (150427 assertions), its 99-group byte comparison, 100/300/1295-group temporal cases, and 101 distinct hourly groups over 101 full hourly steps. When integrating both changes, retain #54's Int32 `ParticleSG` allocation and move the deposition initialization to `ParticleManagement` as in this patch.
