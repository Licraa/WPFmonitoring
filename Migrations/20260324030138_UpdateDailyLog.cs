using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringApp.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDailyLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShiftName",
                table: "TrendChart_log",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TrendChart_log_MachineId",
                table: "TrendChart_log",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrendChart_log_line_MachineId",
                table: "TrendChart_log",
                column: "MachineId",
                principalTable: "line",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrendChart_log_line_MachineId",
                table: "TrendChart_log");

            migrationBuilder.DropIndex(
                name: "IX_TrendChart_log_MachineId",
                table: "TrendChart_log");

            migrationBuilder.DropColumn(
                name: "ShiftName",
                table: "TrendChart_log");
        }
    }
}
