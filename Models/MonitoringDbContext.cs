using System;
using System.Collections.Generic;
using System.IO; // Wajib ada
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // Wajib ada

namespace MonitoringApp.Models // Pastikan namespace ini sesuai dengan kode kamu sebelumnya
{
    public partial class MonitoringDbContext : DbContext
    {
        public MonitoringDbContext()
        {
        }

        public MonitoringDbContext(DbContextOptions<MonitoringDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<DataRealtime> DataRealtimes { get; set; }
        public virtual DbSet<Line> Lines { get; set; }
        public virtual DbSet<Shift1> Shift1s { get; set; }
        public virtual DbSet<Shift2> Shift2s { get; set; }
        public virtual DbSet<Shift3> Shift3s { get; set; }

        // INI BAGIAN PENTING YANG MEMPERBAIKI CRASH
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Jika belum disetting (artinya aplikasi jalan normal, bukan migrasi)
            if (!optionsBuilder.IsConfigured)
            {
                // Cari file appsettings.json di folder tempat aplikasi berjalan (bin\Debug)
                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // Gunakan BaseDirectory agar aman
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                var configuration = configBuilder.Build();

                // Ambil connection string
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                // Gunakan koneksi tersebut
                optionsBuilder.UseSqlServer(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DataRealtime>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__data_rea__3213E83FED038E83");
                entity.ToTable("data_realtime");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DataCh1).HasColumnName("dataCh1");
                entity.Property(e => e.DurasiTerakhirA4).HasColumnName("durasiTerakhirA4");
                entity.Property(e => e.LastUpdate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("last_update");
                entity.Property(e => e.NilaiA0).HasColumnName("nilaiA0");
                entity.Property(e => e.NilaiTerakhirA2).HasColumnName("nilaiTerakhirA2");
                entity.Property(e => e.PDatach1).HasColumnName("p_datach1");
                entity.Property(e => e.PUptime).HasColumnName("p_uptime");
                entity.Property(e => e.Parthours).HasColumnName("parthours");
                entity.Property(e => e.RatarataTerakhirA4).HasColumnName("ratarataTerakhirA4");
                entity.Property(e => e.Uptime).HasColumnName("uptime");
            });

            modelBuilder.Entity<Line>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__line__3213E83F3F01BB6B");
                entity.ToTable("line");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Line1)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("line");
                entity.Property(e => e.LineProduction)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("line_production");
                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("name");
                entity.Property(e => e.Process)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("process");
                entity.Property(e => e.Remark)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("remark");
            });

            modelBuilder.Entity<Shift1>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__shift_1__3213E83FB6FAFB77");
                entity.ToTable("shift_1");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DataCh1).HasColumnName("dataCh1");
                entity.Property(e => e.DurasiTerakhirA4).HasColumnName("durasiTerakhirA4");
                entity.Property(e => e.LastUpdate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("last_update");
                entity.Property(e => e.NilaiA0).HasColumnName("nilaiA0");
                entity.Property(e => e.NilaiTerakhirA2).HasColumnName("nilaiTerakhirA2");
                entity.Property(e => e.PDatach1).HasColumnName("p_datach1");
                entity.Property(e => e.PUptime).HasColumnName("p_uptime");
                entity.Property(e => e.Parthours).HasColumnName("parthours");
                entity.Property(e => e.RatarataTerakhirA4).HasColumnName("ratarataTerakhirA4");
                entity.Property(e => e.Uptime).HasColumnName("uptime");
            });

            modelBuilder.Entity<Shift2>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__shift_2__3213E83F451A4DBB");
                entity.ToTable("shift_2");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DataCh1).HasColumnName("dataCh1");
                entity.Property(e => e.DurasiTerakhirA4).HasColumnName("durasiTerakhirA4");
                entity.Property(e => e.LastUpdate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("last_update");
                entity.Property(e => e.NilaiA0).HasColumnName("nilaiA0");
                entity.Property(e => e.NilaiTerakhirA2).HasColumnName("nilaiTerakhirA2");
                entity.Property(e => e.PDatach1).HasColumnName("p_datach1");
                entity.Property(e => e.PUptime).HasColumnName("p_uptime");
                entity.Property(e => e.Parthours).HasColumnName("parthours");
                entity.Property(e => e.RatarataTerakhirA4).HasColumnName("ratarataTerakhirA4");
                entity.Property(e => e.Uptime).HasColumnName("uptime");
            });

            modelBuilder.Entity<Shift3>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__shift_3__3213E83F4884078E");
                entity.ToTable("shift_3");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DataCh1).HasColumnName("dataCh1");
                entity.Property(e => e.DurasiTerakhirA4).HasColumnName("durasiTerakhirA4");
                entity.Property(e => e.LastUpdate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("last_update");
                entity.Property(e => e.NilaiA0).HasColumnName("nilaiA0");
                entity.Property(e => e.NilaiTerakhirA2).HasColumnName("nilaiTerakhirA2");
                entity.Property(e => e.PDatach1).HasColumnName("p_datach1");
                entity.Property(e => e.PUptime).HasColumnName("p_uptime");
                entity.Property(e => e.Parthours).HasColumnName("parthours");
                entity.Property(e => e.RatarataTerakhirA4).HasColumnName("ratarataTerakhirA4");
                entity.Property(e => e.Uptime).HasColumnName("uptime");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}