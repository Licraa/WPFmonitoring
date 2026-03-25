using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedShiftTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MachineShiftDatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftNumber = table.Column<int>(type: "int", nullable: false),
                    NilaiA0 = table.Column<int>(type: "int", nullable: false),
                    NilaiTerakhirA2 = table.Column<int>(type: "int", nullable: false),
                    DurasiTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    RataRataTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    PartHours = table.Column<int>(type: "int", nullable: false),
                    DataCh1 = table.Column<TimeSpan>(type: "time", nullable: false),
                    Uptime = table.Column<TimeSpan>(type: "time", nullable: false),
                    P_DataCh1 = table.Column<int>(type: "int", nullable: false),
                    P_Uptime = table.Column<int>(type: "int", nullable: false),
                    Last_Update = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineShiftDatas", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MachineShiftDatas");
        }
    }
}
