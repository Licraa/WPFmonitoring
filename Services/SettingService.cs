using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MonitoringApp.Models;

namespace MonitoringApp.Services
{
    public class SettingService
    {
        private readonly string _configPath;
        private AppSettingsModel _currentSettings;

        public SettingService()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            _currentSettings = LoadSettings();
        }

        public AppSettingsModel GetSettings() => _currentSettings;

        private AppSettingsModel LoadSettings()
        {
            if (!File.Exists(_configPath)) return new AppSettingsModel();
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppSettingsModel>(json) ?? new AppSettingsModel();
        }

        public bool TestDatabaseConnection(string connectionString)
        {
            try
            {
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();
                    return true;
                }
            }
            catch { return false; }
        }

        public void SaveSettings(AppSettingsModel newSettings)
        {
            try
            {
                _currentSettings = newSettings; // Update cache di memori

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // Agar file JSON mudah dibaca manusia
                };

                string json = JsonSerializer.Serialize(newSettings, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception("Gagal menulis ke appsettings.json: " + ex.Message);
            }
        }
    }
}