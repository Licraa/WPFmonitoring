using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using MonitoringApp.Models;

namespace MonitoringApp.Data
{
    // Class ini KHUSUS dipanggil saat kamu mengetik 'dotnet ef migrations...'
    // Ini membuat EF Core mengabaikan App.xaml dan LoginWindow sepenuhnya.
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // 1. Dapatkan folder tempat perintah dijalankan
            var basePath = Directory.GetCurrentDirectory();

            // 2. Coba baca appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true) // optional=true biar gak crash
                .Build();

            // 3. Ambil Connection String
            // Trik: Jika appsettings.json gagal dibaca tool, kita pakai string manual sebagai cadangan
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // GANTI STRING INI SESUAI SQL SERVER KAMU JIKA PERLU
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "Server=localhost;Database=MonitoringDB;Trusted_Connection=True;TrustServerCertificate=True;";
            }

            // 4. Buat Context
            var builder = new DbContextOptionsBuilder<AppDbContext>();
            builder.UseSqlServer(connectionString);

            return new AppDbContext(builder.Options);
        }
    }
}