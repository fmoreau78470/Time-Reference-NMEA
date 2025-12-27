using System;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App;

public partial class MainWindow : Window
{
    private readonly SerialGpsReader _gpsReader;
    private readonly ConfigService _configService;
    private AppConfig _config;
    private DispatcherTimer _ntpStatusTimer;

    public MainWindow()
    {
        InitializeComponent();
        
        // Chargement de la configuration
        _configService = new ConfigService();
        _config = _configService.Load();
        Logger.Info("Application démarrée. Configuration chargée.");

        // Initialisation de l'interface (remplit le champ texte avec le port sauvegardé)
        TxtPort.Text = _config.SerialPort;

        // On instancie notre lecteur GPS
        _gpsReader = new SerialGpsReader();
        
        // On s'abonne aux événements (quand une donnée arrive ou une erreur survient)
        _gpsReader.GpsDataReceived += OnGpsDataReceived;
        _gpsReader.ErrorOccurred += OnErrorOccurred;

        // Timer pour surveiller l'état du service NTP (Spec 5)
        _ntpStatusTimer = new DispatcherTimer();
        _ntpStatusTimer.Interval = TimeSpan.FromSeconds(0.5);
        _ntpStatusTimer.Tick += (s, e) => UpdateNtpStatus();
        _ntpStatusTimer.Start();
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (!_gpsReader.IsConnected)
        {
            // Mise à jour et sauvegarde de la config avant connexion
            _config.SerialPort = TxtPort.Text;
            _configService.Save(_config);

            // Tentative de connexion
            _gpsReader.Start(_config.SerialPort, _config.BaudRate);
            Logger.Info($"Tentative de connexion au port {_config.SerialPort} à {_config.BaudRate} bauds.");
            
            if (_gpsReader.IsConnected)
            {
                BtnConnect.Content = "Déconnecter";
                LblStatus.Text = "Statut : Connecté (En attente de données...)";
                LblStatus.Foreground = Brushes.Orange;
            }
        }
        else
        {
            // Déconnexion
            _gpsReader.Stop();
            Logger.Info("Déconnexion manuelle.");
            BtnConnect.Content = "Connecter";
            LblStatus.Text = "Statut : Déconnecté";
            LblStatus.Foreground = Brushes.Gray;
        }
    }

    private void BtnGenerateNtp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // On met à jour la config avec la valeur actuelle du champ texte
            _config.SerialPort = TxtPort.Text;
            _configService.Save(_config);

            var ntpService = new NtpService();
            ntpService.GenerateConfFile(_config);

            // Redémarrage du service NTP pour appliquer la config
            WindowsServiceHelper.RestartService("NTP");

            MessageBox.Show($"Fichier généré et service NTP redémarré avec succès :\n{_config.NtpConfPath}", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            Logger.Info("Fichier ntp.conf généré et service redémarré.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur lors de la génération :\n{ex.Message}\n\n(Avez-vous les droits d'admin ?)", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            Logger.Error($"Erreur génération NTP : {ex.Message}");
        }
    }

    // --- GESTION DU SERVICE NTP (Spec 5) ---

    private void UpdateNtpStatus()
    {
        var status = WindowsServiceHelper.GetStatus("NTP");
        
        // Note: Assurez-vous d'avoir ajouté LblNtpStatus dans votre XAML
        if (LblNtpStatus == null) return; 

        if (status == ServiceControllerStatus.Running)
        {
            LblNtpStatus.Text = "Service NTP : Démarré";
            LblNtpStatus.Foreground = Brushes.Green;
        }
        else if (status == ServiceControllerStatus.Stopped)
        {
            LblNtpStatus.Text = "Service NTP : Arrêté";
            LblNtpStatus.Foreground = Brushes.Red;
        }
        else if (status == null)
        {
            LblNtpStatus.Text = "Service NTP : Non trouvé";
            LblNtpStatus.Foreground = Brushes.Gray;
        }
        else
        {
            LblNtpStatus.Text = $"Service NTP : {status}";
            LblNtpStatus.Foreground = Brushes.Orange;
        }
    }

