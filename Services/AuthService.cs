using System.Linq;
using MonitoringApp.Data;
using MonitoringApp.Models;

namespace MonitoringApp.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly SecurityHelper _securityHelper; // Pastikan pakai SecurityHelper

        // Constructor Injection
        public AuthService(AppDbContext context, SecurityHelper securityHelper)
        {
            _context = context;
            _securityHelper = securityHelper;
        }

        public string? Login(string username, string password)
        {
            // 1. Cari user
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null) return null;

            // 2. Verifikasi Password
            bool isValid = _securityHelper.VerifyPassword(password, user.Password);

            if (isValid)
            {
                // === LOGIKA UPDATE OTOMATIS (MIGRASI) ===
                // Jika password di DB masih format lama (contoh: "123")
                if (!user.Password.Contains(":"))
                {
                    // Ubah jadi Hash Aman
                    user.Password = _securityHelper.HashPassword(password);

                    // Simpan ke Database
                    _context.SaveChanges();
                }
                // ========================================

                return user.Role;
            }

            return null; // Password Salah
        }
    }
}