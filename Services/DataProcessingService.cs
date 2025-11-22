using System;
using System.Collections.Generic;
using System.Globalization;

namespace MonitoringApp.Services
{
    public class DataProcessingService
    {
        private readonly Dictionary<int, (object[] Data, string Name, string Line, string Process)> _localCache = new();

        public void ProcessData(int idKey, int status, int value, float param1, float param2, int param3, float param4, float param5, int param6, int param7)
        {
            var data = new object[] { idKey, status, value, param1, param2, param3, param4, param5, param6, param7 };

            // Jika data berisi 0 semua (selain id), abaikan
            if (IsDataZero(data))
            {
                Console.WriteLine($"ID {idKey}: Data semua 0111, diabaikan.");
                return;
            }

            // Bandingkan dengan cache per ID
            if (_localCache.TryGetValue(idKey, out var cachedEntry))
            {
                if (AreArraysEqual(data, cachedEntry.Data))
                {
                    Console.WriteLine($"ID {idKey}: Data sama, diabaikan.");
                    return;
                }
            }

            // Data berbeda, simpan dan update cache
            _localCache[idKey] = (data, "Unknown", "Unknown", "Unknown");
            Console.WriteLine($"ID {idKey}: Data baru disimpan.");
        }

        private static bool AreArraysEqual(object[] data1, object[] data2)
        {
            if (data1.Length != data2.Length) return false;
            for (int i = 0; i < data1.Length; i++)
            {
                if (!data1[i].Equals(data2[i])) return false;
            }
            return true;
        }

        private static bool IsDataZero(object[] data)
        {
            return Convert.ToDouble(data[3]) == 0 &&
                   Convert.ToDouble(data[4]) == 0 &&
                   Convert.ToInt32(data[5]) == 0 &&
                   Convert.ToDouble(data[6]) == 0;
        }

        private static string FloatToTimeStr(double value)
        {
            TimeSpan time = TimeSpan.FromSeconds(value);
            return time.ToString("hh\\:mm\\:ss", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}