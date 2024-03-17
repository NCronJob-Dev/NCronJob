<h1 align="center">NCronJob</h1>

<p align="center">
  <img src="assets/logo_small.png" alt="logo" width="120px" height="120px"/>
  <br>
  <em>Scheduling made easy</em>
  <br>
</p>

# NCronJob
A Job Scheduler sitting on top of `IHostedService` in dotnet.

Often times one finds themself between the simplicisty of the `BackgroundService`/`IHostedService` and the complexity of a full blown `Hangfire` or `Quartz` scheduler. 
This library aims to fill that gap by providing a simple and easy to use job scheduler that can be used in any dotnet application and feels "native".

## Features
- [x] The ability to schedule jobs using a cron expression
- [x] The ability to instantly run a job
- [x] Get notified when a job is done (either successfully or with an error)
