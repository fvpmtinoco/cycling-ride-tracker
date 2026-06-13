using Cycling.Rider.Tracking.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cycling.Rider.Tracking.Infrastructure.Cleanup;

public class CleanupService(IServiceScopeFactory serviceScopeFactory, ILogger<CleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Need to inject IServiceScopeFactory to resolve DbContext from a created scoped per unit of work
                // since DBContext is registered scoped and the hosted service as singleton
                using var scope = serviceScopeFactory.CreateScope();

                var db = scope.ServiceProvider.GetService<DatabaseContext>();

                var cutOffDate = DateTimeOffset.UtcNow.AddDays(-1);

                // Delete directly with a single set-based DELETE instead of loading
                // the rows and removing them. Concurrency-safe: overlapping instances
                // just delete whatever rows remain, with no row-count assertion to fail.
                // ExecuteDeleteAsync runs directly, it bypasses the change tracker (SaveChangesAsync)
                await db!.TransactionFiles
                    .Where(x => x.Processed && x.ProcessedAt <= cutOffDate)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Cleanup service stopping");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in cleanup service cycle");
            }
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
