using System.Linq;
using MonitoringApp.Data;
using MonitoringApp.Models;

namespace MonitoringApp.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService()
        {
            // Inisialisasi Context
            _context = new AppDbContextFactory().CreateDbContext(null);
        }

        public string? Login(string username, string password)
        {
            // LINQ: Mencari user berdasarkan username & password
            // Note: Di production, password harus di-hash, jangan plain text.
            var user = _context.Users
                .FirstOrDefault(u => u.Username == username && u.Password == password);

            return user?.Role; // Kembalikan Role jika user ditemukan, atau null jika tidak
        }
    }
}