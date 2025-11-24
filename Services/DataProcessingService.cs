using System;
using System.Collections.Generic;
using System.Globalization; // Wajib untuk CultureInfo
using System.Linq;

namespace MonitoringApp.Services
{
    public class DataProcessingService
    {
        private readonly Dictionary<int, object[]> _localCache = new();

        public struct ProcessedResult
        {
            public bool IsValid;
            public bool IsDuplicate;
            public int IdKey;
            public object[] ParsedData;
            public string ErrorMessage;
        }

        public ProcessedResult ProcessRawData(string rawLine)
        {
            var result = new ProcessedResult { IsValid = false, IsDuplicate = false };

            if (string.IsNullOrWhiteSpace(rawLine)) return result;

            try
            {
                // 1. Split Data
                var tokens = rawLine.Trim().Split(',').Select(t => t.Trim()).ToArray();

                // 2. Validasi Jumlah Data (Harus 10 item)
                if (tokens.Length < 10)
                {
                    result.ErrorMessage = $"Data Incomplete (Got {tokens.Length}, Need 10). Raw: {rawLine}";
                    return result;
                }

                // 3. Parsing Strict (Gunakan CultureInfo.InvariantCulture untuk titik desimal)
                var culture = CultureInfo.InvariantCulture;
                var parsedData = new object[10];

                // PENTING: Gunakan float.Parse dulu baru cast ke int agar "10.00" terbaca sebagai 10
                parsedData[0] = (int)float.Parse(tokens[0], culture); // ID
                parsedData[1] = (int)float.Parse(tokens[1], culture); // NilaiA0
                parsedData[2] = (int)float.Parse(tokens[2], culture); // NilaiTerakhirA2
                parsedData[3] = float.Parse(tokens[3], culture);      // DurasiTerakhirA4
                parsedData[4] = float.Parse(tokens[4], culture);      // RataRataTerakhirA4
                parsedData[5] = (int)float.Parse(tokens[5], culture); // PartHours
                parsedData[6] = float.Parse(tokens[6], culture);      // DataCh1
                parsedData[7] = float.Parse(tokens[7], culture);      // Uptime
                parsedData[8] = (int)float.Parse(tokens[8], culture); // P_DataCh1
                parsedData[9] = (int)float.Parse(tokens[9], culture); // P_Uptime

                // Pastikan tidak ada yang null (Cek manual)
                for (int i = 0; i < 10; i++)
                {
                    if (parsedData[i] == null) throw new Exception($"Data at index {i} is NULL");
                }

                result.IdKey = (int)parsedData[0];
                result.ParsedData = parsedData;

                // 4. Cek Data 0 Semua (Logic Bisnis)
                if (IsDataAllZeros(parsedData))
                {
                    result.ErrorMessage = "Data Zero (Ignored)";
                    return result;
                }

                // 5. Cek Duplikat
                if (_localCache.TryGetValue(result.IdKey, out var cachedData))
                {
                    if (AreArraysEqual(parsedData, cachedData))
                    {
                        result.IsDuplicate = true;
                        result.ErrorMessage = "Duplicate Data";
                        return result;
                    }
                }

                _localCache[result.IdKey] = parsedData;
                result.IsValid = true;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"ParseErr: {ex.Message}";
                return result;
            }
        }

        private static bool IsDataAllZeros(object[] data)
        {
            // Cek index 1 sampai 9
            for (int i = 1; i < data.Length; i++)
            {
                if (data[i] is int valInt && valInt != 0) return false;
                if (data[i] is float valFloat && valFloat != 0) return false;
            }
            return true;
        }

        private static bool AreArraysEqual(object[] data1, object[] data2)
        {
            if (data1.Length != data2.Length) return false;
            for (int i = 0; i < data1.Length; i++)
            {
                if (data1[i] is float f1 && data2[i] is float f2)
                {
                    if (Math.Abs(f1 - f2) > 0.001) return false;
                }
                else if (!data1[i].Equals(data2[i])) return false;
            }
            return true;
        }
    }
}