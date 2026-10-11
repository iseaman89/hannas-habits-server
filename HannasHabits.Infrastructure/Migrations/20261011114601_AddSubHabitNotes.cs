using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HannasHabits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubHabitNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubHabitNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubHabitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubHabitNotes", x => x.Id);
                    table.CheckConstraint("CK_SubHabitNotes_Date", "\"Date\" BETWEEN DATE '2000-01-01' AND DATE '2100-12-31'");
                    table.CheckConstraint("CK_SubHabitNotes_Text", "char_length(btrim(\"Text\")) BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_SubHabitNotes_SubHabits_SubHabitId",
                        column: x => x.SubHabitId,
                        principalTable: "SubHabits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubHabitNotes_SubHabitId_Date",
                table: "SubHabitNotes",
                columns: new[] { "SubHabitId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubHabitNotes");
        }
    }
}
