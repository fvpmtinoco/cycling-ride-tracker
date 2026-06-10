using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cycling.Rider.Tracking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class TransactionOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "transaction_files",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                file_content = table.Column<byte[]>(type: "bytea", nullable: false),
                file_id = table.Column<Guid>(type: "uuid", nullable: false),
                processed = table.Column<bool>(type: "boolean", nullable: false),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_transaction_files", x => x.id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "transaction_files",
            schema: "public");
    }
}
