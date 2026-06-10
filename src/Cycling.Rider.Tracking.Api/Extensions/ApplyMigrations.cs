using Cycling.Rider.Tracking.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Api.Extensions;

public static class ApplyMigrations
{
    public static void ApplyMigrationsAtStartup(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        dbContext.Database.Migrate();
    }
}
