# Global Concurrency
**NCronJob** utilisies a priority queue with a maximum amount of entries. That is, the queue will only hold and execute a maximum amount of jobs at any given time. This is to prevent the system from being overloaded with jobs.

## The Maxmimum
The global maximum of concurrent jobs is calculated as:

```csharp
var maxDegreeOfParallelism = Environment.ProcessorCount * 4;
```

If you have a CPU with 12 Cores (like a M2 Processor), the maximum amount of concurrent jobs will be 48. A CRON job is rescheduled after it has been executed. This means that the queue will always be filled with jobs that are ready to be executed. If that queue is full, no more jobs will be added to the queue until a job has been executed.

A simple example: You have only one processor (therefore maximum 4 jobs executed at the same time) and a cron job that runs every minute. The job takes six minutes to complete. 
So after four minutes the queue is full, and no more jobs will be added to the queue. After the fifth minute, the queue is still full, and no more jobs will be added to the queue. After the sixth minute, the first job is removed from the queue, and the next job is added to the queue. Therefore it can happen that jobs are skipped and not executed.

The same applies to the `SupportsConcurrencyAttribute` discussed in: *[Concurrency Control](../features/concurrency-control.md)*
