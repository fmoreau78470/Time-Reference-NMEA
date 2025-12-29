using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeReference.Core.Models;

namespace TimeReference.Core.Services
{
    public class LogReaderService
    {
        private readonly string _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        public List<string> GetLogFiles()
        {
            if (!Directory.Exists(_logDir)) return new List<string>();
            return Directory.GetFiles(_logDir, "log_*.txt")
                            .Select(f => Path.GetFileName(f))
                            .OrderByDescending(f => f) // Le plus récent en premier
                            .ToList()!;
        }

        public List<LogEntry> ReadLog(string filename)
        {
            var entries = new List<LogEntry>();
            string path = Path.Combine(_logDir, filename);
            
            if (!File.Exists(path)) return entries;

            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    // Parsing basique du format : YYYY-MM-DD HH:mm:ss [LEVEL] Message
                    // Longueur min : 23 chars
                    if (line.Length > 23 && line[4] == '-' && line[19] == ' ')
                    {
                        var entry = new LogEntry();
                        if (DateTime.TryParse(line.Substring(0, 19), out DateTime dt))
                            entry.Timestamp = dt;

                        int levelEnd = line.IndexOf(']', 21);
                        if (levelEnd > 21)
                        {
                            entry.Level = line.Substring(21, levelEnd - 21);
                            entry.Message = line.Substring(levelEnd + 2).Trim();
                        }
                        else
                        {
                            entry.Message = line.Substring(20);
                        }
                        entries.Add(entry);
                    }
                }
            }
            catch { /* Ignorer les erreurs de lecture */ }

            return entries;
        }

        public void DeleteLog(string filename)
        {
            string path = Path.Combine(_logDir, filename);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}