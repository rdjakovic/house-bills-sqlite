using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseBills.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimatedAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AmountVaries",
                table: "RecurringBills",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEstimated",
                table: "Bills",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountVaries",
                table: "RecurringBills");

            migrationBuilder.DropColumn(
                name: "IsEstimated",
                table: "Bills");
        }
    }
}
