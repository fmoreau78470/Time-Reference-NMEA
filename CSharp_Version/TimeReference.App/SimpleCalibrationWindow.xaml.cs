using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App;

public partial class SimpleCalibrationWindow : Window
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private DispatcherTimer? _timer;
    private DateTime _startTime;
    private TimeSpan _targetDuration;
    private string _loopstatsPath = string.Empty;
    private long _lastFilePosition = 0;
    private List<double> _offsets = new List<double>();
    private double _calculatedFudge = 0;
    private bool _isRunning = false;

    public SimpleCalibrationWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        _configService = new ConfigService();
        Log("Mode Simple initialisé.");
        Log("Ce mode aligne le GPS sur l'horloge système actuelle.");
    }

    private void Log(string message)
    {
        TxtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        TxtLogs.ScrollToEnd();
    }

    private void SldDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblDuration != null)
            LblDuration.Text = $"{(int)e.NewValue} min";
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            StopAnalysis();
            return;
        }

        // 1. Localiser le fichier loopstats
        string ntpDir = Path.GetDirectoryName(_config.NtpConfPath) ?? @"C:\Program Files (x86)\NTP\etc";
        // Format standard NTP : loopstats.YYYYMMDD
        string filename = $"loopstats.{DateTime.Now:yyyyMMdd}";
        _loopstatsPath = Path.Combine(ntpDir, filename);

        if (!File.Exists(_loopstatsPath))
        {
            Log($"ERREUR : Fichier introuvable : {_loopstatsPath}");
            Log("Vérifiez que 'statsdir' est configuré dans ntp.conf et que le service tourne.");
            MessageBox.Show($"Fichier loopstats introuvable :\n{_loopstatsPath}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Log($"Fichier identifié : {_loopstatsPath}");

        // 2. Préparation
        _isRunning = true;
        BtnStart.Content = "Arrêter";
        BtnClose.IsEnabled = false;
        SldDuration.IsEnabled = false;
        _offsets.Clear();
        _targetDuration = TimeSpan.FromMinutes(SldDuration.Value);
        _startTime = DateTime.Now;

        // On se place à la fin du fichier pour ne lire que les nouvelles données
        using (var fs = new FileStream(_loopstatsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            _lastFilePosition = fs.Length;
        }
        Log("Positionnement à la fin du fichier. En attente de nouvelles données NTP...");

        // 3. Démarrage du Timer (Vérification toutes les 2s)
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(2);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        LblStatus.Text = "Analyse en cours...";
    }

    private void StopAnalysis()
    {
        _timer?.Stop();
        _isRunning = false;
        BtnStart.Content = "Démarrer";
        SldDuration.IsEnabled = true;
        BtnClose.IsEnabled = true;
        Log("Analyse arrêtée par l'utilisateur.");
        LblStatus.Text = "Arrêté.";
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        // Gestion du temps
        var elapsed = DateTime.Now - _startTime;
        double progress = (elapsed.TotalSeconds / _targetDuration.TotalSeconds) * 100;
        if (progress > 100) progress = 100;
        PbProgress.Value = progress;

        // Lecture des nouvelles lignes
        ReadNewLines();

        // Fin de la période ?
        if (elapsed >= _targetDuration)
        {
            _timer?.Stop();
            CalculateResult();
        }
    }

    private void ReadNewLines()
    {
        try
        {
            using (var fs = new FileStream(_loopstatsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length > _lastFilePosition)
                {
                    fs.Seek(_lastFilePosition, SeekOrigin.Begin);
                    using (var sr = new StreamReader(fs))
                    {
                        string? line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            ParseLoopstatsLine(line);
                        }
                        _lastFilePosition = fs.Position;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Erreur lecture fichier : {ex.Message}");
        }
    }

    private void ParseLoopstatsLine(string line)
    {
        // Format loopstats : MJD Second Offset Drift Error Stability Poll
        // Ex: 59879 45213.123 0.001234 12.34 ...
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            if (double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double offset))
            {
                _offsets.Add(offset);
                Log($"Nouvelle mesure : Offset = {offset * 1000:F3} ms");
                
                // Mise à jour stats UI
                if (_offsets.Count > 0)
                {
                    LblStats.Text = $"Moyenne Offset : {_offsets.Average() * 1000:F3} ms | Échantillons : {_offsets.Count}";
                }
            }
        }
    }

    private void CalculateResult()
    {
        _isRunning = false;
        BtnStart.Content = "Démarrer";
        LblStatus.Text = "Analyse terminée.";
        BtnClose.IsEnabled = true;

        if (_offsets.Count == 0)
        {
            Log("ERREUR : Aucune donnée reçue. Vérifiez que NTP tourne et écrit dans loopstats.");
            MessageBox.Show("Aucune donnée collectée.\nLe service NTP est-il démarré ?", "Échec", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        double avgOffset = _offsets.Average();
        Log("------------------------------------------------");
        Log($"Moyenne Offset (Peer - Local) : {avgOffset * 1000:F3} ms");

        // Calcul du nouveau Fudge
        // Si Offset > 0, le GPS est en avance sur le Local.
        // Pour aligner le GPS sur le Local (Offset -> 0), il faut réduire le temps GPS.
        // Nouveau Fudge = Ancien Fudge - Offset Moyen
        double currentFudge = _config.Time2Value;
        double newFudge = currentFudge - avgOffset;

        _calculatedFudge = newFudge;

        Log($"Fudge Actuel : {currentFudge * 1000:F3} ms");
        Log($"Nouveau Fudge Suggéré : {newFudge * 1000:F3} ms");
        Log("------------------------------------------------");

        BtnApply.IsEnabled = true;
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _config.Time2Value = Math.Round(_calculatedFudge, 4);
            _configService.Save(_config);
            Log("Configuration sauvegardée.");

            var ntpService = new NtpService();
            ntpService.GenerateConfFile(_config);
            Log("Fichier ntp.conf régénéré.");

            WindowsServiceHelper.RestartService("NTP");
            Log("Service NTP redémarré.");

            MessageBox.Show("Calibration appliquée !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            BtnApply.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        Close();
    }
}