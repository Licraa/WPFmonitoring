using System;
using Microsoft.Data.SqlClient; // Pastikan pakai Microsoft.Data.SqlClient sesuai setup sebelumnya

namespace MonitoringApp.Services
{
    public class AuthService
    {
        private readonly DatabaseService _dbService;

        public AuthService()
        {
            _dbService = new DatabaseService();
        }

        /// <summary>
        /// Cek login. Mengembalikan string Role ('Admin'/'User') jika sukses, atau null jika gagal.
        /// </summary>
        public string? Login(string username, string password)
        {
            using (var conn = _dbService.GetConnection())
            {
                try
                {
                    conn.Open();
                    string query = "SELECT Role FROM Users WHERE Username = @user AND Password = @pass";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", username);
                        cmd.Parameters.AddWithValue("@pass", password); // Di real app, hash password dulu di sini

                        var result = cmd.ExecuteScalar();

                        if (result != null)
                        {
                            return result.ToString(); // Kembalikan Role (misal: "Admin")
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Login Error: " + ex.Message);
                }
            }
            return null; // Login gagal
        }
    }
}