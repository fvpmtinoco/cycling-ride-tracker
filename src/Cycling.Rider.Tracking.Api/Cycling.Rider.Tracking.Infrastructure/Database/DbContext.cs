using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Domain.Rides;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Infrastructure.Database;

public sealed class DatabaseContext(DbContextOptions<DatabaseContext> options)
    : DbContext(options), IDatabaseContext
{
    public DbSet<Ride> Rides { get; set; }
    public DbSet<Domain.Outbox.TransactionFile> TransactionFiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly);

        modelBuilder.AddTransactionalOutboxEntities();

        modelBuilder.HasDefaultSchema("public");
    }
}
