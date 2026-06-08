using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BizMetrics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDatasetProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Columns",
                table: "Datasets",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Datasets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "Datasets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "Datasets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataRows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataRows_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataRows_DatasetId_RowIndex",
                table: "DataRows",
                columns: new[] { "DatasetId", "RowIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataRows");

            migrationBuilder.DropColumn(
                name: "Columns",
                table: "Datasets");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "Datasets");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "Datasets");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "Datasets");
        }
    }
}
