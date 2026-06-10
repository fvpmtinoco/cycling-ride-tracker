using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cycling.Rider.Tracking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTransactionFilesCreatedDate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "created_at",
            schema: "public",
            table: "transaction_files",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "created_at",
            schema: "public",
            table: "transaction_files");
    }
}
