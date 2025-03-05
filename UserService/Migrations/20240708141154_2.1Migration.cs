using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class _21Migration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "30a24107-d279-4e37-96fd-01af5b38cb27",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "b090802d-e607-4ad4-b432-887b36c7b001", "AQAAAAIAAYagAAAAEKfgfzebDbPnQF/1/psrL9mszlVBAlM5nSZ6d8Xlt+K/ucq5NhQVp5b8qlUuHeivNw==", "9fce24e8-47c7-48c3-b4c4-82d2e1484b95" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8e448afa-f008-446e-a52f-13c449803c2e",
                columns: new[] { "ConcurrencyStamp", "NormalizedUserName", "PasswordHash", "SecurityStamp", "UserName" },
                values: new object[] { "0838bb7b-71ef-47f7-9efa-01bdcaa61f30", "ADMIN@TMS.COM", "AQAAAAIAAYagAAAAEC0IQBfQcI64Wn32JHhvdgdlJ678mjetlw4oH5BC3piVlmZMNf3fs8rfULXQHYbQOw==", "b37405d8-3a90-4fb4-8be3-1622e6136b9f", "admin@tms.com" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                columns: new[] { "ConcurrencyStamp", "NormalizedUserName", "PasswordHash", "SecurityStamp", "UserName" },
                values: new object[] { "e1cce68a-9931-49e7-890a-0e7d229f9b82", null, "AQAAAAIAAYagAAAAEMC6vhK9c1TYV1oZLbVB1LNb/w7IYs/n3a/p+JtKQlfO9uPpUFTSVpcP1qTTGYwuKQ==", "03646e01-db27-4b02-bf95-11dbca9c345e", null });
        }
    }
}
