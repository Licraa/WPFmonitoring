using System.Collections.Generic;
using System.Linq;
using MonitoringApp.Data;
using MonitoringApp.Models;

namespace MonitoringApp.Services
{
    public class UserService
    {
        private readonly AppDbContext _context;
        private readonly SecurityHelper _securityHelper; // 1. Gunakan SecurityHelper

        // 2. Inject SecurityHelper di Constructor
        public UserService(AppDbContext context, SecurityHelper securityHelper)
        {
            _context = context;
            _securityHelper = securityHelper;
        }

        // Ambil semua user
        public List<User> GetAllUsers()
        {
            return _context.Users.OrderBy(u => u.Username).ToList();
        }

        // Tambah User Baru (Auto Hash Password)
        public bool AddUser(string username, string password, string role)
        {
            // 1. Cek Duplikat Username
            // Gunakan ToLower() agar pencarian tidak case-sensitive (opsional tapi disarankan)
            if (_context.Users.Any(u => u.Username == username))
            {
                return false; // Username sudah ada
            }

            // 2. Hash Password menggunakan SecurityHelper
            string passwordHash = _securityHelper.HashPassword(password);

            // 3. Simpan ke Database
            var newUser = new User
            {
                Username = username,
                Password = passwordHash,
                Role = role
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();
            return true;
        }

        // Hapus User
        public void DeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
            }
        }
    }
}