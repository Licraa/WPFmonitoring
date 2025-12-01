using System;
using System.Security.Cryptography;
using System.Text;

namespace MonitoringApp.Services
{
    public static class SecurityHelper
    {
        // Fungsi untuk mengubah password menjadi Hash SHA256
        public static string HashPassword(string rawPassword)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawPassword));
                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Fungsi untuk memverifikasi login
        public static bool VerifyPassword(string inputPassword, string storedHash)
        {
            var inputHash = HashPassword(inputPassword);
            
            return string.Equals(inputHash, storedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}