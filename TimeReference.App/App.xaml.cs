﻿﻿﻿using System.Configuration;
using System.Data;
using System.Windows;
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

        // Initialisation de la langue (fr par défaut si null)
        TranslationManager.Instance.LoadLanguage(config.Language ?? "fr");
    }
}
