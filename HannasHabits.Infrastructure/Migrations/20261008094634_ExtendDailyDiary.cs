using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HannasHabits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendDailyDiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orphans first, otherwise the foreign key below cannot be created: diaries whose owner does not exist
            // (the test data of the deleted legacy "Users" table). Entries of real users are kept.
            migrationBuilder.Sql(
                "DELETE FROM \"DailyDiary\" WHERE \"UserId\" NOT IN (SELECT \"Id\" FROM \"AspNetUsers\");");

            // The former required "Text" becomes the optional highlight: renamed (not dropped and added), so existing
            // entries keep their text.
            migrationBuilder.RenameColumn(
                name: "Text",
                table: "DailyDiary",
                newName: "Highlight");

            migrationBuilder.AlterColumn<string>(
                name: "Highlight",
                table: "DailyDiary",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<int>(
                name: "Mood",
                table: "DailyDiary",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Body",
                table: "DailyDiary",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Mind",
                table: "DailyDiary",
                type: "integer",
                nullable: true);

            // The defaults only backfill current rows ("no entries" / "no tasks"); the model itself has no database default.
            migrationBuilder.AddColumn<string[]>(
                name: "Grateful",
                table: "DailyDiary",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string[]>(
                name: "Learned",
                table: "DailyDiary",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "Tasks",
                table: "DailyDiary",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyDiary_Body",
                table: "DailyDiary",
                sql: "\"Body\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyDiary_Mind",
                table: "DailyDiary",
                sql: "\"Mind\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyDiary_Mood",
                table: "DailyDiary",
                sql: "\"Mood\" BETWEEN 1 AND 5");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyDiary_AspNetUsers_UserId",
                table: "DailyDiary",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyDiary_AspNetUsers_UserId",
                table: "DailyDiary");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyDiary_Body",
                table: "DailyDiary");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyDiary_Mind",
                table: "DailyDiary");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyDiary_Mood",
                table: "DailyDiary");

            migrationBuilder.DropColumn(
                name: "Tasks",
                table: "DailyDiary");

            migrationBuilder.DropColumn(
                name: "Learned",
                table: "DailyDiary");

            migrationBuilder.DropColumn(
                name: "Grateful",
                table: "DailyDiary");

            migrationBuilder.DropColumn(
                name: "Mind",
                table: "DailyDiary");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "DailyDiary");

            migrationBuilder.DropColumn(
                name: "Mood",
                table: "DailyDiary");

            // The old model required a text and allowed 2000 characters: entries without a highlight get an empty
            // text and a longer highlight is cut, everything else (mood, lists, tasks) is lost with the columns above.
            migrationBuilder.Sql(
                "UPDATE \"DailyDiary\" SET \"Highlight\" = LEFT(COALESCE(\"Highlight\", ''), 2000);");

            migrationBuilder.AlterColumn<string>(
                name: "Highlight",
                table: "DailyDiary",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "Highlight",
                table: "DailyDiary",
                newName: "Text");
        }
    }
}
