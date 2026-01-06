﻿using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Chargement de la configuration pour récupérer la langue
        var configService = new ConfigService();
        var config = configService.Load();

        // Détection d'une nouvelle installation (fichier config.json absent)
        bool isFreshInstall = !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"));
        string? language = config.Language;

        // Si c'est une nouvelle installation OU si la langue n'est pas définie, on regarde le registre
        if (isFreshInstall || string.IsNullOrEmpty(language))
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Time Reference NMEA");
                var installLang = key?.GetValue("InstallLanguage") as string;
                
                if (!string.IsNullOrEmpty(installLang))
                {
                    language = installLang;
                    // IMPORTANT : On sauvegarde cette préférence dans la config pour que MainWindow l'utilise
                    config.Language = language;
                    configService.Save(config);
                }
            }
            catch { }
        }

        // Initialisation de la langue (anglais par défaut si toujours null)
        TranslationManager.Instance.LoadLanguage(language ?? "en");

        base.OnStartup(e);
    }
}
