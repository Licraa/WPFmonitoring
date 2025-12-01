using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_realtime",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    nilaiA0 = table.Column<int>(type: "int", nullable: false),
                    nilaiTerakhirA2 = table.Column<int>(type: "int", nullable: false),
                    durasiTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    ratarataTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    parthours = table.Column<int>(type: "int", nullable: false),
                    dataCh1 = table.Column<TimeSpan>(type: "time", nullable: false),
                    uptime = table.Column<TimeSpan>(type: "time", nullable: false),
                    p_datach1 = table.Column<int>(type: "int", nullable: false),
                    p_uptime = table.Column<int>(type: "int", nullable: false),
                    last_update = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_realtime", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "line",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineCode = table.Column<int>(type: "int", nullable: false),
                    line = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    line_production = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    process = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    remark = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_line", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_1",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    nilaiA0 = table.Column<int>(type: "int", nullable: false),
                    nilaiTerakhirA2 = table.Column<int>(type: "int", nullable: false),
                    durasiTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    ratarataTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    parthours = table.Column<int>(type: "int", nullable: false),
                    dataCh1 = table.Column<TimeSpan>(type: "time", nullable: false),
                    uptime = table.Column<TimeSpan>(type: "time", nullable: false),
                    p_datach1 = table.Column<int>(type: "int", nullable: false),
                    p_uptime = table.Column<int>(type: "int", nullable: false),
                    last_update = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_1", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_2",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    nilaiA0 = table.Column<int>(type: "int", nullable: false),
                    nilaiTerakhirA2 = table.Column<int>(type: "int", nullable: false),
                    durasiTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    ratarataTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    parthours = table.Column<int>(type: "int", nullable: false),
                    dataCh1 = table.Column<TimeSpan>(type: "time", nullable: false),
                    uptime = table.Column<TimeSpan>(type: "time", nullable: false),
                    p_datach1 = table.Column<int>(type: "int", nullable: false),
                    p_uptime = table.Column<int>(type: "int", nullable: false),
                    last_update = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_2", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_3",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    nilaiA0 = table.Column<int>(type: "int", nullable: false),
                    nilaiTerakhirA2 = table.Column<int>(type: "int", nullable: false),
                    durasiTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    ratarataTerakhirA4 = table.Column<float>(type: "real", nullable: false),
                    parthours = table.Column<int>(type: "int", nullable: false),
                    dataCh1 = table.Column<TimeSpan>(type: "time", nullable: false),
                    uptime = table.Column<TimeSpan>(type: "time", nullable: false),
                    p_datach1 = table.Column<int>(type: "int", nullable: false),
                    p_uptime = table.Column<int>(type: "int", nullable: false),
                    last_update = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_3", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_realtime");

            migrationBuilder.DropTable(
                name: "line");

            migrationBuilder.DropTable(
                name: "shift_1");

            migrationBuilder.DropTable(
                name: "shift_2");

            migrationBuilder.DropTable(
                name: "shift_3");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
