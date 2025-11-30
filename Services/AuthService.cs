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
            // LINQ: Mencari user
            var user = _context.Users
                .FirstOrDefault(u => u.Username == username && u.Password == password);

            return user?.Role;
        }
    }
}