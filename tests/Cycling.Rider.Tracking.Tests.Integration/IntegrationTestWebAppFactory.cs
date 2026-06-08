using Cycling.Rider.Tracking.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Cycling.Rider.Tracking.Tests.Integration;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17")
        .Build();

    private readonly RabbitMqContainer rabbitMq = new RabbitMqBuilder("rabbitmq:4.3.1")
        .Build();

    private readonly MinioContainer minio = new MinioBuilder("minio/minio:latest")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            postgres.StartAsync(),
            rabbitMq.StartAsync(),
            minio.StartAsync());

        // AddInfrastructure captures these values at DI-registration time (before the test
        // host applies any ConfigureAppConfiguration overrides), so they must be present in
        // configuration when the host is built. Environment variables sit last in the default
        // configuration chain and are read by WebApplication.CreateBuilder up front.
        Environment.SetEnvironmentVariable("ConnectionStrings__Database", postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Messaging__Host", rabbitMq.GetConnectionString());
        Environment.SetEnvironmentVariable("Storage__ServiceUrl", $"http://{minio.Hostname}:{minio.GetMappedPublicPort(9000)}");
        Environment.SetEnvironmentVariable("Storage__AccessKey", minio.GetAccessKey());
        Environment.SetEnvironmentVariable("Storage__SecretKey", minio.GetSecretKey());
        Environment.SetEnvironmentVariable("Storage__Bucket", "rides");

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Database", null);
        Environment.SetEnvironmentVariable("Messaging__Host", null);
        Environment.SetEnvironmentVariable("Storage__ServiceUrl", null);
        Environment.SetEnvironmentVariable("Storage__AccessKey", null);
        Environment.SetEnvironmentVariable("Storage__SecretKey", null);
        Environment.SetEnvironmentVariable("Storage__Bucket", null);

        await postgres.DisposeAsync();
        await rabbitMq.DisposeAsync();
        await minio.DisposeAsync();

        await base.DisposeAsync();
    }
}
