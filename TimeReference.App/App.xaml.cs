﻿using System.Configuration;
using System.Data;
using System.Windows;
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
        base.OnStartup(e);

        // Chargement de la configuration pour récupérer la langue
        var configService = new ConfigService();
        var config = configService.Load();

        string? language = config.Language;

        // Si la langue n'est pas définie dans la config, on tente de récupérer celle de l'installation
        if (string.IsNullOrEmpty(language))
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Time Reference NMEA");
                language = key?.GetValue("InstallLanguage") as string;
            }
            catch { }
        }

        // Initialisation de la langue (anglais par défaut si toujours null)
        TranslationManager.Instance.LoadLanguage(language ?? "en");
    }
}
