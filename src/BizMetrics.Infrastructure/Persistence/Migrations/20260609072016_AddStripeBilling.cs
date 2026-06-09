using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizMetrics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "Organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StripeEventLogs",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeEventLogs", x => x.EventId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StripeEventLogs");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Organizations");

            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "Organizations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
