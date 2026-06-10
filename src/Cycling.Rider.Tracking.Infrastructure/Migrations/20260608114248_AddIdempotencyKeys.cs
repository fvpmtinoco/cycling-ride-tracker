using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cycling.Rider.Tracking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddIdempotencyKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "idempotency_keys",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                response_status_code = table.Column<int>(type: "integer", nullable: false),
                response_body = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_idempotency_keys", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_idempotency_keys_key",
            schema: "public",
            table: "idempotency_keys",
            column: "key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "idempotency_keys",
            schema: "public");
    }
}
