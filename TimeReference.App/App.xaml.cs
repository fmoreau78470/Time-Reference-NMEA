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
    public App()
    {
        // Intercepte toutes les erreurs non gérées (UI et non-UI)
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatalError(e.Exception, "Dispatcher");
        e.Handled = true; // Tente d'empêcher le crash immédiat pour laisser le temps de voir le message
        Shutdown();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogFatalError(e.ExceptionObject as Exception, "AppDomain");
    }

    private void LogFatalError(Exception? ex, string source)
    {
        if (ex == null) return;
        string msg = $"[{DateTime.Now}] CRITICAL ERROR ({source}): {ex.Message}\n{ex.StackTrace}";
        try { File.WriteAllText("fatal_error.txt", msg); } catch { }
        MessageBox.Show(msg, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Chargement de la configuration pour récupérer la langue
        var configService = new ConfigService();
        var config = configService.Load();

        string? language = config.Language;

        // On vérifie le registre pour voir si une langue a été choisie lors de l'installation.
        // Cette clé agit comme un "override" unique au premier lancement après une installation/mise à jour.
        try
        {
            // 'true' pour avoir les droits d'écriture (nécessaire pour DeleteValue)
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Time Reference NMEA", true);
            if (key != null)
            {
                var installLang = key.GetValue("InstallLanguage") as string;
                
                if (!string.IsNullOrEmpty(installLang))
                {
                    language = installLang;
                    // IMPORTANT : On sauvegarde cette préférence dans la config pour que MainWindow l'utilise
                    config.Language = language;
                    configService.Save(config);

                    // On supprime la valeur du registre pour ne pas écraser les futurs choix de l'utilisateur
                    key.DeleteValue("InstallLanguage", false);
                }
            }
        }
        catch { }

        // Initialisation de la langue (anglais par défaut si toujours null)
        TranslationManager.Instance.LoadLanguage(language ?? "en");

        base.OnStartup(e);
    }
}
