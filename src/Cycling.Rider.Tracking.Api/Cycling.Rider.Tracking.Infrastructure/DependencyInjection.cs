using Amazon.Runtime;
using Amazon.S3;
using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Infrastructure.Database;
using Cycling.Rider.Tracking.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cycling.Rider.Tracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddDatabase(configuration)
                .AddStorage(configuration);

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");

        services.AddDbContext<DatabaseContext>(
            options => options
                .UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "public"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IDatabaseContext>(sp => sp.GetRequiredService<DatabaseContext>());

        return services;
    }

    private static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<S3StorageOptions>(configuration.GetSection("Storage"));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            S3StorageOptions options = sp.GetRequiredService<IOptions<S3StorageOptions>>().Value;
            return new AmazonS3Client(
                        new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                        new AmazonS3Config
                        {
                            ServiceURL = options.ServiceUrl,
                            ForcePathStyle = true  // true for MinIO, false for real S3/R2
                        });
        });

        services.AddScoped<IFileStore, S3FileStorage>();

        return services;
    }
}