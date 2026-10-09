using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HannasHabits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // The one name people typed ("Hanna Müller") becomes first name = its first word, last name = the rest
            // ("Anna Maria Schmidt" -> "Anna" / "Maria Schmidt"); a single word is only a first name. EF would have
            // guessed a rename of the column here, which would have put the whole name into the last name.
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET "FirstName" = substring("DisplayName" from '^\S+'),
                    "LastName" = NULLIF(btrim(regexp_replace("DisplayName", '^\S+\s*', '')), '')
                WHERE btrim("DisplayName") <> '';
                """);

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Both names back into the one; the column is 100 characters, so a long pair is cut.
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET "DisplayName" = NULLIF(left(btrim(concat_ws(' ', "FirstName", "LastName")), 100), '');
                """);

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "AspNetUsers");
        }
    }
}
