using System;
using System.Diagnostics;
using System.Globalization;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App;

public partial class MainWindow : Window
{
    private readonly SerialGpsReader _gpsReader = null!;
    private readonly ConfigService _configService = null!;
    private AppConfig _config = null!;
    private DispatcherTimer _ntpStatusTimer = null!;
    private DispatcherTimer _ntpQualityTimer = null!;
    private DispatcherTimer _ntpClockVarTimer = null!;
    private int _lastNoreply = -1;
    private int _lastBadformat = -1;
    private string _lastTimecode = string.Empty;
    private double _healthScore = 100;
    private int _healthCheckCounter = 0;
    private ServiceControllerStatus? _lastNtpStatus = null;
    private bool _expectingNtpStateChange = false;
    private string _currentTheme = "Light";
    private static Mutex? _mutex = null;
    private bool _isMiniMode = false;
    private double _restoreLeft;
    private double _restoreTop;

    public MainWindow()
    {
        // Spec 1 : Instance unique
        const string appName = "TimeReferenceNMEA_SingleInstance";
        bool createdNew;
        _mutex = new Mutex(true, appName, out createdNew);
        if (!createdNew)
        {
            MessageBox.Show("L'application est déjà en cours d'exécution.", "Instance unique", MessageBoxButton.OK, MessageBoxImage.Warning);
            Application.Current.Shutdown();
            return;
        }

        // Splash Screen (Image fixe + Infos)
        var splash = new SplashScreenWindow();
        splash.Show();

        InitializeComponent();

        this.Loaded += Window_Loaded;

        // Chargement de la configuration
        _configService = new ConfigService();
        _config = _configService.Load();

        Logger.Info("=== OUVERTURE DE L'APPLICATION ===");

        // Restauration de l'état (santé et thème)
        LoadAppState();
        // Applique le thème chargé (ou le thème par défaut "Light")
        ChangeTheme(_currentTheme);
        UpdateHealthUI();

        // Initialisation de l'interface (remplit le champ texte avec le port sauvegardé)
        TxtPort.Text = _config.SerialPort;
        ChkUtc.IsChecked = _config.UtcMode;

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

        // Timer pour la qualité NTP (Step 24) - Toutes les 2s car ntpq est lourd
        _ntpQualityTimer = new DispatcherTimer();
        _ntpQualityTimer.Interval = TimeSpan.FromSeconds(1);
        _ntpQualityTimer.Tick += async (s, e) => await UpdateNtpQualityAsync();
        _ntpQualityTimer.Start();

        // Timer pour récupérer les données GPS via NTP (clockvar) quand le port COM est occupé
        _ntpClockVarTimer = new DispatcherTimer();
        _ntpClockVarTimer.Interval = TimeSpan.FromSeconds(1);
        _ntpClockVarTimer.Tick += async (s, e) => await UpdateGpsFromNtpqAsync();
        _ntpClockVarTimer.Start();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Spec 1 : Vérification des prérequis (W32Time vs NTP)
        // Déplacé ici pour permettre l'affichage immédiat de la fenêtre
        await Task.Yield();

        if (!CheckPrerequisites())
        {
            Application.Current.Shutdown();
            return;
        }

        // Tentative de démarrage automatique du service NTP s'il est arrêté
        await AutoStartNtpAsync();
    }

