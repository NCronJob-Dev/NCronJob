using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class ServiceValidationTests : JobIntegrationBase
{
    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldSucceedWhenAllDependenciesAreRegistered()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("0 0 * * *")));
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw
        await app.UseNCronJobAsync(options => options.ValidateOnBuild = true);
    }

    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldThrowWhenDependencyIsMissing()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("0 0 * * *")));
            // Intentionally not registering the dependency
        });

        using var app = BuildApp(builder);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            app.UseNCronJobAsync(options => options.ValidateOnBuild = true));
        
        exception.Message.ShouldContain("service validation failed");
        exception.Message.ShouldContain(nameof(JobWithDependency));
        exception.Message.ShouldContain(nameof(MyService));
    }

    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldSucceedWhenDependencyIsRegistered()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("0 0 * * *")));
            services.AddSingleton<MyService>();
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw
        await app.UseNCronJobAsync(options => options.ValidateOnBuild = true);
    }

    [Fact]
    public async Task UseNCronJob_WithoutValidateOnBuild_ShouldNotValidate()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("0 0 * * *")));
            // Intentionally not registering the dependency
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw because validation is disabled
        await app.UseNCronJobAsync(options => options.ValidateOnBuild = false);
    }

    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldValidateDelegateJobDependencies()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob((MyService service) => 
            {
                // Job implementation
            }, "0 0 * * *");
            // Intentionally not registering MyService
        });

        using var app = BuildApp(builder);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            app.UseNCronJobAsync(options => options.ValidateOnBuild = true));
        
        exception.Message.ShouldContain("service validation failed");
        exception.Message.ShouldContain(nameof(MyService));
    }

    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldSucceedForDelegateJobWithRegisteredDependencies()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob((MyService service) => 
            {
                // Job implementation
            }, "0 0 * * *");
            services.AddSingleton<MyService>();
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw
        await app.UseNCronJobAsync(options => options.ValidateOnBuild = true);
    }

    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldIgnoreCancellationTokenParameter()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob((CancellationToken ct) => 
            {
                // Job implementation
            }, "0 0 * * *");
            // CancellationToken doesn't need to be registered
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw
        await app.UseNCronJobAsync(options => options.ValidateOnBuild = true);
    }

    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldIgnoreJobExecutionContextParameter()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob((JobExecutionContext context) => 
            {
                // Job implementation
            }, "0 0 * * *");
            // JobExecutionContext doesn't need to be registered
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw
        await app.UseNCronJobAsync(options => options.ValidateOnBuild = true);
    }

    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldHandleOptionalParameters()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n.AddJob<JobWithOptionalDependency>(p => p.WithCronExpression("0 0 * * *")));
            // Optional dependency is not registered - should still pass
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw
        await app.UseNCronJobAsync(options => options.ValidateOnBuild = true);
    }

    [Fact]
    public async Task UseNCronJob_WithValidateOnBuild_ShouldValidateMultipleJobs()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression("0 0 * * *"))
                .AddJob<JobWithDependency>(p => p.WithCronExpression("0 1 * * *")));
            // Missing MyService dependency for JobWithDependency
        });

        using var app = BuildApp(builder);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            app.UseNCronJobAsync(options => options.ValidateOnBuild = true));
        
        exception.Message.ShouldContain("service validation failed");
        exception.Message.ShouldContain(nameof(JobWithDependency));
    }

    [Fact]
    public async Task UseNCronJob_DefaultBehavior_ShouldNotValidateInNonDevelopmentEnvironment()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("0 0 * * *")));
            // Intentionally not registering the dependency
            // Not adding IHostEnvironment, so it won't be in development mode
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw because development check will fail
        await app.UseNCronJobAsync();
    }

    [Fact]
    public async Task UseNCronJob_InDevelopmentEnvironment_ShouldAutoEnableValidation()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("0 0 * * *")));
            // Intentionally not registering the dependency
            
            // Add a development environment
            services.AddSingleton<IHostEnvironment>(new FakeDevelopmentEnvironment());
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should throw because development environment auto-enables validation
        var exception = await Should.ThrowAsync<InvalidOperationException>(app.UseNCronJobAsync());
        
        exception.Message.ShouldContain("service validation failed");
        exception.Message.ShouldContain(nameof(JobWithDependency));
    }

    [Fact]
    public async Task UseNCronJob_InDevelopmentEnvironment_CanDisableValidationExplicitly()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("0 0 * * *")));
            // Intentionally not registering the dependency
            
            // Add a development environment
            services.AddSingleton<IHostEnvironment>(new FakeDevelopmentEnvironment());
        });

        using var app = BuildApp(builder);

        // Act & Assert - Should not throw because we explicitly disabled validation
        await app.UseNCronJobAsync(options => options.ValidateOnBuild = false);
    }

    private IHost BuildApp(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Storage);
            services.Replace(new ServiceDescriptor(typeof(TimeProvider), FakeTimer));
        });

        return builder.Build();
    }

    // Test jobs
    private class SimpleJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }

    private class JobWithDependency : IJob
    {
        private readonly MyService service;

        public JobWithDependency(MyService service)
        {
            this.service = service;
        }

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }

    private class JobWithOptionalDependency : IJob
    {
        private readonly MyService? service;

        public JobWithOptionalDependency(MyService? service = null)
        {
            this.service = service;
        }

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }

    private class MyService
    {
    }

    private sealed class FakeDevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
