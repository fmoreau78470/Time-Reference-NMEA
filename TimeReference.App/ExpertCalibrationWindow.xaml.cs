using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App;

public partial class ExpertCalibrationWindow : Window
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private DispatcherTimer? _timer;
    private int _measureCount = 0;
    private const int MAX_MEASURES = 30; // 30 secondes de mesure
    private List<double> _gpsOffsets = new List<double>();
    private List<double> _netOffsets = new List<double>();
    private double _calculatedFudge = 0;

    public ExpertCalibrationWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        _configService = new ConfigService();
        Log("Assistant initialisé. En attente de démarrage...");
        this.Loaded += (s, e) => EnsureVisible();
    }

    private void Log(string message)
    {
        TxtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        TxtLogs.ScrollToEnd();
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        BtnStart.IsEnabled = false;
        BtnClose.IsEnabled = false;
        
        // Étape 1 : Vérification Santé
        LblStep.Text = "Étape 1 : Vérification Santé GPS";
        PbProgress.Value = 10;
        Log("Vérification de l'état du GPS (Reach & Jitter)...");

        bool healthOk = await CheckGpsHealthAsync();
        if (!healthOk)
        {
            LblStatus.Text = "Échec Santé GPS";
            BtnStart.IsEnabled = true;
            BtnClose.IsEnabled = true;
            return;
        }

        // Étape 2 : Mesure
        LblStep.Text = "Étape 2 : Mesure Comparative";
        Log("Démarrage des mesures (30 secondes)...");
        _measureCount = 0;
        _gpsOffsets.Clear();
        _netOffsets.Clear();

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += async (s, args) => await MeasureTickAsync();
        _timer.Start();
    }

    private async Task<bool> CheckGpsHealthAsync()
    {
        string output = await RunNtpqPAsync();
        if (string.IsNullOrEmpty(output))
        {
            Log("Erreur : Impossible d'exécuter ntpq -p");
            return false;
        }

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // On cherche la ligne GPS (souvent .GPS. ou 127.127.20.x)
            if (line.Contains(".GPS.") || line.Contains("127.127.20."))
            {
                // Format: remote refid st t when poll reach delay offset jitter
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 10)
                {
                    string reachStr = parts[6];
                    string jitterStr = parts[9];

                    Log($"GPS trouvé : Reach={reachStr}, Jitter={jitterStr}ms");

                    if (reachStr != "377")
                    {
                        Log("ERREUR : Le Reach n'est pas à 377 (GPS instable ou démarrage récent).");
                        MessageBox.Show("Le GPS n'est pas stable (Reach != 377).\nAttendez quelques minutes que NTP se synchronise.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    return true;
                }
            }
        }

        Log("ERREUR : Source GPS introuvable dans ntpq -p");
        return false;
    }

    private async Task MeasureTickAsync()
    {
        _measureCount++;
        PbProgress.Value = 10 + (_measureCount * 2); // 10 -> 70%
        LblStatus.Text = $"Mesure {_measureCount}/{MAX_MEASURES}";

        string output = await RunNtpqPAsync();
        ParseOffsets(output);

        if (_measureCount >= MAX_MEASURES)
        {
            _timer?.Stop();
            CalculateResult();
        }
    }

    private void ParseOffsets(string output)
    {
        if (string.IsNullOrEmpty(output)) return;

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        double currentGpsOffset = double.NaN;
        List<double> currentNetOffsets = new List<double>();

        foreach (var line in lines)
        {
            if (line.StartsWith("remote")) continue;
            if (line.StartsWith("=")) continue;

            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 9) continue;

            // Offset est à l'index 8
            if (double.TryParse(parts[8], NumberStyles.Any, CultureInfo.InvariantCulture, out double offset))
            {
                if (line.Contains(".GPS.") || line.Contains("127.127.20."))
                {
                    currentGpsOffset = offset;
                }
                else if (!line.Contains(".LOCL.")) // On ignore Local Clock
                {
                    // On prend les serveurs Internet (candidats + ou actifs *)
                    if (line.StartsWith("*") || line.StartsWith("+") || line.StartsWith("-"))
                    {
                        currentNetOffsets.Add(offset);
                    }
                }
            }
        }

        if (!double.IsNaN(currentGpsOffset)) _gpsOffsets.Add(currentGpsOffset);
        if (currentNetOffsets.Count > 0) _netOffsets.Add(currentNetOffsets.Average());
    }

    private void CalculateResult()
    {
        LblStep.Text = "Étape 3 : Calcul";
        PbProgress.Value = 90;

        if (_gpsOffsets.Count == 0 || _netOffsets.Count == 0)
        {
            Log("ERREUR : Pas assez de données pour le calcul.");
            LblStatus.Text = "Échec Calcul";
            BtnClose.IsEnabled = true;
            return;
        }

        double avgGps = _gpsOffsets.Average();
        double avgNet = _netOffsets.Average();

        Log($"Moyenne Offset GPS (vs Local) : {avgGps:F3} ms");
        Log($"Moyenne Offset Net (vs Local) : {avgNet:F3} ms");

        // Formule Spec 10 : Nouveau Fudge = Fudge Actuel + Offset Constaté
        // Offset Constaté (Ecart Réel) = Offset_Net - Offset_GPS
        // Ex: Net=+2, GPS=-865 => Ecart = 2 - (-865) = +867ms à ajouter
        double deltaMs = avgNet - avgGps;
        
        Log($"Écart constaté (Delta) : {deltaMs:F3} ms");

        double currentFudgeSec = _config.Time2Value;
        double newFudgeSec = currentFudgeSec + (deltaMs / 1000.0);

        _calculatedFudge = newFudgeSec;

        Log("------------------------------------------------");
        Log($"Fudge Actuel : {currentFudgeSec:F4} s");
        Log($"Nouveau Fudge Calculé : {newFudgeSec:F4} s");
        Log("------------------------------------------------");

        LblStatus.Text = "Calcul terminé. Vérifiez les logs.";
        PbProgress.Value = 100;
        BtnApply.IsEnabled = true;
        BtnClose.IsEnabled = true;
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Mise à jour de la config
            _config.Time2Value = Math.Round(_calculatedFudge, 4);
            _configService.Save(_config);
            Log("Configuration (config.json) mise à jour.");

            // Régénération ntp.conf
            var ntpService = new NtpService();
            ntpService.GenerateConfFile(_config);
            Log("Fichier ntp.conf régénéré.");

            // Redémarrage Service
            WindowsServiceHelper.RestartService("NTP");
            Log("Service NTP redémarré.");

            MessageBox.Show("Calibration appliquée avec succès !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            BtnApply.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur lors de l'application :\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            Log($"ERREUR : {ex.Message}");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_timer != null && _timer.IsEnabled) _timer.Stop();
        this.Close();
    }

    private async Task<string> RunNtpqPAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("ntpq", "-p") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
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
    }

    private void EnsureVisible()
    {
        double virtualScreenLeft = SystemParameters.VirtualScreenLeft;
        double virtualScreenTop = SystemParameters.VirtualScreenTop;
        double virtualScreenWidth = SystemParameters.VirtualScreenWidth;
        double virtualScreenHeight = SystemParameters.VirtualScreenHeight;

        bool isOffScreen = (this.Left + this.Width < virtualScreenLeft) ||
                           (this.Left > virtualScreenLeft + virtualScreenWidth) ||
                           (this.Top + this.Height < virtualScreenTop) ||
                           (this.Top > virtualScreenTop + virtualScreenHeight);

        if (isOffScreen)
        {
            this.Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - this.Width) / 2;
            this.Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - this.Height) / 2;
        }
    }
}