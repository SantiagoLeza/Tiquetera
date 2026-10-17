using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Cinema.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seats",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    row = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    hold_token = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seats", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "seats",
                columns: new[] { "id", "expires_at", "hold_token", "number", "row", "status" },
                values: new object[,]
                {
                    { "A1", null, null, 1, "A", "Free" },
                    { "A10", null, null, 10, "A", "Free" },
                    { "A2", null, null, 2, "A", "Free" },
                    { "A3", null, null, 3, "A", "Free" },
                    { "A4", null, null, 4, "A", "Free" },
                    { "A5", null, null, 5, "A", "Free" },
                    { "A6", null, null, 6, "A", "Free" },
                    { "A7", null, null, 7, "A", "Free" },
                    { "A8", null, null, 8, "A", "Free" },
                    { "A9", null, null, 9, "A", "Free" },
                    { "B1", null, null, 1, "B", "Free" },
                    { "B10", null, null, 10, "B", "Free" },
                    { "B2", null, null, 2, "B", "Free" },
                    { "B3", null, null, 3, "B", "Free" },
                    { "B4", null, null, 4, "B", "Free" },
                    { "B5", null, null, 5, "B", "Free" },
                    { "B6", null, null, 6, "B", "Free" },
                    { "B7", null, null, 7, "B", "Free" },
                    { "B8", null, null, 8, "B", "Free" },
                    { "B9", null, null, 9, "B", "Free" },
                    { "C1", null, null, 1, "C", "Free" },
                    { "C10", null, null, 10, "C", "Free" },
                    { "C2", null, null, 2, "C", "Free" },
                    { "C3", null, null, 3, "C", "Free" },
                    { "C4", null, null, 4, "C", "Free" },
                    { "C5", null, null, 5, "C", "Free" },
                    { "C6", null, null, 6, "C", "Free" },
                    { "C7", null, null, 7, "C", "Free" },
                    { "C8", null, null, 8, "C", "Free" },
                    { "C9", null, null, 9, "C", "Free" },
                    { "D1", null, null, 1, "D", "Free" },
                    { "D10", null, null, 10, "D", "Free" },
                    { "D2", null, null, 2, "D", "Free" },
                    { "D3", null, null, 3, "D", "Free" },
                    { "D4", null, null, 4, "D", "Free" },
                    { "D5", null, null, 5, "D", "Free" },
                    { "D6", null, null, 6, "D", "Free" },
                    { "D7", null, null, 7, "D", "Free" },
                    { "D8", null, null, 8, "D", "Free" },
                    { "D9", null, null, 9, "D", "Free" },
                    { "E1", null, null, 1, "E", "Free" },
                    { "E10", null, null, 10, "E", "Free" },
                    { "E2", null, null, 2, "E", "Free" },
                    { "E3", null, null, 3, "E", "Free" },
                    { "E4", null, null, 4, "E", "Free" },
                    { "E5", null, null, 5, "E", "Free" },
                    { "E6", null, null, 6, "E", "Free" },
                    { "E7", null, null, 7, "E", "Free" },
                    { "E8", null, null, 8, "E", "Free" },
                    { "E9", null, null, 9, "E", "Free" },
                    { "F1", null, null, 1, "F", "Free" },
                    { "F10", null, null, 10, "F", "Free" },
                    { "F2", null, null, 2, "F", "Free" },
                    { "F3", null, null, 3, "F", "Free" },
                    { "F4", null, null, 4, "F", "Free" },
                    { "F5", null, null, 5, "F", "Free" },
                    { "F6", null, null, 6, "F", "Free" },
                    { "F7", null, null, 7, "F", "Free" },
                    { "F8", null, null, 8, "F", "Free" },
                    { "F9", null, null, 9, "F", "Free" },
                    { "G1", null, null, 1, "G", "Free" },
                    { "G10", null, null, 10, "G", "Free" },
                    { "G2", null, null, 2, "G", "Free" },
                    { "G3", null, null, 3, "G", "Free" },
                    { "G4", null, null, 4, "G", "Free" },
                    { "G5", null, null, 5, "G", "Free" },
                    { "G6", null, null, 6, "G", "Free" },
                    { "G7", null, null, 7, "G", "Free" },
                    { "G8", null, null, 8, "G", "Free" },
                    { "G9", null, null, 9, "G", "Free" },
                    { "H1", null, null, 1, "H", "Free" },
                    { "H10", null, null, 10, "H", "Free" },
                    { "H2", null, null, 2, "H", "Free" },
                    { "H3", null, null, 3, "H", "Free" },
                    { "H4", null, null, 4, "H", "Free" },
                    { "H5", null, null, 5, "H", "Free" },
                    { "H6", null, null, 6, "H", "Free" },
                    { "H7", null, null, 7, "H", "Free" },
                    { "H8", null, null, 8, "H", "Free" },
                    { "H9", null, null, 9, "H", "Free" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_seats_row_number",
                table: "seats",
                columns: new[] { "row", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seats");
        }
    }
}
