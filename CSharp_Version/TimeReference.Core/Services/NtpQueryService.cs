using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TimeReference.Core.Services
{
    public class NtpQueryService
    {
        public async Task<string> GetClockVarRawAsync()
        {
            // CORRECTION ICI : Utilisation de @ pour que les \ soient traités comme du texte
            string ntpqPath = @"C:\Program Files (x86)\NTP\bin\ntpq.exe";

            // Vérification si le fichier existe, sinon on tente d'autres chemins
            if (!File.Exists(ntpqPath))
            {
                // Cas Windows 64 bits natif ou installation différente
                if (File.Exists(@"C:\Program Files\NTP\bin\ntpq.exe"))
                {
                    ntpqPath = @"C:\Program Files\NTP\bin\ntpq.exe";
                }
                else
                {
                    // Si introuvable, on espère qu'il est dans le PATH système
                    ntpqPath = "ntpq"; 
                }
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ntpqPath,
                    Arguments = "-c clockvar",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();
                    // Lecture asynchrone de la sortie pour ne pas bloquer l'interface
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    return output;
                }
            }
            catch (Exception ex)
            {
                return $"Erreur lors de l'exécution de ntpq : {ex.Message}";
            }
        }

        // Méthode pour transformer la réponse brute en dictionnaire (clé/valeur)
        public Dictionary<string, string> ParseClockVar(string rawOutput)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(rawOutput)) return result;

            // La sortie peut être sur plusieurs lignes
            var lines = rawOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                // Format typique : timecode=..., poll=..., noreply=...
                // On sépare par les virgules
                var parts = line.Split(',');
                foreach (var part in parts)
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2)
                    {
                        result[kv[0].Trim()] = kv[1].Trim();
                    }
                }
            }
            return result;
        }

        // Méthodes de compatibilité pour ClockVarWindow
        public string GetClockVar()
        {
            // Wrapper synchrone pour l'appel asynchrone (évite de bloquer l'UI directement)
            return Task.Run(async () => await GetClockVarRawAsync()).GetAwaiter().GetResult();
        }

        public string ExtractValue(string rawOutput, string key)
        {
            var dict = ParseClockVar(rawOutput);
            return dict.TryGetValue(key, out var val) ? val : "N/A";
        }
    }
}
