using System;
using System.Data.SqlClient;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace MonitoringApp.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public bool TestConnection()
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                return connection.State == System.Data.ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }
    }
}