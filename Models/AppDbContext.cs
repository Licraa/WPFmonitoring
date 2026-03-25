using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MonitoringApp.Models;

namespace MonitoringApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // --- 1. DAFTAR TABEL (DbSet) ---
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Line> Lines { get; set; }

        // Tabel Data Mesin
        public virtual DbSet<DataRealtime> DataRealtimes { get; set; }
        public virtual DbSet<Shift1> Shift1s { get; set; }
        public virtual DbSet<Shift2> Shift2s { get; set; }
        public virtual DbSet<Shift3> Shift3s { get; set; }
        public virtual DbSet<MachineShiftData> MachineShiftDatas { get; set; }
        public virtual DbSet<DailyUptimeLog> DailyUptimeLogs { get; set; }

        // --- 2. KONFIGURASI KONEKSI ---
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        // --- 3. MAPPING TABEL ---
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

                entity.HasData(new User
                {
                    Id = 1,
                    Username = "admin",
                    Password = "wearesave", // Password awal (nanti auto-hash)
                    Role = "Admin"
                });

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

            // C. DataRealtime (Menggunakan MachineDataBase sebagai base)
            ConfigureShiftTable<DataRealtime>(modelBuilder, "data_realtime");

            // D. Shift Tables
            ConfigureShiftTable<Shift1>(modelBuilder, "shift_1");
            ConfigureShiftTable<Shift2>(modelBuilder, "shift_2");
            ConfigureShiftTable<Shift3>(modelBuilder, "shift_3");

            // --- TAMBAHKAN RELASI FOREIGN KEY DISINI ---
            // A. Relasi Line ke DataRealtime
            modelBuilder.Entity<DataRealtime>()
                .HasOne<Line>()
                .WithOne()
                .HasForeignKey<DataRealtime>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade); // Otomatis hapus jika Line dihapus

            // B. Relasi Line ke Shift1
            modelBuilder.Entity<Shift1>()
                .HasOne<Line>()
                .WithOne()
                .HasForeignKey<Shift1>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade);

            // C. Relasi Line ke Shift2
            modelBuilder.Entity<Shift2>()
                .HasOne<Line>()
                .WithOne()
                .HasForeignKey<Shift2>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade);

            // D. Relasi Line ke Shift3
            modelBuilder.Entity<Shift3>()
                .HasOne<Line>()
                .WithOne()
                .HasForeignKey<Shift3>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DailyUptimeLog>()
                .HasOne(d => d.Line)             // Log ini punya 1 Line induk
                .WithMany()                      // Tapi Line induk bisa punya Banyak Log
                .HasForeignKey(d => d.MachineId) // Yang jadi pengikatnya adalah MachineId
                .OnDelete(DeleteBehavior.Cascade); // Jika Line dihapus, hapus juga semua log-nya!s

            modelBuilder.Entity<MachineShiftData>(entity =>
            {
                // Nama tabelnya
                entity.ToTable("machine_shift_data");

                // Kunci gabungan
                entity.HasKey(e => new { e.Id, e.ShiftNumber });

                // 🌟 INI OBATNYA: Matikan Auto-Increment secara paksa!
                entity.Property(e => e.Id)
                      .HasColumnName("id")
                      .ValueGeneratedNever();

                entity.Property(e => e.ShiftNumber).HasColumnName("shift_number");

                // (Bawahnya biarkan sama seperti yang Mas Raja tulis sebelumnya)
            });
        }

        // --- HELPER CANGGIH ---
        // Fungsi ini sekarang menggunakan 'MachineDataBase' yang ada di file DataRealtime.cs Anda
        private void ConfigureShiftTable<T>(ModelBuilder modelBuilder, string tableName) where T : MachineDataBase
        {
            modelBuilder.Entity<T>(entity =>
            {
                entity.ToTable(tableName);

                // Karena ID tidak Auto-Increment (ikut tabel Line), kita set Never
                entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

                entity.HasKey(e => e.Id);

                // Mapping nama properti C# (PascalCase) ke Kolom SQL (lowercase/snake_case)
                entity.Property(e => e.NilaiA0).HasColumnName("nilaiA0");
                entity.Property(e => e.NilaiTerakhirA2).HasColumnName("nilaiTerakhirA2");
                entity.Property(e => e.DurasiTerakhirA4).HasColumnName("durasiTerakhirA4");

                // Perhatikan e.RataRata... (R besar di tengah, sesuai DataRealtime.cs Anda)
                entity.Property(e => e.RataRataTerakhirA4).HasColumnName("ratarataTerakhirA4");
                entity.Property(e => e.PartHours).HasColumnName("parthours");

                entity.Property(e => e.DataCh1).HasColumnName("dataCh1");
                entity.Property(e => e.Uptime).HasColumnName("uptime");

                // Perhatikan e.P_... (Underscore, sesuai DataRealtime.cs Anda)
                entity.Property(e => e.P_DataCh1).HasColumnName("p_datach1");
                entity.Property(e => e.P_Uptime).HasColumnName("p_uptime");

                entity.Property(e => e.Last_Update)
                    .HasColumnName("last_update")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");
            });
        }

    }
}