using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MonitoringApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "MachineCode",
                table: "line",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MachineCode",
                table: "line");

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "password", "role", "username" },
                values: new object[,]
                {
                    { 1, "123", "Admin", "admin" },
                    { 2, "123", "User", "user" }
                });
        }
    }
}
