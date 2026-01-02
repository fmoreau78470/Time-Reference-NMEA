using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TimeReference.Core.Services;

public class NtpVersionService
{
    /// <summary>
    /// Récupère la dernière version disponible sur le site de Meinberg.
    /// </summary>
    public async Task<string?> GetLatestMeinbergVersionAsync()
    {
        string url = "https://www.meinbergglobal.com/english/sw/ntp.htm";
        try
        {
            // Configuration pour ignorer les erreurs SSL (comme le script Python)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
            string content = await client.GetStringAsync(url);
            
            // Recherche des versions type 4.2.8p18
            var regex = new Regex(@"4\.\d+\.\d+p\d+");
            var matches = regex.Matches(content);
            
            var versions = new List<string>();
            foreach (Match match in matches)
            {
                versions.Add(match.Value);
            }
            
            if (versions.Count > 0)
            {
                // Tri intelligent (p10 > p9)
                versions.Sort(CompareNtpVersions);
                return versions.Last();
            }
        }
        catch
        {
            // Echec silencieux (pas de connexion, site changé...)
        }
        return null;
    }

    /// <summary>
    /// Récupère la version locale installée via ntpq.
    /// </summary>
    public string? GetLocalNtpVersion()
    {
        try
        {
            // Essai via le PATH
            string? version = GetNtpVersionFromCommand("ntpq");
            if (version != null) return version;

            // Essai via le chemin par défaut
            string defaultPath = @"C:\Program Files (x86)\NTP\bin\ntpq.exe";
            if (File.Exists(defaultPath))
            {
                return GetNtpVersionFromCommand(defaultPath);
            }
        }
        catch { }
        return null;
    }

    private string? GetNtpVersionFromCommand(string command)
    {
        try
        {
            var psi = new ProcessStartInfo(command, "-c \"rv 0 version\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                
                var regex = new Regex(@"4\.\d+\.\d+p\d+");
                var match = regex.Match(output);
                if (match.Success)
                {
                    return match.Value;
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Compare deux versions NTP (ex: 4.2.8p9 vs 4.2.8p10).
    /// Retourne > 0 si v1 > v2.
    /// </summary>
    public static int CompareNtpVersions(string v1, string v2)
    {
        try 
        {
            var regex = new Regex(@"4\.(\d+)\.(\d+)p(\d+)");
            var m1 = regex.Match(v1);
            var m2 = regex.Match(v2);

            if (m1.Success && m2.Success)
            {
                for (int i = 1; i <= 3; i++)
                {
                    int n1 = int.Parse(m1.Groups[i].Value);
                    int n2 = int.Parse(m2.Groups[i].Value);
                    if (n1 != n2) return n1.CompareTo(n2);
                }
                return 0;
            }
        }
        catch {}
        // Fallback string compare
        return string.Compare(v1, v2, StringComparison.Ordinal);
    }
}