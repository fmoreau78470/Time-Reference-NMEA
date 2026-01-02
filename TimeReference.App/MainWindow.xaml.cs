using System;
using System.Diagnostics;
using System.Globalization;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
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
    private NtpStatusModel _ntpStatus = new NtpStatusModel();
    private NtpStatusModel? _previousNtpStatus = null;
    private int _consecutivePoorHealth = 0;
    private DateTime _lastAutoRestart = DateTime.MinValue;
    private double _healthScore = 100;
    private int _healthCheckCounter = 0;
    private ServiceControllerStatus? _lastNtpStatus = null;
    private bool _expectingNtpStateChange = false;
    private string _currentTheme = "Light";
    private static Mutex? _mutex = null;
    private DateTime? _lastGpsUtcTime;
    private bool _isMiniMode = false;
    private bool _shouldStartInMiniMode = false;
    private bool _isGpsActivePeer = false;
    private bool _hasActivePeer = false;
    private double _restoreLeft;
    private double _restoreTop;
    private double _restoreWidth;
    private double _restoreHeight;
    private DateTime _lastClockUpdate = DateTime.MinValue;
    private PeersWindow? _peersWindow = null;

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    
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
        var splash = new SplashScreenWindow(true);
        splash.Show();

        InitializeComponent();
        this.Loaded += Window_Loaded;
        this.SizeChanged += MainWindow_SizeChanged;

        // Gestion de la transparence pour le mode mini
        this.MouseEnter += (s, e) => { if (_isMiniMode) this.Opacity = 1.0; };
        this.MouseLeave += (s, e) => { if (_isMiniMode) this.Opacity = _config.MiniModeOpacity > 0.1 ? _config.MiniModeOpacity : 1.0; };

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
        UpdateUtcButtonAppearance();

        // On instancie notre lecteur GPS
        _gpsReader = new SerialGpsReader();
        
        // On s'abonne aux événements (quand une donnée arrive ou une erreur survient)
        _gpsReader.GpsDataReceived += OnGpsDataReceived;
        _gpsReader.ErrorOccurred += OnErrorOccurred;

        // Timer pour surveiller l'état du service NTP (Spec 5)
        _ntpStatusTimer = new DispatcherTimer();
        _ntpStatusTimer.Interval = TimeSpan.FromSeconds(0.5);
        _ntpStatusTimer.Tick += (s, e) => 
        {
            UpdateNtpStatus();
            // Fallback : Si pas de mise à jour par GPS depuis > 1.2s, on met à jour l'horloge système
            if ((DateTime.Now - _lastClockUpdate).TotalSeconds > 1.2)
            {
                UpdateSystemClock();
            }
        };
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

        // Restauration du mode Mini si nécessaire
        if (_shouldStartInMiniMode)
        {
            ToggleMiniMode();
        }

        // Application du réglage "Toujours au premier plan" après le chargement
        this.Topmost = _config.MiniModeAlwaysOnTop;
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

    private async void BtnRestartNtp_Click(object sender, RoutedEventArgs e)
    {
        try 
        { 
            _expectingNtpStateChange = true;
            Logger.Info("ACTION UTILISATEUR : Demande de REDÉMARRAGE du service NTP.");
            
            BtnRestartNtp.IsEnabled = false;

            await Task.Run(() => WindowsServiceHelper.RestartService("NTP")); 
        }
        catch (Exception ex) { _expectingNtpStateChange = false; MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); Logger.Error(ex.Message); }
        finally
        {
            BtnRestartNtp.IsEnabled = true;
        }
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
                        Logger.Info("Modification des paramètres annulée par l'utilisateur (Refus de redémarrage).");
                        MessageBox.Show("Les modifications ont été annulées car le service n'a pas été redémarré.", "Annulation", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    _config = newConfig;
                    Logger.Info("Paramètres mis à jour (sans impact NTP).");
                }

                // Application immédiate du paramètre "Toujours au premier plan"
                this.Topmost = _config.MiniModeAlwaysOnTop;
                if (_peersWindow != null) _peersWindow.Topmost = _config.MiniModeAlwaysOnTop;
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

    private async void BtnPeers_Click(object sender, RoutedEventArgs e)
    {
        if (_peersWindow == null)
        {
            _peersWindow = new PeersWindow(_config);
            _peersWindow.Owner = this;
            _peersWindow.Topmost = _config.MiniModeAlwaysOnTop;

            // Restaurer la position si elle a été sauvegardée
            if (_config.PeersWindowLeft != -1 && _config.PeersWindowTop != -1)
            {
                _peersWindow.Left = _config.PeersWindowLeft;
                _peersWindow.Top = _config.PeersWindowTop;
            }

            _peersWindow.Closed += (s, args) =>
            {
                // Mémoriser la position à la fermeture
                if (s is Window closedWindow)
                {
                    _config.PeersWindowLeft = closedWindow.Left;
                    _config.PeersWindowTop = closedWindow.Top;
                    _configService.Save(_config);
                }
                _peersWindow = null;
            };
            _peersWindow.Show();

            // Forcer une mise à jour immédiate des données (évite d'attendre le timer)
            await UpdateNtpQualityAsync();
        }
        else
        {
            _peersWindow.Close();
            _peersWindow = null;
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

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new SplashScreenWindow(false);
        aboutWindow.Owner = this;
        aboutWindow.ShowDialog();
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        string nextTheme = _currentTheme switch
        {
            "Light" => "Dark",
            "Dark" => "Red",
            "Red" => "Light",
            _ => "Light"
        };
        ChangeTheme(nextTheme);
    }

    private void ChkShowPeers_Click(object sender, RoutedEventArgs e)
    {
        // Cette fonctionnalité est obsolète avec la nouvelle interface
    }

    private void ThemeIcon_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string theme)
        {
            ChangeTheme(theme);
        }
    }

    private void ChangeTheme(string theme)
    {
        if (!IsLoaded)
        {
            ApplyThemeResources(theme);
            return;
        }

        // Animation de transition fluide
        var fadeOut = new DoubleAnimation(0.3, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (s, e) =>
        {
            ApplyThemeResources(theme);
            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150));
            this.BeginAnimation(OpacityProperty, fadeIn);

            // Synchronisation de la transition avec la fenêtre Peers
            if (_peersWindow != null && _peersWindow.IsLoaded)
            {
                double targetOpacity = _config.MiniModeOpacity > 0.1 ? _config.MiniModeOpacity : 1.0;
                var peerFadeIn = new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(200));
                peerFadeIn.Completed += (s2, e2) => _peersWindow.BeginAnimation(OpacityProperty, null);
                _peersWindow.BeginAnimation(OpacityProperty, peerFadeIn);
            }
        };
        this.BeginAnimation(OpacityProperty, fadeOut);

        if (_peersWindow != null && _peersWindow.IsLoaded)
            _peersWindow.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ApplyThemeResources(string theme)
    {
        _currentTheme = theme;
        string uri = $"Themes/{theme}Theme.xaml";
        try
        {
            var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
            
            UpdateThemeButtonIcon();
        }
        catch (Exception ex) { Logger.Error($"Erreur chargement thème {theme}: {ex.Message}"); }
    }

    private void UpdateThemeButtonIcon()
    {
        if (BtnBack == null) return;

        string pathData = "";
        Brush fillBrush = Brushes.White;
        Brush strokeBrush = Brushes.Transparent;
        double strokeThickness = 0;

        switch (_currentTheme)
        {
            case "Light": // Soleil
                // Soleil jaune avec rayons
                pathData = "M12,7A5,5 0 1,1 12,17A5,5 0 1,1 12,7 M12,1L12,4 M12,20L12,23 M4.22,4.22L6.34,6.34 M17.66,17.66L19.78,19.78 M1,12L4,12 M20,12L23,12 M4.22,19.78L6.34,17.66 M17.66,6.34L19.78,4.22";
                fillBrush = Brushes.Gold;
                strokeBrush = Brushes.Gold;
                strokeThickness = 2;
                break;
            case "Dark": // Croissant de lune
                // Croissant de lune simple
                pathData = "M12,3c-4.97,0-9,4.03-9,9s4.03,9,9,9s9-4.03,9-9c0-0.46-0.04-0.92-0.1-1.36c-0.98,1.37-2.58,2.26-4.4,2.26c-2.98,0-5.4-2.42-5.4-5.4c0-1.81,0.89-3.42,2.26-4.4C12.92,3.04,12.46,3,12,3L12,3z";
                break;
            case "Red": // Etoile
                pathData = "M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z";
                break;
            default: // Soleil par défaut
                pathData = "M12,7A5,5 0 1,1 12,17A5,5 0 1,1 12,7 M12,1L12,4 M12,20L12,23 M4.22,4.22L6.34,6.34 M17.66,17.66L19.78,19.78 M1,12L4,12 M20,12L23,12 M4.22,19.78L6.34,17.66 M17.66,6.34L19.78,4.22";
                fillBrush = Brushes.Gold;
                strokeBrush = Brushes.Gold;
                strokeThickness = 2;
                break;
        }

        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(pathData),
            Fill = fillBrush,
            Stroke = strokeBrush,
            StrokeThickness = strokeThickness,
            Stretch = Stretch.Uniform,
            Width = 18,
            Height = 18
        };

        BtnBack.Content = path;
    }

    private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMiniMode();
            e.Handled = true;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Gestion du double-clic pour sortir du mode mini
        if (e.ClickCount == 2)
        {
            if (_isMiniMode) ToggleMiniMode();
            return;
        }

        // Permet de déplacer la fenêtre "Raquette" en cliquant n'importe où
        if (e.ChangedButton == MouseButton.Left) this.DragMove();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isMiniMode)
        {
            UpdateMiniWindowRegion();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMiniMode();
    }

    private void ToggleMiniMode()
    {
        _isMiniMode = !_isMiniMode;

        if (_isMiniMode)
        {
            // --- Passage en Mode Mini ---
            _restoreLeft = this.Left;
            _restoreTop = this.Top;
            _restoreWidth = this.Width;
            _restoreHeight = this.Height;

            // On capture la largeur actuelle de l'écran LCD pour la conserver
            double targetWidth = HeaderBorder.ActualWidth;

            _config = _configService.Load();

            LogoPanel.Visibility = Visibility.Collapsed;
            KeypadGrid.Visibility = Visibility.Collapsed;
            RaquetteBackground.Visibility = Visibility.Collapsed;

            MainRootGrid.Margin = new Thickness(0);
            HeaderBorder.Margin = new Thickness(0);

            this.Topmost = _config.MiniModeAlwaysOnTop;
            this.Opacity = IsMouseOver ? 1.0 : (_config.MiniModeOpacity > 0.1 ? _config.MiniModeOpacity : 1.0);
            this.ResizeMode = ResizeMode.NoResize;
            if (_peersWindow != null) _peersWindow.Topmost = _config.MiniModeAlwaysOnTop;
            
            this.Width = targetWidth;
            this.SizeToContent = SizeToContent.Height;

            if (_config.MiniModeLeft != -1 && _config.MiniModeTop != -1)
            {
                this.Left = _config.MiniModeLeft;
                this.Top = _config.MiniModeTop;
            }
        }
        else
        {
            // --- Retour au Mode Normal ---
            _config.MiniModeLeft = this.Left;
            _config.MiniModeTop = this.Top;
            _configService.Save(_config);

            this.SizeToContent = SizeToContent.Manual;

            var helper = new WindowInteropHelper(this);
            SetWindowRgn(helper.Handle, IntPtr.Zero, true);

            this.Topmost = false;
            this.Topmost = _config.MiniModeAlwaysOnTop;
            this.Opacity = 1.0;
            this.ResizeMode = ResizeMode.NoResize;
            if (_peersWindow != null) _peersWindow.Topmost = false;
            if (_peersWindow != null) _peersWindow.Topmost = _config.MiniModeAlwaysOnTop;

            LogoPanel.Visibility = Visibility.Visible;
            KeypadGrid.Visibility = Visibility.Visible;
            RaquetteBackground.Visibility = Visibility.Visible;

            MainRootGrid.Margin = new Thickness(20);

            this.Width = _restoreWidth;
            this.Height = _restoreHeight;
            this.Left = _restoreLeft;
            this.Top = _restoreTop;
        }
    }

    private void UpdateMiniWindowRegion()
    {
        if (!_isMiniMode) return;

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget == null) return;

        double dpiX = source.CompositionTarget.TransformToDevice.M11;
        double dpiY = source.CompositionTarget.TransformToDevice.M22;

        int width = (int)(this.ActualWidth * dpiX);
        int height = (int)(this.ActualHeight * dpiY);

        int cornerRadius = (int)(15 * dpiX);

        IntPtr hRgn = CreateRoundRectRgn(0, 0, width, height, cornerRadius, cornerRadius);

        var helper = new WindowInteropHelper(this);
        SetWindowRgn(helper.Handle, hRgn, true);
    }

    // --- GESTION HORLOGES & QUALITÉ (Step 24) ---

    private void UpdateSystemClock()
    {
        if (LblSysTime == null) return;
        bool isUtc = _config.UtcMode;
        DateTime now = isUtc ? DateTime.UtcNow : DateTime.Now;
        LblSysTime.Text = now.ToString("HH:mm:ss");
    }

    private void ChkUtc_Click(object sender, RoutedEventArgs e)
    {
        // Sauvegarde de la préférence
        _config.UtcMode = !_config.UtcMode; // Bascule de l'état
        _configService.Save(_config);

        // Mise à jour immédiate lors du clic
        UpdateSystemClock();
        UpdateGpsClockDisplay();
        UpdateUtcButtonAppearance();
    }

    private void UpdateGpsClockDisplay()
    {
        if (LblGpsTimeHeader == null || !_lastGpsUtcTime.HasValue) return;

        DateTime timeToDisplay = _lastGpsUtcTime.Value;
        if (!_config.UtcMode)
        {
            timeToDisplay = timeToDisplay.ToLocalTime();
        }

        LblGpsTimeHeader.Text = timeToDisplay.ToString("HH:mm:ss");
    }

    private void UpdateUtcButtonAppearance()
    {
        if (BtnUtc == null) return; // Sécurité pour l'initialisation
        
        if (LblTimeMode != null)
        {
            LblTimeMode.Text = _config.UtcMode ? "UTC" : "Locale";
        }

        if (_config.UtcMode)
        {
            BtnUtc.Content = "UTC";
            BtnUtc.SetResourceReference(BackgroundProperty, "SuccessColor");
        }
        else
        {
            BtnUtc.Content = "Locale";
            BtnUtc.SetResourceReference(BackgroundProperty, "AccentColor");
        }
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

            // Mise à jour de la fenêtre Peers si elle est ouverte
            if (_peersWindow != null) _peersWindow.UpdatePeers(output);

            // Nettoyage de l'affichage précédent
            PnlNtpPeers.Children.Clear();

            // Variables locales pour détection de la source active
            bool foundGps = false;
            bool foundAny = false;

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
                    foundAny = true;
                    // Détection si c'est notre pilote GPS (127.127.20.x) ou le refid .GPS.
                    if (line.Contains("127.127.20.") || line.Contains(".GPS."))
                    {
                        foundGps = true;
                    }

                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    // Format standard : remote refid st t when poll reach delay offset jitter
                    // Offset est souvent à l'index 8, Jitter à l'index 9
                    if (parts.Length >= 10)
                    {
                        // Remplissage du modèle de données pour le futur algorithme
                        _ntpStatus.PeerRefId = parts[1];
                        if (int.TryParse(parts[2], out int st)) _ntpStatus.PeerStratum = st;
                        
                        // Conversion du Reach (Octal string -> Int) pour analyse des bits
                        try { _ntpStatus.Reach = Convert.ToInt32(parts[6], 8); } catch { _ntpStatus.Reach = 0; }

                        if (double.TryParse(parts[8], NumberStyles.Any, CultureInfo.InvariantCulture, out double off)) _ntpStatus.Offset = off;
                        if (double.TryParse(parts[9], NumberStyles.Any, CultureInfo.InvariantCulture, out double jit)) _ntpStatus.Jitter = jit;

                        LblOffset.Text = parts[8] + " ms";
                        LblJitter.Text = parts[9] + " ms";
                    }
                }
            }

            _isGpsActivePeer = foundGps;
            _hasActivePeer = foundAny;
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

            // Remplissage du modèle de données (Driver)
            _ntpStatus.Timecode = currentTimecode;
            _ntpStatus.DriverStratum = ExtractNtpValue(output, "stratum=");
            _ntpStatus.DriverRefId = ExtractNtpString(output, "refid=");
            _ntpStatus.NoReply = ExtractNtpValue(output, "noreply=");
            _ntpStatus.BadFormat = ExtractNtpValue(output, "badformat=");
            _ntpStatus.Poll = ExtractNtpValue(output, "poll=");

            _healthCheckCounter++;
            if (_healthCheckCounter >= 10) // Analyse toutes les 10 secondes
            {
                // Calcul du score basé sur la comparaison avec l'état précédent (il y a 10s)
                _healthScore = _ntpStatus.CalculateHealthScore(_previousNtpStatus);
                _ntpStatus.HealthScore = _healthScore;
                
                // Tentative de récupération automatique si le GPS est perdu puis rebranché
                CheckAndRecoverNtp();

                UpdateHealthUI();

                // Mémorisation pour le prochain cycle
                _previousNtpStatus = _ntpStatus.Clone();
                _healthCheckCounter = 0;
            }
            // ----------------------------------------------------

            // Affichage (si on a trouvé un timecode)
            bool isGpsFixOk = false;
            if (!string.IsNullOrEmpty(currentTimecode))
            {
                isGpsFixOk = ParseAndDisplayNmea(currentTimecode);
            }

            // Feedback visuel prioritaire (Surcharge "Fix GPS OK" si problème détecté)
            if (LblStatus != null)
            {
                // 1. Câble débranché ? (Priorité absolue)
                if (!IsSerialPortAvailable(_config.SerialPort))
                {
                    LblStatus.Text = "⚠️ Câble USB débranché ?";
                    LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "ErrorColor");
                }
                // 2. Bascule sur Web ? (Le port est là, mais NTP utilise une autre source)
                else if (_hasActivePeer && !_isGpsActivePeer)
                {
                    LblStatus.Text = "⚠️ Mode Web (GPS ignoré)";
                    LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor");
                }
                // 3. Suspicion blocage ? (GPS actif mais santé pourrie depuis > 20s)
                else if (_consecutivePoorHealth >= 2)
                {
                    LblStatus.Text = "⚠️ Suspicion blocage";
                    LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor");
                }
                // 4. Tout va bien (ou recherche)
                else
                {
                    if (isGpsFixOk)
                    {
                        LblStatus.Text = "Fix GPS OK";
                        LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
                    }
                    else
                    {
                        LblStatus.Text = "Recherche de satellites...";
                        LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor");
                    }
                }
            }
        }
        catch { }
    }

    private void CheckAndRecoverNtp()
    {
        // Condition : Santé critique (< 20%) ou Stratum 16 (Non sync)
        // Cela indique que NTP tourne mais ne reçoit rien de valide
        if (_healthScore < 20 || _ntpStatus.PeerStratum >= 16)
        {
            _consecutivePoorHealth++;
            
            // 3 cycles = 30 secondes de panne confirmée
            if (_consecutivePoorHealth >= 3)
            {
                // Vérification du Cooldown (5 minutes) pour éviter les boucles de redémarrage
                if ((DateTime.Now - _lastAutoRestart).TotalMinutes > 5)
                {
                    // Vérification que le port COM est bien présent physiquement
                    if (IsSerialPortAvailable(_config.SerialPort))
                    {
                        Logger.Info("AUTO-RECOVERY : Santé critique détectée avec port COM présent. Redémarrage préventif du service NTP...");
                        _lastAutoRestart = DateTime.Now;
                        _consecutivePoorHealth = 0;
                        
                        // Lancement asynchrone pour ne pas bloquer l'UI
                        _expectingNtpStateChange = true;
                        Task.Run(() => WindowsServiceHelper.RestartService("NTP"));
                    }
                }
            }
        }
        else
        {
            _consecutivePoorHealth = 0;
        }
    }

    private bool IsSerialPortAvailable(string portName)
    {
        try { return System.IO.Ports.SerialPort.GetPortNames().Any(p => p.Equals(portName, StringComparison.OrdinalIgnoreCase)); }
        catch { return false; }
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

    private string ExtractNtpString(string output, string key)
    {
        try
        {
            int idx = output.IndexOf(key);
            if (idx != -1)
            {
                idx += key.Length;
                int endIdx = output.IndexOf(",", idx);
                if (endIdx == -1) endIdx = output.Length;
                return output.Substring(idx, endIdx - idx).Trim().Trim('"');
            }
        }
        catch { }
        return string.Empty;
    }

    private void UpdateHealthUI()
    {
        if (LblHealth == null) return;
        LblHealth.Text = $"{_healthScore:F0}%";

        string colorKey = "ErrorColor";
        if (_healthScore > 90) colorKey = "SuccessColor";
        else if (_healthScore > 50) colorKey = "WarningColor";

        LblHealth.SetResourceReference(TextBlock.ForegroundProperty, colorKey);

        // La bordure colorée du mode mini est obsolète.

        // Mise à jour des barres de signal
        UpdateSignalBar(Bar1, _healthScore >= 20, colorKey);
        UpdateSignalBar(Bar2, _healthScore >= 40, colorKey);
        UpdateSignalBar(Bar3, _healthScore >= 60, colorKey);
        UpdateSignalBar(Bar4, _healthScore >= 80, colorKey);
        UpdateSignalBar(Bar5, _healthScore >= 99, colorKey);

        // Mise à jour de l'info-bulle détaillée
        if (LblHealth.ToolTip is not ToolTip tt)
        {
            tt = new ToolTip();
            LblHealth.ToolTip = tt;
        }
        ((ToolTip)LblHealth.ToolTip).Content = 
            $"Santé: {_healthScore:F0}%\n" +
            $"Stratum: {_ntpStatus.PeerStratum}\n" +
            $"RefID: {_ntpStatus.PeerRefId}\n" +
            $"Reach: {Convert.ToString(_ntpStatus.Reach, 8)} (Octal)\n" +
            $"Offset: {_ntpStatus.Offset:F3} ms";
    }

    private void UpdateSignalBar(System.Windows.Shapes.Rectangle? bar, bool isActive, string colorKey)
    {
        if (bar == null) return;
        if (isActive)
        {
            bar.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, colorKey);
        }
        else
        {
            bar.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "BorderColor");
        }
    }

    private bool ParseAndDisplayNmea(string nmea)
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
                        _lastGpsUtcTime = gpsTime;

                        bool isUtc = _config.UtcMode;
                        if (!isUtc) gpsTime = gpsTime.ToLocalTime();
                        
                        string formattedTime = gpsTime.ToString("HH:mm:ss");
                        LblGpsTimeHeader.Text = formattedTime;
                        
                        // Mise à jour de l'horloge système synchronisée
                        UpdateSystemClock();
                        _lastClockUpdate = DateTime.Now;
                    }
                }

                // Position (si valide)
                if (parts[2] == "A")
                {
                    // Affichage brut des coordonnées (ou conversion si nécessaire)
                    // Correction Bug : Conversion NMEA (DDMM.MMMM) vers Degrés Décimaux (DD.dddd)
                    LblLat.Text = NmeaToDecimal(parts[3], parts[4]);
                    LblLon.Text = NmeaToDecimal(parts[5], parts[6]);
                    LblLatDms.Text = NmeaToDms(parts[3], parts[4]);
                    LblLonDms.Text = NmeaToDms(parts[5], parts[6]);
                    return true;
                }
            }
        }
        catch { }
        return false;
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
            return $"{degrees.ToString().PadLeft(padWidth)}°{intMin:00}'{seconds:00}\" {dir}";
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
        return $"{degrees.ToString().PadLeft(padWidth)}°{intMin:00}'{seconds:00}\" {dir}";
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
                _lastGpsUtcTime = data.UtcTime;
                DateTime displayTime = data.UtcTime;
                bool isUtc = _config.UtcMode;
                if (!isUtc) displayTime = displayTime.ToLocalTime();
                
                LblGpsTimeHeader.Text = displayTime.ToString("HH:mm:ss");
                
                // Mise à jour de l'horloge système synchronisée
                UpdateSystemClock();
                _lastClockUpdate = DateTime.Now;
            }

            if (data.IsValid)
            {
                LblStatus.Text = "Fix GPS OK";
                LblStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
                
                // FIX: Idem pour l'heure dans le panneau de détails
                if (data.UtcTime != DateTime.MinValue)
                {
                    DateTime displayTime = data.UtcTime;
                    bool isUtc = _config.UtcMode;
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
        if (_isMiniMode)
        {
            _config.MiniModeLeft = this.Left;
            _config.MiniModeTop = this.Top;
            _configService.Save(_config);
        }
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
                        Logger.Info($"Thème restauré : {_currentTheme}");
                    }

                    // Restauration du mode Mini
                    if (state.IsMiniMode)
                    {
                        _shouldStartInMiniMode = true;
                        Logger.Info("Mode Mini restauré.");
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
            var state = new AppState 
            { 
                HealthScore = _healthScore, 
                LastExitTime = DateTime.UtcNow, 
                Theme = _currentTheme,
                IsMiniMode = _isMiniMode
            };
            string json = System.Text.Json.JsonSerializer.Serialize(state);
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appstate.json");
            System.IO.File.WriteAllText(path, json);
        }
        catch (Exception ex) { Logger.Error($"Erreur sauvegarde état : {ex.Message}"); }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        // La logique de bascule en mode mini lors de la minimisation est obsolète.
        base.OnStateChanged(e);
    }
}

public class AppState
{
    public double HealthScore { get; set; }
    public DateTime LastExitTime { get; set; }
    public string? Theme { get; set; }
    public bool IsMiniMode { get; set; }
}
