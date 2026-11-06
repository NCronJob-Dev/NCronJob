#!/usr/bin/env python3
"""Runs the NCronJob benchmark harness built from two checkouts and prints a median comparison.

Usage: compare.py <baseline-harness-dir> <candidate-harness-dir> [runs]
Each directory is the output folder of `dotnet build -c Release` of benchmarks/NCronJob.Benchmarks.
Runs are interleaved (baseline, candidate, baseline, ...) to spread machine noise evenly over both sides.
"""
import json
import statistics
import subprocess
import sys

SCENARIOS = [("instant", 100_000), ("dependent", 50_000), ("startup-cron", 5_000), ("runtime-churn", 3_000)]
METRICS = [
    ("WallMs", "wall ms"),
    ("CpuMs", "CPU ms"),
    ("AllocatedMb", "allocated MB"),
    ("PeakManagedMb", "peak managed heap MB"),
    ("PeakWorkingSetMb", "peak working set MB"),
    ("RetainedMb", "retained MB"),
    ("Gen0", "gen0 GCs"),
]


def run(harness_dir: str, scenario: str, count: int) -> dict:
    out = subprocess.run(
        ["dotnet", f"{harness_dir}/NCronJob.Benchmarks.dll", scenario, str(count)],
        check=True, capture_output=True, text=True, timeout=600,
    ).stdout.strip().splitlines()[-1]
    return json.loads(out)


def main() -> None:
    baseline, candidate = sys.argv[1], sys.argv[2]
    runs = int(sys.argv[3]) if len(sys.argv) > 3 else 7

    print(f"| Scenario | Metric | Baseline (median of {runs}) | Candidate (median of {runs}) | Change |")
    print("|---|---|---:|---:|---:|")
    for scenario, count in SCENARIOS:
        samples = {"baseline": [], "candidate": []}
        for _ in range(runs):
            samples["baseline"].append(run(baseline, scenario, count))
            samples["candidate"].append(run(candidate, scenario, count))
        for key, label in METRICS:
            base = statistics.median(s[key] for s in samples["baseline"])
            cand = statistics.median(s[key] for s in samples["candidate"])
            change = "n/a" if base == 0 else f"{(cand - base) / base * 100:+.1f}%"
            print(f"| {scenario} ({count:,}) | {label} | {base:.2f} | {cand:.2f} | {change} |")


if __name__ == "__main__":
    main()
