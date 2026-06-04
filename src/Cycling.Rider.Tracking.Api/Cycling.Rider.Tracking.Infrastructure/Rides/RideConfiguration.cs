using Cycling.Rider.Tracking.Domain.Rides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cycling.Rider.Tracking.Infrastructure.Rides;

internal class RideConfiguration : IEntityTypeConfiguration<Ride>
{
    public void Configure(EntityTypeBuilder<Ride> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("ride");
    }
}
