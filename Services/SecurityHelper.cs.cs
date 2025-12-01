using System;
using System.Security.Cryptography;
using System.Text;

namespace MonitoringApp.Services
{
    public class SecurityHelper // Pastikan nama classnya ini
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
        private const char Delimiter = ':';

        public string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);
            return $"{Convert.ToBase64String(salt)}{Delimiter}{Convert.ToBase64String(hash)}";
        }

        public bool VerifyPassword(string passwordInput, string passwordHashDb)
        {
            var parts = passwordHashDb.Split(Delimiter);

            // 1. Cek Format Hash Baru
            if (parts.Length == 2)
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] hash = Convert.FromBase64String(parts[1]);
                byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(passwordInput, salt, Iterations, Algorithm, HashSize);
                return CryptographicOperations.FixedTimeEquals(hash, inputHash);
            }

            // 2. Cek Plain Text (Untuk password "123")
            if (passwordInput == passwordHashDb)
            {
                return true;
            }

            // 3. Cek Format SQL Hex (Opsional, jaga-jaga)
            using (var sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(passwordInput);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);
                string inputHashHex = BitConverter.ToString(hashBytes).Replace("-", "");
                if (inputHashHex.Equals(passwordHashDb, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}