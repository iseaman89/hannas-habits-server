using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HannasHabits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotaRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive: the new column is NULL for every existing habit, which is what the four older kinds require.
            migrationBuilder.DropCheckConstraint(
                name: "CK_Habits_ScheduleRule",
                table: "Habits");

            migrationBuilder.AddColumn<int>(
                name: "ScheduleTimes",
                table: "Habits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Habits_ScheduleRule",
                table: "Habits",
                sql: "(\"ScheduleKind\" = 0 AND \"ScheduleInterval\" BETWEEN 1 AND 52 AND ((\"ScheduleInterval\" = 1) = (\"ScheduleAnchor\" IS NULL)) AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleTimes\" IS NULL) OR (\"ScheduleKind\" = 1 AND \"ScheduleWeekdays\" = 127 AND \"ScheduleInterval\" BETWEEN 2 AND 365 AND \"ScheduleAnchor\" IS NOT NULL AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleTimes\" IS NULL) OR (\"ScheduleKind\" = 2 AND (\"ScheduleWeekdays\" & (\"ScheduleWeekdays\" - 1)) = 0 AND \"ScheduleInterval\" = 1 AND \"ScheduleAnchor\" IS NULL AND \"ScheduleOccurrence\" IS NOT NULL AND \"ScheduleOccurrence\" BETWEEN 1 AND 5 AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleTimes\" IS NULL) OR (\"ScheduleKind\" = 3 AND \"ScheduleWeekdays\" = 127 AND \"ScheduleInterval\" = 1 AND \"ScheduleAnchor\" IS NULL AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NOT NULL AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleTimes\" IS NULL) OR (\"ScheduleKind\" = 4 AND \"ScheduleWeekdays\" = 127 AND \"ScheduleInterval\" = 1 AND \"ScheduleAnchor\" IS NULL AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleTimes\" IS NOT NULL AND \"ScheduleTimes\" BETWEEN 1 AND 7) OR (\"ScheduleKind\" = 5 AND \"ScheduleWeekdays\" = 127 AND \"ScheduleInterval\" = 1 AND \"ScheduleAnchor\" IS NULL AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleTimes\" IS NOT NULL AND \"ScheduleTimes\" BETWEEN 1 AND 28)");
        }

        /// <inheritdoc />
        /// <remarks>
        /// The old schema has no quota, so a "N times a week / month" habit comes back as a daily one - the days it can
        /// fall on are all of them. The count is the only thing lost.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Habits_ScheduleRule",
                table: "Habits");

            // Kind 4 (times a week) and 5 (times a month); their weekday set, interval and the other facts already
            // are "every day" / 1 / NULL, so changing the kind is all it takes to satisfy the old constraint.
            migrationBuilder.Sql("UPDATE \"Habits\" SET \"ScheduleKind\" = 0 WHERE \"ScheduleKind\" IN (4, 5);");

            migrationBuilder.DropColumn(
                name: "ScheduleTimes",
                table: "Habits");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Habits_ScheduleRule",
                table: "Habits",
                sql: "(\"ScheduleKind\" = 0 AND \"ScheduleInterval\" BETWEEN 1 AND 52 AND ((\"ScheduleInterval\" = 1) = (\"ScheduleAnchor\" IS NULL)) AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NULL) OR (\"ScheduleKind\" = 1 AND \"ScheduleWeekdays\" = 127 AND \"ScheduleInterval\" BETWEEN 2 AND 365 AND \"ScheduleAnchor\" IS NOT NULL AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NULL) OR (\"ScheduleKind\" = 2 AND (\"ScheduleWeekdays\" & (\"ScheduleWeekdays\" - 1)) = 0 AND \"ScheduleInterval\" = 1 AND \"ScheduleAnchor\" IS NULL AND \"ScheduleOccurrence\" IS NOT NULL AND \"ScheduleOccurrence\" BETWEEN 1 AND 5 AND \"ScheduleDayOfMonth\" IS NULL) OR (\"ScheduleKind\" = 3 AND \"ScheduleWeekdays\" = 127 AND \"ScheduleInterval\" = 1 AND \"ScheduleAnchor\" IS NULL AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NOT NULL AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31)");
        }
    }
}