    private void BtnStartNtp_Click(object sender, RoutedEventArgs e)
    {
        try { WindowsServiceHelper.StartService("NTP"); Logger.Info("Demande démarrage NTP"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); Logger.Error(ex.Message); }
    }

    private void BtnStopNtp_Click(object sender, RoutedEventArgs e)
    {
        try { WindowsServiceHelper.StopService("NTP"); Logger.Info("Demande arrêt NTP"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); Logger.Error(ex.Message); }
    }

    private void BtnRestartNtp_Click(object sender, RoutedEventArgs e)
    {
        try { WindowsServiceHelper.RestartService("NTP"); Logger.Info("Demande redémarrage NTP"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); Logger.Error(ex.Message); }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Owner = this;
        
        if (settingsWindow.ShowDialog() == true)
        {
            _config = _configService.Load();
            TxtPort.Text = _config.SerialPort;
            Logger.Info("Paramètres mis à jour.");
        }
    }

    private void BtnLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logWindow = new LogWindow();
            logWindow.Owner = this;
            logWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir les logs :\n{ex.Message}\n\nDétail : {ex.InnerException?.Message}", "Erreur Critique", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnMonitor_Click(object sender, RoutedEventArgs e)
    {
        // Fenêtre non-modale (Show au lieu de ShowDialog) pour pouvoir surveiller en parallèle
        var monitorWindow = new ClockVarWindow();
        monitorWindow.Owner = this;
        monitorWindow.Show();
    }

    private void BtnIqt_Click(object sender, RoutedEventArgs e)
    {
        // Si le lecteur principal est connecté, on le déconnecte pour libérer le port pour IqtWindow
        if (_gpsReader.IsConnected)
        {
            _gpsReader.Stop();
            BtnConnect.Content = "Connecter";
            LblStatus.Text = "Statut : Déconnecté (Requis pour IQT)";
            LblStatus.Foreground = Brushes.Gray;
        }

        var iqtWindow = new IqtWindow(_config);
        iqtWindow.Owner = this;
        iqtWindow.ShowDialog();
    }

    // IMPORTANT : Cet événement est déclenché par le Thread de lecture (arrière-plan).
    // Pour modifier l'interface (UI), il faut passer par le "Dispatcher".
    private void OnGpsDataReceived(GpsData data)
    {
        Dispatcher.Invoke(() =>
        {
            LblRaw.Text = data.RawNmea;

            if (data.IsValid)
            {
                LblStatus.Text = "Statut : Fix GPS OK";
                LblStatus.Foreground = Brushes.Green;
                
                LblTime.Text = data.UtcTime.ToString("HH:mm:ss");
                LblLat.Text = data.Latitude.ToString("F5");
                LblLon.Text = data.Longitude.ToString("F5");
            }
            else
            {
                LblStatus.Text = "Statut : Recherche de satellites...";
                LblStatus.Foreground = Brushes.Orange;
            }
        });
    }

    private void OnErrorOccurred(string message)
    {
        Dispatcher.Invoke(() =>
        {
            Logger.Error($"Erreur GPS : {message}");
            MessageBox.Show(message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Si l'erreur a coupé la connexion, on met à jour l'interface
            if (!_gpsReader.IsConnected)
            {
                BtnConnect.Content = "Connecter";
                LblStatus.Text = "Statut : Erreur";
                LblStatus.Foreground = Brushes.Red;
            }
        });
    }
    
    // Nettoyage propre à la fermeture de la fenêtre
    protected override void OnClosed(EventArgs e)
    {
        _gpsReader.Stop();
        _ntpStatusTimer.Stop();
        Logger.Info("Fermeture de l'application.");
        base.OnClosed(e);
    }
}
