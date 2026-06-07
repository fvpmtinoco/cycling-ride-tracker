using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cycling.Rider.Tracking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTransactionFilesContentType : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "content_type",
            schema: "public",
            table: "transaction_files",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "content_type",
            schema: "public",
            table: "transaction_files");
    }
}
