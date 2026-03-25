using Microsoft.EntityFrameworkCore;
using MonitoringApp.Models;

namespace MonitoringApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // 🌟 1. DAFTAR TABEL YANG AKTIF (YANG LAMA SUDAH DIBUANG)
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Line> Lines { get; set; }
        public virtual DbSet<DataRealtime> DataRealtimes { get; set; }
        public virtual DbSet<MachineShiftData> MachineShiftDatas { get; set; }
        public virtual DbSet<DailyUptimeLog> DailyUptimeLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // A. User
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Password).HasColumnName("password").HasMaxLength(255).IsRequired();
                entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20).IsRequired();
                entity.HasData(new User { Id = 1, Username = "admin", Password = "wearesave", Role = "Admin" });
            });

            // B. Line
            modelBuilder.Entity<Line>(entity =>
            {
                entity.ToTable("line");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Line1).HasColumnName("line").HasMaxLength(255);
                entity.Property(e => e.LineProduction).HasColumnName("line_production").HasMaxLength(255);
                entity.Property(e => e.Process).HasColumnName("process").HasMaxLength(255);
                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
                entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(255);
            });

            // C. DataRealtime
            ConfigureShiftTable<DataRealtime>(modelBuilder, "data_realtime");

            // D. MachineShiftData (Tabel 1 Buku Besar Baru)
            modelBuilder.Entity<MachineShiftData>(entity =>
            {
                entity.ToTable("machine_shift_data");
                entity.HasKey(e => new { e.Id, e.ShiftNumber }); // Kunci Gabungan
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.ShiftNumber).HasColumnName("shift_number");
            });

            // 🌟 2. RELASI YANG BERSIH
            modelBuilder.Entity<DataRealtime>()
                .HasOne<Line>().WithOne().HasForeignKey<DataRealtime>(e => e.Id).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DailyUptimeLog>()
                .HasOne(d => d.Line).WithMany().HasForeignKey(d => d.MachineId).OnDelete(DeleteBehavior.Cascade);
        }

        // --- HELPER UNTUK MAPPING KOLOM ---
        private void ConfigureShiftTable<T>(ModelBuilder modelBuilder, string tableName) where T : MachineDataBase
        {
            modelBuilder.Entity<T>(entity =>
            {
                entity.ToTable(tableName);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.HasKey(e => e.Id);
                entity.Property(e => e.NilaiA0).HasColumnName("nilaiA0");
                entity.Property(e => e.NilaiTerakhirA2).HasColumnName("nilaiTerakhirA2");
                entity.Property(e => e.DurasiTerakhirA4).HasColumnName("durasiTerakhirA4");
                entity.Property(e => e.RataRataTerakhirA4).HasColumnName("ratarataTerakhirA4");
                entity.Property(e => e.PartHours).HasColumnName("parthours");
                entity.Property(e => e.DataCh1).HasColumnName("dataCh1");
                entity.Property(e => e.Uptime).HasColumnName("uptime");
                entity.Property(e => e.P_DataCh1).HasColumnName("p_datach1");
                entity.Property(e => e.P_Uptime).HasColumnName("p_uptime");
                entity.Property(e => e.Last_Update).HasColumnName("last_update").HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            });
        }
    }
}