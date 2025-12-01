using System.Linq;
using MonitoringApp.Data;
using MonitoringApp.Models;

namespace MonitoringApp.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        public string? Login(string username, string password)
        {
            // 1. Cari User berdasarkan Username saja dulu
            var user = _context.Users
                .FirstOrDefault(u => u.Username == username);

            if (user == null) return null; // User tidak ditemukan

            // 2. Verifikasi Password menggunakan Helper
            bool isValid = SecurityHelper.VerifyPassword(password, user.Password);

            if (isValid)
            {
                return user.Role;
            }

            return null; // Password salah
        }
    }
}