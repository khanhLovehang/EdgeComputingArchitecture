using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class Mig1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceivedTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OriginalTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessingCompletedTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataRecords_DeviceId",
                table: "DataRecords",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DataRecords_ReceivedTimestamp",
                table: "DataRecords",
                column: "ReceivedTimestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataRecords");
        }
    }
}
