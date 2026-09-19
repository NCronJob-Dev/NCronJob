# Known gotchas

This page collects the most common NCronJob pitfalls in one place. Use it as a quick checklist when a job does not behave the way you expected.

## Startup jobs run together

Startup jobs are all triggered when `UseNCronJob` or `UseNCronJobAsync` is called, and they must finish before regular CRON or instant jobs begin.

!!! warning
    Startup jobs are awaited together, so their relative execution order is not guaranteed.
    If one startup job depends on another, model that dependency explicitly instead of relying on registration order.

## Forced jobs bypass queue safeguards

`ForceRunInstantJob` and `ForceRunScheduledJob` bypass the queue and ignore the usual concurrency limits.

Use them only when you intentionally want to skip the built-in protections against overlapping work and resource contention.

## Conditions are not re-evaluated for retries

`OnlyIf(...)` is evaluated before the job instance is created.

If a job starts and then retries because of a retry policy, those retries continue even if the condition would later evaluate to `false`.

## Parameters are passed by reference

Parameters are not cloned. If you pass a mutable object and the job changes it, later executions can observe that mutated state.

Prefer immutable parameter objects, or clone the parameter inside the job before mutating it.

## Instant jobs can be ambiguous

Triggering an instant job by type only works when NCronJob can uniquely identify the target registration.

If you registered the same job type more than once, prefer giving each registration a unique name and trigger the intended one by name.

## Runtime removal has limits

Jobs that act as dependency targets cannot be removed while another job still references them as a dependent job.

If you need to change that graph, update the dependency registrations first and then remove the job.

## Duplicate registrations fail fast

NCronJob rejects duplicate registrations and conflicting named registrations during setup.

This is especially relevant when:

- the same job is registered multiple times with the same configuration
- two named jobs use the same custom name
- an unnamed typed instant job with a configured parameter is registered more than once

## Scheduler pressure can skip work

NCronJob has a global concurrency cap and queued runs can expire.

If jobs run longer than their cadence, or if concurrency is set too low, scheduled runs can be skipped or expire before they ever start.

See also:

- [Global Concurrency](./global-concurrency.md)
- [Job Timeouts and Run Expiry](../features/timeouts-and-expiry.md)
- [Triggering instant jobs](../features/instant-jobs.md)
- [Conditional Job Scheduling with OnlyIf](../features/conditional-job-scheduling.md)
- [Dynamic Job Control](./dynamic-job-control.md)
