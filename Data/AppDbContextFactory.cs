using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using MonitoringApp.Models;

namespace MonitoringApp.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();

            // 2. BUILD CONFIGURATION
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 3. AMBIL CONNECTION STRING
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // 4. VALIDASI KONEKSI (Mencegah Silent Failure)
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' tidak ditemukan di appsettings.json. " +
                    "Pastikan file konfigurasi ada dan benar.");
            }

            // 5. KONFIGURASI DB CONTEXT DENGAN RETRY LOGIC
            var builder = new DbContextOptionsBuilder<AppDbContext>();

            builder.UseSqlServer(connectionString, sqlOptions =>
            {
              
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,             // Coba ulang maksimal 5 kali
                    maxRetryDelay: TimeSpan.FromSeconds(10), // Tunggu maksimal 10 detik antar percobaan
                    errorNumbersToAdd: null       // Gunakan list error default SQL Server
                );
                sqlOptions.CommandTimeout(30);
            });

            return new AppDbContext(builder.Options);
        }
    }
}