using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HannasHabits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing habits had no schedule: they become "every day" (all seven bits = 127). The default only
            // backfills current rows; the model itself has no database default.
            migrationBuilder.AddColumn<int>(
                name: "Schedule",
                table: "Habits",
                type: "integer",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Habits_Schedule",
                table: "Habits",
                sql: "\"Schedule\" BETWEEN 1 AND 127");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Habits_Schedule",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "Schedule",
                table: "Habits");
        }
    }
}
