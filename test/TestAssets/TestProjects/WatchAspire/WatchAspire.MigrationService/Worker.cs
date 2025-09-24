using System.Diagnostics;

namespace MigrationService;

public class Worker(IHostApplicationLifetime hostApplicationLifetime, ILogger<Worker> logger) : BackgroundService
{
    private static readonly ActivitySource s_activitySource = new("Migrations");

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity(
            "Migrating database",
            ActivityKind.Client
        );

        logger.LogInformation("Migration complete");

        hostApplicationLifetime.StopApplication();

        return Task.CompletedTask;
    }
}
