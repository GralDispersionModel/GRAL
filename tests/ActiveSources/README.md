# Active source allocation checks

These checks exercise the production allocation component with a small fixture.
The fixture supplies program state and a log writer. It links
`src/TeilchenAufteilung.cs` and `src/TransientDeposition.cs` directly.

## Requirements

Install the .NET 10 SDK. Run the commands from the repository root.

## Run all component checks

PowerShell:

```powershell
$cases = @('active', 'off', 'budget', 'deposition', 'deposition-source', 'capacity')
foreach ($case in $cases) {
    dotnet run --project tests/ActiveSources/ActiveSources.csproj -- $case
    if ($LASTEXITCODE -ne 0) { throw "Check failed: $case" }
}
```

POSIX shell:

```sh
for case in active off budget deposition deposition-source capacity; do
    dotnet run --project tests/ActiveSources/ActiveSources.csproj -- "$case" || exit 1
done
```

Each run prints `PASS` when its assertions succeed. A failed assertion returns
a nonzero exit code.

- `active`: positive weights, four source types, transitions, and factor fallback.
- `off`: no new particles and preserved residual-particle settings.
- `budget`: the configured budget after a situation with minimum counts.
- `deposition`: reference velocity weights and deposition-only exclusion.
- `deposition-source`: activity based on the deposition emission rate.
- `capacity`: array growth, reuse, and reordered source-group lookup.

## Run synthetic transport cases

The synthetic runner requires Python 3 and `dotnet` on `PATH`. It creates each
output directory and requires a new path. The inputs use
three virtual point sources in a flat 200 m square domain. The runner uses a
fixed random seed, `TAUS=3600 s`, and `TPS=0.03` (108 particles per hour).
The `control24`, `sparse24`, and `switch48` cases use total emissions of
3 kg/h. `edge4` includes two hours with no active sources.

Build the model and run the examples with new output paths:

```sh
dotnet build src/GRAL.csproj -c Release
python3 tests/ActiveSources/run_synthetic.py --case sparse24 --dll src/bin/Release/net10.0/GRAL.dll --out /tmp/gral-sparse24
python3 tests/ActiveSources/run_synthetic.py --case switch48 --dll src/bin/Release/net10.0/GRAL.dll --out /tmp/gral-switch48
```

On Windows, use a Python 3 launcher such as `py -3` or `python` and select new
output paths, for example under `%TEMP%`. Use `control24` or `edge4` with the
same command to run the other cases.

These checks cover the allocation component and small synthetic transport
cases. They do not establish equivalence for a production domain.
