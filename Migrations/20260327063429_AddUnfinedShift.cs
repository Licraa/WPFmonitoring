using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUnfinedShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shift_1");

            migrationBuilder.DropTable(
                name: "shift_2");

            migrationBuilder.DropTable(
                name: "shift_3");

            migrationBuilder.CreateTable(
                name: "machine_shift_data",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    shift_number = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_machine_shift_data", x => new { x.id, x.shift_number });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_shift_data");

            migrationBuilder.CreateTable(
                name: "shift_1",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    dataCh1 = table.Column<TimeSpan>(type: "time", nullable: false),
                    durasiTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    last_update = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    nilaiA0 = table.Column<int>(type: "int", nullable: false),
                    nilaiTerakhirA2 = table.Column<int>(type: "int", nullable: false),
                    p_datach1 = table.Column<int>(type: "int", nullable: false),
                    p_uptime = table.Column<int>(type: "int", nullable: false),
                    parthours = table.Column<int>(type: "int", nullable: false),
                    ratarataTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    uptime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_1", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_1_line_id",
                        column: x => x.id,
                        principalTable: "line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shift_2",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    dataCh1 = table.Column<TimeSpan>(type: "time", nullable: false),
                    durasiTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    last_update = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    nilaiA0 = table.Column<int>(type: "int", nullable: false),
                    nilaiTerakhirA2 = table.Column<int>(type: "int", nullable: false),
                    p_datach1 = table.Column<int>(type: "int", nullable: false),
                    p_uptime = table.Column<int>(type: "int", nullable: false),
                    parthours = table.Column<int>(type: "int", nullable: false),
                    ratarataTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    uptime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_2", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_2_line_id",
                        column: x => x.id,
                        principalTable: "line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shift_3",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    dataCh1 = table.Column<TimeSpan>(type: "time", nullable: false),
                    durasiTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    last_update = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    nilaiA0 = table.Column<int>(type: "int", nullable: false),
                    nilaiTerakhirA2 = table.Column<int>(type: "int", nullable: false),
                    p_datach1 = table.Column<int>(type: "int", nullable: false),
                    p_uptime = table.Column<int>(type: "int", nullable: false),
                    parthours = table.Column<int>(type: "int", nullable: false),
                    ratarataTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    uptime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_3", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_3_line_id",
                        column: x => x.id,
                        principalTable: "line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
