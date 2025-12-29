using System;
using System.IO;

namespace TimeReference.Core.Services
{
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        public static void Write(string message, string level = "INFO")
        {
            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);

                string filename = $"log_{DateTime.Now:yyyyMMdd}.txt";
                string path = Path.Combine(LogDir, filename);
                // Format : YYYY-MM-DD HH:mm:ss [LEVEL] Message
                string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";

                File.AppendAllText(path, logLine);
            }
            catch 
            { 
                // On ignore les erreurs de log pour ne pas planter l'application principale
            }
        }

        public static void Info(string message) => Write(message, "INFO");
        public static void Error(string message) => Write(message, "ERROR");
        public static void Warning(string message) => Write(message, "WARN");
    }
}