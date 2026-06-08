using Cycling.Rider.Tracking.Domain.Idempotency;
using Cycling.Rider.Tracking.Domain.Outbox;
using Cycling.Rider.Tracking.Domain.Rides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cycling.Rider.Tracking.Application.Abstractions.Data;

public interface IDatabaseContext
{
    DbSet<Ride> Rides { get; set; }
    DbSet<TransactionFile> TransactionFiles { get; set; }
    DbSet<IdempotencyKey> IdempotencyKeys { get; set; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
