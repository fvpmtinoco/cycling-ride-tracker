using Cycling.Rider.Tracking.Domain.Rides;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Application.Abstractions.Data;

public interface IDatabaseContext
{
    DbSet<Ride> Rides { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
