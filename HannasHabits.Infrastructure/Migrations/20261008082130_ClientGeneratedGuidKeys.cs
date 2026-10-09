using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HannasHabits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClientGeneratedGuidKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change: Ids are Guids assigned by EntityBase, so the model no longer marks them
            // as ValueGeneratedOnAdd. This migration only brings the model snapshot in sync.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
