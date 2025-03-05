using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class _2Migration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "30a24107-d279-4e37-96fd-01af5b38cb27",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "feea1358-7444-4540-af15-38ed59fb2888", "AQAAAAIAAYagAAAAEKwoZxDIda0ekGtv++72VlzJszLZYdd9msKGIgHNwRaaI1VY93clw7ChJ65gBLC5dw==", "a6f2fe05-279d-4b37-96a7-380cd097d67f" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8e448afa-f008-446e-a52f-13c449803c2e",
                columns: new[] { "ConcurrencyStamp", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "SecurityStamp" },
                values: new object[] { "e1cce68a-9931-49e7-890a-0e7d229f9b82", "ADMIN@TMS.COM", null, "AQAAAAIAAYagAAAAEMC6vhK9c1TYV1oZLbVB1LNb/w7IYs/n3a/p+JtKQlfO9uPpUFTSVpcP1qTTGYwuKQ==", "03646e01-db27-4b02-bf95-11dbca9c345e" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "30a24107-d279-4e37-96fd-01af5b38cb27",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "4a46eed0-924e-4b73-9e0e-033a4bf65a3a", "AQAAAAIAAYagAAAAEI62XYMyvM7Vy35OH3yJ97AlH1Wte4ajK5y3K+Jxf+6MvonYpUy0ZIogW64d5cqVCQ==", "164c0bdc-ff69-4974-8a32-0d7226c98b5b" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8e448afa-f008-446e-a52f-13c449803c2e",
                columns: new[] { "ConcurrencyStamp", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "SecurityStamp" },
                values: new object[] { "82448558-64b3-4ecc-85d9-aa9447f944d8", null, "ADMINISTRATOR", "AQAAAAIAAYagAAAAEJ58qSMt/2UG2beps1AGmHEqYURBGJsnPa2KPLjld6LtQjVce/6WZKUZC6wAuQ1XxQ==", "55c94067-f212-45f5-82f8-e30fed6357f1" });
        }
    }
}
