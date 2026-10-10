using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HannasHabits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrenceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Habits_Schedule",
                table: "Habits");

            migrationBuilder.RenameColumn(
                name: "Schedule",
                table: "Habits",
                newName: "ScheduleWeekdays");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduleAnchor",
                table: "Habits",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleDayOfMonth",
                table: "Habits",
                type: "integer",
                nullable: true);

            // Every existing habit is a weekly one, every week (kind 0, interval 1) on the days it already has: the old
            // `Schedule` bit set is the weekday set of that rule, unchanged. The defaults only fill the existing rows -
            // the application always writes both - so they are dropped again below, like the StartDate default.
            migrationBuilder.AddColumn<int>(
                name: "ScheduleInterval",
                table: "Habits",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleKind",
                table: "Habits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("ALTER TABLE \"Habits\" ALTER COLUMN \"ScheduleInterval\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Habits\" ALTER COLUMN \"ScheduleKind\" DROP DEFAULT;");

            migrationBuilder.AddColumn<int>(
                name: "ScheduleOccurrence",
                table: "Habits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Habits_ScheduleAnchor",
                table: "Habits",
                sql: "\"ScheduleAnchor\" IS NULL OR \"ScheduleAnchor\" BETWEEN DATE '2000-01-01' AND DATE '2100-12-31'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Habits_ScheduleRule",
                table: "Habits",
                sql: "(\"ScheduleKind\" = 0 AND \"ScheduleInterval\" BETWEEN 1 AND 52 AND ((\"ScheduleInterval\" = 1) = (\"ScheduleAnchor\" IS NULL)) AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NULL) OR (\"ScheduleKind\" = 1 AND \"ScheduleWeekdays\" = 127 AND \"ScheduleInterval\" BETWEEN 2 AND 365 AND \"ScheduleAnchor\" IS NOT NULL AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NULL) OR (\"ScheduleKind\" = 2 AND (\"ScheduleWeekdays\" & (\"ScheduleWeekdays\" - 1)) = 0 AND \"ScheduleInterval\" = 1 AND \"ScheduleAnchor\" IS NULL AND \"ScheduleOccurrence\" IS NOT NULL AND \"ScheduleOccurrence\" BETWEEN 1 AND 5 AND \"ScheduleDayOfMonth\" IS NULL) OR (\"ScheduleKind\" = 3 AND \"ScheduleWeekdays\" = 127 AND \"ScheduleInterval\" = 1 AND \"ScheduleAnchor\" IS NULL AND \"ScheduleOccurrence\" IS NULL AND \"ScheduleDayOfMonth\" IS NOT NULL AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Habits_ScheduleWeekdays",
                table: "Habits",
                sql: "\"ScheduleWeekdays\" BETWEEN 1 AND 127");
        }

        /// <inheritdoc />
        /// <remarks>
        /// The old schema only knows weekday sets, so a rule that is not "weekly, every week" comes back as the weekdays
        /// it can fall on (every day for "every 3 days" or "the 15th", one weekday for "the last Saturday").
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Habits_ScheduleAnchor",
                table: "Habits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Habits_ScheduleRule",
                table: "Habits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Habits_ScheduleWeekdays",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ScheduleAnchor",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ScheduleDayOfMonth",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ScheduleInterval",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ScheduleKind",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ScheduleOccurrence",
                table: "Habits");

            migrationBuilder.RenameColumn(
                name: "ScheduleWeekdays",
                table: "Habits",
                newName: "Schedule");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Habits_Schedule",
                table: "Habits",
                sql: "\"Schedule\" BETWEEN 1 AND 127");
        }
    }
}