    private async Task AutoStartNtpAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var status = WindowsServiceHelper.GetStatus("NTP");
                if (status == ServiceControllerStatus.Stopped)
                {
                    Dispatcher.Invoke(() => Logger.Info("Service NTP arrêté au démarrage. Tentative de lancement..."));
                    _expectingNtpStateChange = true;
                    WindowsServiceHelper.StartService("NTP");
                }
            });
        }
        catch (Exception ex)
        {
            _expectingNtpStateChange = false;
            Dispatcher.Invoke(() => 
            {
                Logger.Error($"Impossible de démarrer le service NTP au lancement : {ex.Message}");
                MessageBox.Show($"Le service NTP est arrêté et le démarrage automatique a échoué.\n\nErreur : {ex.Message}", "Avertissement NTP", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }
    }

    private bool CheckPrerequisites()
    {
        // 1. Vérifier si W32Time est actif (ce qui suggère que NTP Meinberg n'est pas le maître)
        try
        {
            var w32Status = WindowsServiceHelper.GetStatus("W32Time");
            if (w32Status == ServiceControllerStatus.Running)
            {
                var result = MessageBox.Show(
                    "Le service Windows Time (W32Time) est actif.\n" +
                    "Cela indique que NTP by Meinberg n'est probablement pas installé ou configuré correctement.\n\n" +
                    "Ce logiciel nécessite NTP by Meinberg.\n" +
                    "Voulez-vous télécharger NTP maintenant ?",
                    "Conflit de Service de Temps",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://www.meinbergglobal.com/english/sw/ntp.htm") { UseShellExecute = true });
                }
                return false;
            }
        }
        catch { /* Ignorer si W32Time n'existe pas */ }

        // 2. Vérifier si le service NTP existe
        var ntpStatus = WindowsServiceHelper.GetStatus("NTP");
        if (ntpStatus == null)
        {
            var result = MessageBox.Show(
                "Le service 'NTP' est introuvable.\n" +
                "Ce logiciel nécessite l'installation de NTP by Meinberg.\n\n" +
                "Voulez-vous télécharger NTP maintenant ?",
                "Service NTP Manquant",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo("https://www.meinbergglobal.com/english/sw/ntp.htm") { UseShellExecute = true });
            }
            return false;
        }

        return true;
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        try
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
                    LblStatus.Text = "Connecté (En attente...)";
                    LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor");
                }
            }
            else
            {
                // Déconnexion
                _gpsReader.Stop();
                Logger.Info("Déconnexion manuelle.");
                BtnConnect.Content = "Connecter";
                LblStatus.Text = "Déconnecté";
                LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur de connexion :\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            Logger.Error($"Erreur connexion : {ex.Message}");
        }
    }

    // --- GESTION DU SERVICE NTP (Spec 5) ---

    private void UpdateNtpStatus()
    {
        var status = WindowsServiceHelper.GetStatus("NTP");

        // Log du changement d'état pour traçabilité (évite le spam)
        if (status != _lastNtpStatus)
        {
            string cause = _expectingNtpStateChange ? "Action utilisateur" : "Cause EXTÉRIEURE";
            Logger.Info($"Service NTP : Changement d'état détecté ({_lastNtpStatus?.ToString() ?? "Inconnu"} -> {status?.ToString() ?? "Null"}) [{cause}]");
            _expectingNtpStateChange = false;
            _lastNtpStatus = status;
        }
        
        // Note: Assurez-vous d'avoir ajouté LblNtpStatus dans votre XAML
        if (LblNtpStatus == null) return; 

        if (status == ServiceControllerStatus.Running)
        {
            LblNtpStatus.Text = "Service NTP : Démarré";
            LblNtpStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
        }
        else if (status == ServiceControllerStatus.Stopped)
        {
            LblNtpStatus.Text = "Service NTP : Arrêté";
            LblNtpStatus.SetResourceReference(TextBlock.ForegroundProperty, "ErrorColor");
            InvalidateNtpData();
        }
        else if (status == null)
        {
            LblNtpStatus.Text = "Service NTP : Non trouvé";
            LblNtpStatus.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
            InvalidateNtpData();
        }
        else
        {
            LblNtpStatus.Text = $"Service NTP : {status}";
            LblNtpStatus.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor");
        }
    }

    private void InvalidateNtpData()
    {
        // 1. Santé à 0
        _healthScore = 0;
        UpdateHealthUI();

        // 2. Données ntpq -p (Offset/Jitter/Peers)
        if (LblOffset != null) LblOffset.Text = "-- ms";
        if (LblJitter != null) LblJitter.Text = "-- ms";

        if (PnlNtpPeers != null)
        {
            // Évite le scintillement si le message est déjà affiché
            bool alreadyInvalid = PnlNtpPeers.Children.Count == 1 && PnlNtpPeers.Children[0] is TextBlock tb && tb.Text.Contains("Service arrêté");
            if (!alreadyInvalid)
            {
                PnlNtpPeers.Children.Clear();
                PnlNtpPeers.Children.Add(new TextBlock { Text = "Service arrêté (pas de données)", Foreground = Brushes.Gray, FontStyle = FontStyles.Italic });
            }
        }

        // 3. Données GPS via NTP (si pas de connexion directe)
        if (!_gpsReader.IsConnected)
        {
            if (LblGpsTimeHeader != null) LblGpsTimeHeader.Text = "--:--:--";
            if (LblLat != null) LblLat.Text = "--";
            if (LblLon != null) LblLon.Text = "--";
            if (LblLatDms != null) LblLatDms.Text = "--";
            if (LblLonDms != null) LblLonDms.Text = "--";
            if (LblStatus != null) { LblStatus.Text = "Service NTP arrêté"; LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "ErrorColor"); }
        }
    }

    private void BtnStartNtp_Click(object sender, RoutedEventArgs e)
    {
        try 
        { 
            _expectingNtpStateChange = true;
            Logger.Info("ACTION UTILISATEUR : Demande de DÉMARRAGE du service NTP.");
            WindowsServiceHelper.StartService("NTP"); 
        }
        catch (Exception ex) { _expectingNtpStateChange = false; MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); Logger.Error(ex.Message); }
    }

    private void BtnStopNtp_Click(object sender, RoutedEventArgs e)
    {
        try 
        { 
            _expectingNtpStateChange = true;
            Logger.Info("ACTION UTILISATEUR : Demande d'ARRÊT du service NTP.");
            WindowsServiceHelper.StopService("NTP"); 
        }
        catch (Exception ex) { _expectingNtpStateChange = false; MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); Logger.Error(ex.Message); }
    }

    private void BtnRestartNtp_Click(object sender, RoutedEventArgs e)
    {
        try 
        { 
            _expectingNtpStateChange = true;
            Logger.Info("ACTION UTILISATEUR : Demande de REDÉMARRAGE du service NTP.");
            WindowsServiceHelper.RestartService("NTP"); 
        }
        catch (Exception ex) { _expectingNtpStateChange = false; MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); Logger.Error(ex.Message); }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Ouverture de la fenêtre Paramètres.");
            // Sauvegarde de l'état actuel pour comparaison/restauration
            var oldConfig = _configService.Load();

            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            
            if (settingsWindow.ShowDialog() == true)
            {
                var newConfig = _configService.Load();

                // Détection des changements critiques nécessitant un redémarrage NTP
                if (IsNtpConfigChanged(oldConfig, newConfig))
                {
                    var result = MessageBox.Show(
                        "Des paramètres influençant le service NTP ont été modifiés.\nLe service doit être redémarré pour prendre en compte ces changements.\n\nConfirmer le redémarrage ?",
                        "Redémarrage requis",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _config = newConfig;
                        TxtPort.Text = _config.SerialPort;
                        
                        // Régénération et Redémarrage
                        var ntpService = new NtpService();
                        ntpService.GenerateConfFile(_config);
                        
                        _expectingNtpStateChange = true;
                        WindowsServiceHelper.RestartService("NTP");
                        Logger.Info("Paramètres mis à jour et service NTP redémarré.");
                    }
                    else
                    {
                        // Annulation : On restaure l'ancienne config
                        _configService.Save(oldConfig);
                        _config = oldConfig;
                        TxtPort.Text = _config.SerialPort;
                        Logger.Info("Modification des paramètres annulée par l'utilisateur (Refus de redémarrage).");
                        MessageBox.Show("Les modifications ont été annulées car le service n'a pas été redémarré.", "Annulation", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    _config = newConfig;
                    TxtPort.Text = _config.SerialPort;
                    Logger.Info("Paramètres mis à jour (sans impact NTP).");
                }
            }
            Logger.Info("Fermeture de la fenêtre Paramètres.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir les paramètres :\n{ex.Message}\n\nDétail : {ex.InnerException?.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool IsNtpConfigChanged(AppConfig oldC, AppConfig newC)
    {
        if (oldC.SerialPort != newC.SerialPort) return true;
        if (oldC.BaudRate != newC.BaudRate) return true;
        
        // Comparaison dynamique pour éviter les erreurs si les propriétés diffèrent légèrement
        try {
            // Time2Value (Fudge) : On compare les chaînes pour gérer string ou double
            var t1 = ((dynamic)oldC).Time2Value?.ToString();
            var t2 = ((dynamic)newC).Time2Value?.ToString();
            if (t1 != t2) return true;
            
            // Servers : On compare le contenu des listes
            var list1 = ((dynamic)oldC).Servers as System.Collections.IEnumerable;
            var list2 = ((dynamic)newC).Servers as System.Collections.IEnumerable;

            string str1 = "";
            if (list1 != null) foreach (var item in list1) str1 += item?.ToString() + ",";
            
            string str2 = "";
            if (list2 != null) foreach (var item in list2) str2 += item?.ToString() + ",";

            if (str1 != str2) return true;
        } 
        catch (Exception ex) 
        {
            // En cas d'erreur (propriété manquante, etc.), on force la mise à jour par sécurité
            Logger.Error($"Erreur comparaison config NTP : {ex.Message}");
            return true; 
        }

        return false;
    }

    private void BtnLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Ouverture de la fenêtre de Logs.");
            var logWindow = new LogWindow();
            logWindow.Owner = this;
            logWindow.ShowDialog();
            Logger.Info("Fermeture de la fenêtre de Logs.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir les logs :\n{ex.Message}\n\nDétail : {ex.InnerException?.Message}", "Erreur Critique", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnIqt_Click(object sender, RoutedEventArgs e)
    {
        // Rechargement de la configuration pour être sûr d'avoir les dernières valeurs
        _config = _configService.Load();
        TxtPort.Text = _config.SerialPort;

        // Vérification du service NTP (doit être arrêté pour accès exclusif COM)
        var status = WindowsServiceHelper.GetStatus("NTP");
        bool ntpStoppedByApp = false;

        if (status != ServiceControllerStatus.Stopped && status != null)
        {
            var result = MessageBox.Show(
                "L'analyse de qualité signal nécessite l'accès exclusif au port série.\nLe service NTP doit être arrêté temporairement.\n\nVoulez-vous arrêter le service NTP et continuer ?",
                "Confirmation requise",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try {
                _expectingNtpStateChange = true;
                Logger.Info("ACTION UTILISATEUR : Arrêt du NTP pour analyse IQT.");
                WindowsServiceHelper.StopService("NTP");
                ntpStoppedByApp = true;
            } catch (Exception ex) { _expectingNtpStateChange = false; MessageBox.Show(ex.Message); return; }
        }

        Logger.Info("Ouverture de l'analyse Qualité Signal (IQT).");
        // Si le lecteur principal est connecté, on le déconnecte pour libérer le port pour IqtWindow
        if (_gpsReader.IsConnected)
        {
            _gpsReader.Stop();
            BtnConnect.Content = "Connecter";
            LblStatus.Text = "Déconnecté (Requis pour IQT)";
            LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
        }

        var iqtWindow = new IqtWindow(_config);
        iqtWindow.Owner = this;
        iqtWindow.ShowDialog();
        Logger.Info("Fermeture de l'analyse Qualité Signal (IQT).");

        // Redémarrage automatique si on l'avait arrêté
        if (ntpStoppedByApp)
        {
            try {
                _expectingNtpStateChange = true;
                Logger.Info("ACTION UTILISATEUR : Redémarrage automatique du NTP après IQT.");
                WindowsServiceHelper.StartService("NTP");
            } catch (Exception ex) { 
                _expectingNtpStateChange = false; 
                Logger.Error($"Erreur redémarrage NTP post-IQT : {ex.Message}");
            }
        }
    }

    private void BtnCalibration_Click(object sender, RoutedEventArgs e)
    {
        Logger.Info("Ouverture de l'assistant de Calibration.");
        var choiceWindow = new CalibrationChoiceWindow(_config);
        choiceWindow.Owner = this;
        choiceWindow.ShowDialog();
        Logger.Info("Fermeture de l'assistant de Calibration.");
    }

    private void ChkShowPeers_Click(object sender, RoutedEventArgs e)
    {
        if (GrpPeers == null) return;
        bool show = ChkShowPeers.IsChecked == true;
        
        GrpPeers.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        this.SizeToContent = SizeToContent.Manual;
        this.Height = show ? 450 : 320;
    }

    private void ThemeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Évite de se déclencher pendant l'initialisation de la fenêtre
        if (!this.IsLoaded) return;

        string themeName = (int)e.NewValue switch
        {
            1 => "Dark",
            2 => "Red",
            _ => "Light",
        };
        ChangeTheme(themeName);
    }

    private void ChangeTheme(string theme)
    {
        _currentTheme = theme;
        string uri = $"Themes/{theme}Theme.xaml";
        try
        {
            var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }
        catch (Exception ex) { Logger.Error($"Erreur chargement thème {theme}: {ex.Message}"); }
    }

    private void BtnMiniMode_Click(object sender, RoutedEventArgs e)
    {
        ToggleMiniMode();
    }

    private void ToggleMiniMode()
    {
        _isMiniMode = !_isMiniMode;

        if (_isMiniMode)
        {
            // Sauvegarde de la position avant réduction
            _restoreLeft = this.Left;
            _restoreTop = this.Top;

            // Passage en mode Mini
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            
            // Masquage des éléments non essentiels
            ColClocks.Width = new GridLength(0);
            ColPosition.Width = new GridLength(0);
            GridControls.Visibility = Visibility.Collapsed;
            if (GrpPeers != null) GrpPeers.Visibility = Visibility.Collapsed;

            // Ajustement des marges pour compacité maximale
            MainRootGrid.Margin = new Thickness(0);
            HeaderBorder.Margin = new Thickness(0);

            this.SizeToContent = SizeToContent.WidthAndHeight;
        }
        else
        {
            // Retour en mode Normal
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.ResizeMode = ResizeMode.CanMinimize;
            this.SetResourceReference(BackgroundProperty, "WindowBackground");
            this.Topmost = false;
            
            // Restauration des éléments
            ColClocks.Width = GridLength.Auto;
            ColPosition.Width = GridLength.Auto;
            GridControls.Visibility = Visibility.Visible;
            
            // Restauration des marges
            MainRootGrid.Margin = new Thickness(15);
            HeaderBorder.Margin = new Thickness(0, 0, 0, 15);

            // Restauration de la taille et de la visibilité des pairs
            ChkShowPeers_Click(this, new RoutedEventArgs());
            this.Width = 700;

            // Restauration de la position d'origine
            this.Left = _restoreLeft;
            this.Top = _restoreTop;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isMiniMode && e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_isMiniMode)
        {
            ToggleMiniMode();
        }
    }

    // --- GESTION HORLOGES & QUALITÉ (Step 24) ---

    private void UpdateSystemClock()
    {
        if (LblSysTime == null) return;
        bool isUtc = ChkUtc.IsChecked == true;
        DateTime now = isUtc ? DateTime.UtcNow : DateTime.Now;
        LblSysTime.Text = now.ToString("HH:mm:ss");
    }

    private void ChkUtc_Click(object sender, RoutedEventArgs e)
    {
        // Sauvegarde de la préférence
        _config.UtcMode = ChkUtc.IsChecked == true;
        _configService.Save(_config);

        // Mise à jour immédiate lors du clic
        UpdateSystemClock();
    }

    private async Task UpdateNtpQualityAsync()
    {
        // Exécution de ntpq -p pour récupérer Offset et Jitter
        try
        {
            string output = await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo("ntpq", "-p")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        string res = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();
                        return res;
                    }
                }
                catch { /* Ignorer si ntpq n'est pas trouvé ou erreur */ }
                return "";
            });

            if (string.IsNullOrWhiteSpace(output)) return;

            // Nettoyage de l'affichage précédent
            PnlNtpPeers.Children.Clear();

            // Parsing simple : on cherche la ligne active (* ou o)
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Création de la ligne visuelle
                var tb = new TextBlock { Text = line, FontFamily = new FontFamily("Consolas") };

                // Coloration selon le Tally Code (premier caractère)
                if (line.Contains("remote") && line.Contains("refid"))
                {
                    tb.FontWeight = FontWeights.Bold; // En-tête
                    tb.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
                }
                else if (line.Length > 0)
                {
                    switch (line[0])
                    {
                        case '*': tb.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor"); break;
                        case 'o': tb.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor"); break;
                        case '+': tb.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor"); break;
                        case '-': tb.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor"); break;
                        case 'x': tb.SetResourceReference(TextBlock.ForegroundProperty, "ErrorColor"); break;
                        case '.': tb.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText"); break;
                        default:  tb.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText"); break;
                    }
                }
                PnlNtpPeers.Children.Add(tb);

                // Extraction Offset/Jitter pour la barre de statut
                if (line.StartsWith("*") || line.StartsWith("o"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    // Format standard : remote refid st t when poll reach delay offset jitter
                    // Offset est souvent à l'index 8, Jitter à l'index 9
                    if (parts.Length >= 10)
                    {
                        LblOffset.Text = parts[8] + " ms";
                        LblJitter.Text = parts[9] + " ms";
                    }
                }
            }
        }
        catch { }
    }

    private async Task UpdateGpsFromNtpqAsync()
    {
        // Si on est connecté en direct au port série, on n'utilise pas NTPQ pour l'affichage (priorité au direct)
        if (_gpsReader != null && _gpsReader.IsConnected) return;

        try
        {
            string output = await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo("ntpq", "-c clockvar")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        string res = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();
                        return res;
                    }
                }
                catch { }
                return "";
            });

            if (string.IsNullOrWhiteSpace(output)) return;

            // Extraction du timecode (nécessaire pour le Health Check et l'affichage)
            string currentTimecode = "";
            int startIndex = output.IndexOf("timecode=\"");
            if (startIndex != -1)
            {
                startIndex += 10; // Longueur de timecode="
                int endIndex = output.IndexOf("\"", startIndex);
                if (endIndex != -1)
                {
                    currentTimecode = output.Substring(startIndex, endIndex - startIndex);
                }
            }

            // --- Spec 30 : Indicateur de Santé (Health Check) ---
            // On extrait les compteurs d'erreurs
            int currentNoreply = ExtractNtpValue(output, "noreply=");
            int currentBadformat = ExtractNtpValue(output, "badformat=");

            // Initialisation au premier passage
            if (_lastNoreply == -1)
            {
                _lastNoreply = currentNoreply;
                _lastBadformat = currentBadformat;
                _lastTimecode = currentTimecode;
            }

            _healthCheckCounter++;
            if (_healthCheckCounter >= 10) // Analyse toutes les 10 secondes
            {
                int dNoreply = currentNoreply - _lastNoreply;
                int dBadformat = currentBadformat - _lastBadformat;
                bool isTimecodeFrozen = (currentTimecode == _lastTimecode);

                // Gestion du redémarrage du service (compteurs remis à zéro)
                if (dNoreply < 0) dNoreply = 0;
                if (dBadformat < 0) dBadformat = 0;

                // Calcul du score
                if (isTimecodeFrozen)
                {
                    Logger.Info("WARNING: Santé NTP : Timecode figé détecté (Signal GPS perdu ou pilote bloqué).");
                    // ÉTAT : MORT (La trame NMEA ne change plus, le GPS ou le pilote est figé)
                    _healthScore = 0;
                }
                else
                {
                    if (dNoreply == 0 && dBadformat == 0)
                        _healthScore += 5; // Guérison
                    else
                    {
                        Logger.Info($"WARNING: Santé NTP : Instabilité détectée. NoReply={dNoreply}, BadFormat={dBadformat}");
                        _healthScore -= (dNoreply * 10);
                        _healthScore -= (dBadformat * 25);
                    }
                }

                // Bornage et Mise à jour
                if (_healthScore > 100) _healthScore = 100;
                if (_healthScore < 0) _healthScore = 0;
                UpdateHealthUI();

                // Mémorisation pour le prochain cycle
                _lastNoreply = currentNoreply;
                _lastBadformat = currentBadformat;
                _lastTimecode = currentTimecode;
                _healthCheckCounter = 0;
            }
            // ----------------------------------------------------

            // Affichage (si on a trouvé un timecode)
            if (!string.IsNullOrEmpty(currentTimecode))
            {
                ParseAndDisplayNmea(currentTimecode);
            }
        }
        catch { }
    }

    private int ExtractNtpValue(string output, string key)
    {
        try
        {
            int idx = output.IndexOf(key);
            if (idx != -1)
            {
                idx += key.Length;
                // On lit jusqu'à trouver un caractère qui n'est pas un chiffre
                int endIdx = idx;
                while (endIdx < output.Length && char.IsDigit(output[endIdx]))
                {
                    endIdx++;
                }
                
                if (endIdx > idx)
                {
                    string valStr = output.Substring(idx, endIdx - idx);
                    if (int.TryParse(valStr, out int val)) return val;
                }
            }
        }
        catch { }
        return 0;
    }

    private void UpdateHealthUI()
    {
        if (LblHealth == null) return;
        LblHealth.Text = $"{_healthScore:F0}%";
        
        if (_healthScore > 90) LblHealth.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
        else if (_healthScore > 50) LblHealth.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor");
        else LblHealth.SetResourceReference(TextBlock.ForegroundProperty, "ErrorColor");
    }

    private void ParseAndDisplayNmea(string nmea)
    {
        // Parsing manuel simplifié de GPRMC pour l'affichage
        // $GPRMC,HHMMSS.ss,Status,Lat,N/S,Lon,E/W,Spd,Cog,Date,...
        try
        {
            var parts = nmea.Split(',');
            if (parts.Length > 9 && parts[0] == "$GPRMC")
            {
                // Heure
                string timeStr = parts[1];
                if (timeStr.Length >= 6)
                {
                    // Conversion en DateTime pour gérer le mode UTC/Local
                    if (DateTime.TryParseExact(timeStr.Substring(0, 6), "HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime t))
                    {
                        DateTime now = DateTime.UtcNow;
                        DateTime gpsTime = new DateTime(now.Year, now.Month, now.Day, t.Hour, t.Minute, t.Second, DateTimeKind.Utc);

                        bool isUtc = ChkUtc.IsChecked == true;
                        if (!isUtc) gpsTime = gpsTime.ToLocalTime();
                        
                        string formattedTime = gpsTime.ToString("HH:mm:ss");
                        LblGpsTimeHeader.Text = formattedTime;
                        
                        // Mise à jour de l'horloge système synchronisée
                        UpdateSystemClock();
                    }
                }

                // Position (si valide)
                if (parts[2] == "A")
                {
                    LblStatus.Text = "Fix GPS OK";
                    LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");

                    // Affichage brut des coordonnées (ou conversion si nécessaire)
                    // Correction Bug : Conversion NMEA (DDMM.MMMM) vers Degrés Décimaux (DD.dddd)
                    LblLat.Text = NmeaToDecimal(parts[3], parts[4]);
                    LblLon.Text = NmeaToDecimal(parts[5], parts[6]);
                    LblLatDms.Text = NmeaToDms(parts[3], parts[4]);
                    LblLonDms.Text = NmeaToDms(parts[5], parts[6]);
                }
            }
        }
        catch { }
    }

    private string NmeaToDecimal(string pos, string dir)
    {
        if (string.IsNullOrEmpty(pos) || string.IsNullOrEmpty(dir)) return "--";
        
        if (double.TryParse(pos, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
        {
            // Format NMEA : DDMM.MMMM (Lat) ou DDDMM.MMMM (Lon)
            // On divise par 100 pour séparer les degrés des minutes
            int degrees = (int)(val / 100);
            double minutes = val - (degrees * 100);
            double decimalDeg = degrees + (minutes / 60.0);

            if (dir == "S" || dir == "W") decimalDeg *= -1;

            return string.Format(CultureInfo.InvariantCulture, "{0,11:F5}", decimalDeg);
        }
        return pos;
    }

    private string NmeaToDms(string pos, string dir)
    {
        if (string.IsNullOrEmpty(pos) || string.IsNullOrEmpty(dir)) return "";

        if (double.TryParse(pos, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
        {
            int degrees = (int)(val / 100);
            double minutes = val - (degrees * 100);
            int intMin = (int)minutes;
            double seconds = (minutes - intMin) * 60;

            // On force une largeur de 3 pour TOUS les degrés (Lat et Lon) pour garantir une largeur de chaîne identique
            int padWidth = 3;
            return $"{degrees.ToString().PadLeft(padWidth)}°{intMin:00}'{seconds:00.00}\" {dir}";
        }
        return "";
    }

    private string DecimalToDms(double decimalDeg, bool isLat)
    {
        double absVal = Math.Abs(decimalDeg);
        int degrees = (int)absVal;
        double minutes = (absVal - degrees) * 60;
        int intMin = (int)minutes;
        double seconds = (minutes - intMin) * 60;

        string dir = isLat ? (decimalDeg >= 0 ? "N" : "S") : (decimalDeg >= 0 ? "E" : "W");
        // On force une largeur de 3 pour TOUS les degrés pour aligner parfaitement le bloc de gauche (Décimal)
        int padWidth = 3;
        return $"{degrees.ToString().PadLeft(padWidth)}°{intMin:00}'{seconds:00.00}\" {dir}";
    }

    // IMPORTANT : Cet événement est déclenché par le Thread de lecture (arrière-plan).
    // Pour modifier l'interface (UI), il faut passer par le "Dispatcher".
    private void OnGpsDataReceived(GpsData data)
    {
        Dispatcher.Invoke(() =>
        {
            // Mise à jour de l'horloge GPS du bandeau (Step 24)
            // On affiche toujours l'heure GPS en UTC tel que reçu
            // FIX: On ne met à jour que si l'heure est valide (évite les 00:00:00 sur les trames GSV/GSA)
            if (data.UtcTime != DateTime.MinValue)
            {
                DateTime displayTime = data.UtcTime;
                bool isUtc = ChkUtc.IsChecked == true;
                if (!isUtc) displayTime = displayTime.ToLocalTime();
                
                LblGpsTimeHeader.Text = displayTime.ToString("HH:mm:ss");
                
                // Mise à jour de l'horloge système synchronisée
                UpdateSystemClock();
            }

            if (data.IsValid)
            {
                LblStatus.Text = "Fix GPS OK";
                LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
                
                // FIX: Idem pour l'heure dans le panneau de détails
                if (data.UtcTime != DateTime.MinValue)
                {
                    DateTime displayTime = data.UtcTime;
                    bool isUtc = ChkUtc.IsChecked == true;
                    if (!isUtc) displayTime = displayTime.ToLocalTime();
                }
                LblLat.Text = string.Format(CultureInfo.InvariantCulture, "{0,11:F5}", data.Latitude);
                LblLon.Text = string.Format(CultureInfo.InvariantCulture, "{0,11:F5}", data.Longitude);
                LblLatDms.Text = DecimalToDms(data.Latitude, true);
                LblLonDms.Text = DecimalToDms(data.Longitude, false);
            }
            else
            {
                LblStatus.Text = "Recherche de satellites...";
                LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor");
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
                LblStatus.Text = "Erreur";
                LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "ErrorColor");
            }
        });
    }
    
    // Nettoyage propre à la fermeture de la fenêtre
    protected override void OnClosed(EventArgs e)
    {
        _gpsReader.Stop();
        _ntpStatusTimer.Stop();
        _ntpQualityTimer.Stop();
        _ntpClockVarTimer.Stop();
        
        // Sauvegarde de l'état (Santé) pour le prochain lancement
        SaveAppState();
        
        Logger.Info("Fermeture de l'application.");
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private void LoadAppState()
    {
        try
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appstate.json");
            if (System.IO.File.Exists(path))
            {
                string json = System.IO.File.ReadAllText(path);
                var state = System.Text.Json.JsonSerializer.Deserialize<AppState>(json);
                if (state != null)
                {
                    // On ne restaure que si la fermeture date de moins de 5 minutes
                    if ((DateTime.UtcNow - state.LastExitTime).TotalMinutes < 5)
                    {
                        _healthScore = state.HealthScore;
                        Logger.Info($"État santé restauré : {_healthScore:F1}%");
                    }

                    // Restauration du thème
                    if (!string.IsNullOrEmpty(state.Theme))
                    {
                        _currentTheme = state.Theme;
                        ThemeSlider.Value = _currentTheme switch
                        {
                            "Dark" => 1,
                            "Red" => 2,
                            _ => 0,
                        };
                        Logger.Info($"Thème restauré : {_currentTheme}");
                    }
                }
            }
        }
        catch (Exception ex) { Logger.Error($"Erreur restauration état : {ex.Message}"); }
    }

    private void SaveAppState()
    {
        try
        {
            var state = new AppState { HealthScore = _healthScore, LastExitTime = DateTime.UtcNow, Theme = _currentTheme };
            string json = System.Text.Json.JsonSerializer.Serialize(state);
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appstate.json");
            System.IO.File.WriteAllText(path, json);
        }
        catch (Exception ex) { Logger.Error($"Erreur sauvegarde état : {ex.Message}"); }
    }
}

public class AppState
{
    public double HealthScore { get; set; }
    public DateTime LastExitTime { get; set; }
    public string? Theme { get; set; }
}
