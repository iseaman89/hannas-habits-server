using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HannasHabits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Habits",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Existing habits start on the (UTC) day they were created - the closest fact there is. The column default
            // only existed to add the NOT NULL column; it is not a valid start date (the check below rejects it), and
            // the application always sends the value.
            migrationBuilder.Sql("UPDATE \"Habits\" SET \"StartDate\" = (\"CreatedAt\" AT TIME ZONE 'UTC')::date;");
            migrationBuilder.Sql("ALTER TABLE \"Habits\" ALTER COLUMN \"StartDate\" DROP DEFAULT;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Habits_StartDate",
                table: "Habits",
                sql: "\"StartDate\" BETWEEN DATE '2000-01-01' AND DATE '2100-12-31'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Habits_StartDate",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Habits");
        }
    }
}
