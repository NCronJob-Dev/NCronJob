# Benchmarks and architecture metrics

Tooling used to judge refactorings objectively. Neither project is part of `NCronJob.slnx` or shipped.

## Runtime benchmark (CPU, RAM, allocations, GC)

```bash
dotnet build benchmarks/NCronJob.Benchmarks -c Release
dotnet benchmarks/NCronJob.Benchmarks/bin/Release/net10.0/NCronJob.Benchmarks.dll <scenario> <size>
```

Scenarios: `instant`, `dependent`, `startup-cron`, `runtime-churn`, `cron-burst` (many second-precision cron jobs due at once). Each run prints one JSON line.

To compare two checkouts, build the harness once per checkout (`-p:NCronJobProject=<path to NCronJob.csproj>`),
copy both outputs somewhere and run

```bash
python3 benchmarks/compare.py <baseline-output-dir> <candidate-output-dir> 7
```

Runs are interleaved and the median is reported. Run the same output directory against a copy of itself (A/A)
to learn the noise floor of the machine before reading any difference.

## Architecture metrics

```bash
dotnet build benchmarks/NCronJob.Benchmarks -c Release
dotnet run --project benchmarks/ArchMetrics -c Release -- src/NCronJob benchmarks/NCronJob.Benchmarks/bin/Release/net10.0
```

The second argument is any folder that contains the library's dependency assemblies (the benchmark output does).
The tool compiles the sources with the SDK implicit usings and stops if symbols stay unresolved, because those would
silently understate coupling.

Reports per type efferent/afferent coupling (Ce/Ca/CBO), instability, cyclomatic complexity, constructor
parameters, cohesion (LCOM4, Henderson-Sellers LCOM*) and a hotspot score
`Ce × max(LCOM4, 1) × (1 + CC / 50)` that is high only for types that are both coupled and incohesive. It also
reports dependency cycles and per-folder component cohesion.
