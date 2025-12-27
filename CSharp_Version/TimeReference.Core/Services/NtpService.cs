using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using TimeReference.Core.Models;

namespace TimeReference.Core.Services;

public class NtpService
{
    /// <summary>
    /// Génère le fichier ntp.conf à partir de la configuration actuelle.
    /// </summary>
    public void GenerateConfFile(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.NtpConfPath))
        {
            return;
        }

        // 1. Chargement du Template
        // On cherche d'abord à côté de l'exécutable
        string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ntp.template");
        
        if (!File.Exists(templatePath))
        {
            // Fallback : Recherche dans les dossiers parents (Mode Dev)
            // ../../../ = TimeReference.App (Projet)
            // ../../../../ = CSharp_Version (Solution)
            // ../../../../../ = Racine du Repo
            string[] searchUps = { "../../../", "../../../../", "../../../../../" };
            bool found = false;
            foreach (var up in searchUps)
            {
                string tryPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, up + "ntp.template"));
                if (File.Exists(tryPath))
                {
                    templatePath = tryPath;
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                throw new FileNotFoundException($"Le fichier modèle 'ntp.template' est introuvable.\nVeuillez le copier dans : {AppDomain.CurrentDomain.BaseDirectory}");
            }
        }

        string templateContent = File.ReadAllText(templatePath);

        // 2. Calcul des variables
        int portNumber = ExtractPortNumber(config.SerialPort);
        
        int mode = 1; // Base 4800
        if (config.BaudRate == 9600) mode = 17;
        else if (config.BaudRate == 19200) mode = 33;
        else if (config.BaudRate == 38400) mode = 49;
        else if (config.BaudRate == 57600) mode = 65;
        else if (config.BaudRate == 115200) mode = 81;

        // Génération du bloc serveurs
        var sbServers = new StringBuilder();
        if (config.Servers != null)
        {
            foreach (var server in config.Servers)
            {
                string options = !string.IsNullOrWhiteSpace(config.ServerOptions) ? config.ServerOptions : "iburst";
                sbServers.AppendLine($"server {server} {options}");
            }
        }

        // 3. Remplacement des balises
        string finalContent = templateContent
            .Replace("{{ COM_PORT }}", portNumber.ToString())
            .Replace("{{ MODE }}", mode.ToString())
            .Replace("{{ TIME2_VALUE }}", config.Time2Value.ToString("F4", CultureInfo.InvariantCulture))
            .Replace("{{ SERVERS_BLOCK }}", sbServers.ToString());

        try
        {
            // Création du dossier si nécessaire (utile pour les tests en local)
            string? dir = Path.GetDirectoryName(config.NtpConfPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(config.NtpConfPath, finalContent);
        }
        catch (Exception ex)
        {
            // Note : Écrire dans Program Files nécessite les droits Admin.
            throw new IOException($"Erreur lors de l'écriture de {config.NtpConfPath}. Vérifiez les droits d'accès.", ex);
        }
    }

    private int ExtractPortNumber(string portName)
    {
        // Extrait "3" de "COM3"
        var match = Regex.Match(portName, @"\d+");
        if (match.Success && int.TryParse(match.Value, out int port))
        {
            return port;
        }
        return 1; // Fallback
    }
}
