using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Media;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App;

public partial class SimpleCalibrationWindow : Window
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private DispatcherTimer? _timer;
    private DateTime _analysisStartTime;
    private TimeSpan _targetDuration;
    private DateTime _measurementStartTime;
    
    // Transposition Python: Listes pour accumuler les offsets
    private List<double> _gpsOffsets = new List<double>();
    private List<double> _webOffsets = new List<double>();

    // Historique pour le graphique (Temps écoulé en s, Offset en ms)
    private List<(double Time, double Offset)> _historyGps = new();
    private Dictionary<string, List<(double Time, double Offset)>> _historyWebServers = new();
    
    // Historique médianes
    private List<(double Time, double Offset)> _historyMedianGps = new();
    private List<(double Time, double Offset)> _historyMedianWeb = new();

    private Polyline _lineGps = new Polyline { Stroke = Brushes.LimeGreen, StrokeThickness = 1, Opacity = 0.5 };
    private Polyline _lineMedianGps = new Polyline { Stroke = Brushes.LimeGreen, StrokeThickness = 3 };
    private Polyline _lineMedianWeb = new Polyline { Stroke = Brushes.Cyan, StrokeThickness = 3 };
    private Dictionary<string, Polyline> _linesWebServers = new();
    private List<UIElement> _gridElements = new List<UIElement>();

    private TextBlock _labelMedianGps = new TextBlock { Foreground = Brushes.LimeGreen, FontWeight = FontWeights.Bold, Background = Brushes.Black, Opacity = 0.8, Padding = new Thickness(2,0,2,0) };
    private TextBlock _labelMedianWeb = new TextBlock { Foreground = Brushes.Cyan, FontWeight = FontWeights.Bold, Background = Brushes.Black, Opacity = 0.8, Padding = new Thickness(2,0,2,0) };

    private double _calculatedFudge = 0;
    private bool _isRunning = false;
    private bool _isMeasuring = false;
    private bool _isGpsStabilized = false;
    private bool _isNtpModified = false;
    
    // Zoom & Pan
    private double _viewMinX = 0;
    private double _viewMaxX = 60;
    private bool _isAutoScroll = true;
    private Point _lastMousePos;
    private bool _isDragging = false;

    public SimpleCalibrationWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        _configService = new ConfigService();
        Log("Mode Simple (NTPQ Monitor) initialisé.");
        Log("Ce mode analyse les offsets via 'ntpq -pn' (Algorithme Python transposé).");
        Closing += Window_Closing;

        // Initialisation du graphique
        CnvGraph.Children.Add(_lineGps);
        CnvGraph.Children.Add(_lineMedianGps);
        CnvGraph.Children.Add(_lineMedianWeb);
        CnvGraph.Children.Add(_labelMedianGps);
        CnvGraph.Children.Add(_labelMedianWeb);
        this.Loaded += (s, e) => EnsureVisible();
    }

    private void Log(string message)
    {
        Dispatcher.Invoke(() => 
        {
            TxtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            TxtLogs.ScrollToEnd();
        });
    }

    private void SldDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblDuration != null && !_isRunning)
            LblDuration.Text = $"{(int)e.NewValue} min";
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            await StopAnalysisAsync(userRequested: true);
            return;
        }

        // 2. Préparation
        _isRunning = true;
        SetStartButtonState(false); // Affiche l'icône Stop
        BtnClose.IsEnabled = false;
        PnlDurationSettings.Visibility = Visibility.Collapsed;
        LblCountdown.Visibility = Visibility.Visible;
        LblCountdown.Text = "Stabilisation...";
        _isMeasuring = false;
        _isGpsStabilized = false;
        
        // Réinitialisation des listes (Python: all_gps_offsets = [], all_web_offsets = [])
        _gpsOffsets.Clear();
        _webOffsets.Clear();
        _historyGps.Clear();
        _historyWebServers.Clear();
        _historyMedianGps.Clear();
        _historyMedianWeb.Clear();
        foreach (var line in _linesWebServers.Values) CnvGraph.Children.Remove(line);
        _linesWebServers.Clear();

        _targetDuration = TimeSpan.FromMinutes(SldDuration.Value);
        _analysisStartTime = DateTime.Now;

        Log("Démarrage du monitoring NTP (ntpq -pn)...");
        LblStats.Text = "Attente stabilisation GPS & serveurs Web...";
        
        // Modification temporaire de ntp.conf
        await ModifyNtpConfigAsync(true);

        // 3. Démarrage du Timer (Vérification toutes les 10s)
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(10);
        _timer.Tick += async (s, args) => await Timer_TickAsync();
        _timer.Start();

        LblStatus.Text = "Analyse en cours...";
    }

    private async Task StopAnalysisAsync(bool userRequested)
    {
        _timer?.Stop();
        _isRunning = false;
        _isMeasuring = false;
        SetStartButtonState(true); // Affiche l'icône Start
        PnlDurationSettings.Visibility = Visibility.Visible;
        LblCountdown.Visibility = Visibility.Collapsed;
        BtnClose.IsEnabled = true;
        LblStatus.Text = "Arrêté.";
        
        if (_isNtpModified)
        {
            await ModifyNtpConfigAsync(false);
        }
    }
    private async Task StopAnalysisAsync() => await StopAnalysisAsync(false);

    private async Task Timer_TickAsync()
    {
        // Exécution et Parsing (Transposition Python)
        await ProcessNtpqAsync();

        if (_isMeasuring)
        {
            // Measurement is running, update progress bar
            var elapsed = DateTime.Now - _measurementStartTime;
            
            var remaining = _targetDuration - elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            LblCountdown.Text = $"Temps restant : {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";

            // Check for end of measurement
            if (elapsed >= _targetDuration)
            {
                _timer?.Stop();
                await CalculateResultAsync();
            }
        }
        else // Not measuring yet, we are in stabilization phase
        {
            // Check if we can start measuring now
            if (_isGpsStabilized && _isWebStabilized)
            {
                _isMeasuring = true;
                _measurementStartTime = DateTime.Now; // Start the measurement clock!
                LblStatus.Text = "Mesure en cours...";
                LblStats.Text = "Mesure en cours...";
                Log("GPS et serveurs Web stabilisés. Début de la mesure.");
                LblCountdown.Text = "Mesure en cours...";
            }
            else
            {
                // Update status message
                var statusParts = new List<string>();
                if (!_isGpsStabilized) statusParts.Add("GPS");
                if (!_isWebStabilized) statusParts.Add("serveurs Internet");
                LblStats.Text = $"Attente stabilisation {string.Join(" & ", statusParts)}...";
                LblCountdown.Text = "Stabilisation...";
            }
        }
    }

    private async Task ProcessNtpqAsync()
    {
        string output = await RunNtpqPnAsync();
        if (string.IsNullOrWhiteSpace(output)) return;

        // Affichage brut
        TxtNtpOutput.Text = output;

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 9) continue;

            // Extraction adresse et tally
            string remoteToken = parts[0];
            string address = remoteToken;
            char tally = ' ';

            // Si le premier caractère n'est pas un chiffre (et pas une partie d'IP), c'est un tally
            if (!char.IsDigit(remoteToken[0]))
            {
                tally = remoteToken[0];
                address = remoteToken.Length > 1 ? remoteToken.Substring(1) : (parts.Length > 1 ? parts[1] : "");
            }

            // Classification
            bool isGps = address.StartsWith("127.127.");

            // Check for stabilization first, regardless of other filters
            if (parts.Length > 6 && parts[6] == "377")
            {
                if (isGps) _isGpsStabilized = true;
                else _isWebStabilized = true;
            }

            // Filtrage 1 : On garde le GPS peu importe son état (tally), pourvu qu'il soit présent
            // Filtrage 2 : Pour le Web, on ne garde que les sources actives (*, +, o)
            if (!isGps && tally != '*' && tally != '+' && tally != 'o') continue;

            // Filtrage 3 : Reach (index 6) == 377 (Stabilité requise)
            if (parts.Length > 6 && parts[6] != "377") continue;

            // Python: Extraction offset (index 8)
            if (double.TryParse(parts[8], NumberStyles.Any, CultureInfo.InvariantCulture, out double offset))
            {
                double graphTime = (DateTime.Now - _analysisStartTime).TotalSeconds;

                // Always add to graph history
                if (isGps)
                {
                    _historyGps.Add((graphTime, offset));
                }
                else
                {
                    if (!_historyWebServers.ContainsKey(address))
                    {
                        _historyWebServers[address] = new List<(double Time, double Offset)>();
                        var serverLine = new Polyline 
                        { 
                            Stroke = Brushes.Cyan, 
                            StrokeThickness = 1,
                            Opacity = 0.7
                        };
                        _linesWebServers[address] = serverLine;
                        CnvGraph.Children.Add(serverLine);
                    }
                    _historyWebServers[address].Add((graphTime, offset));
                }

                // Only add to calculation lists if we are in the measurement phase
                if (_isMeasuring)
                {
                    if (isGps)
                    {
                        _gpsOffsets.Add(offset);
                    }
                    else
                    {
                        _webOffsets.Add(offset);
                    }
                }
            }
        }
        
        // Calculate and store median history for the graph
        var allGpsPoints = _historyGps.Select(p => p.Offset).ToList();
        var allWebPoints = _historyWebServers.Values.SelectMany(list => list.Select(p => p.Offset)).ToList();

        double medGps = GetMedian(allGpsPoints);
        double medWeb = GetMedian(allWebPoints);
        
        double currentGraphTime = (DateTime.Now - _analysisStartTime).TotalSeconds;
        _historyMedianGps.Add((currentGraphTime, medGps));
        _historyMedianWeb.Add((currentGraphTime, medWeb));

        DrawGraph();
    }

    private async Task<string> RunNtpqPnAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("ntpq", "-pn")
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
    }

    private double GetMedian(List<double> values)
    {
        if (values == null || values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToList();
        int count = sorted.Count;
        if (count % 2 == 0)
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        else
            return sorted[count / 2];
    }

    private async Task CalculateResultAsync()
    {
        _isRunning = false;
        SetStartButtonState(true); // Affiche l'icône Start
        LblStatus.Text = "Analyse terminée.";
        BtnClose.IsEnabled = true;
        
        if (_isNtpModified)
        {
            await ModifyNtpConfigAsync(false);
        }

        if (_gpsOffsets.Count == 0)
        {
            Log("ERREUR : Aucune donnée GPS valide (Reach=377) collectée.");
            MessageBox.Show(this, "Aucune donnée GPS stable (Reach=377) n'a été trouvée.\nAttendez que le GPS se stabilise.", "Échec", MessageBoxButton.OK, MessageBoxImage.Warning);
            
            PnlDurationSettings.Visibility = Visibility.Visible;
            LblCountdown.Visibility = Visibility.Collapsed;
            return;
        }

        double medianGps = GetMedian(_gpsOffsets);
        double medianWeb = GetMedian(_webOffsets);

        Log("------------------------------------------------");
        Log($"Médiane GPS (vs Local) : {medianGps:F3} ms");
        Log($"Médiane Web (vs Local) : {medianWeb:F3} ms");

        // Calcul de la nouvelle Compensation (anciennement Fudge)
        // Logique : On aligne le GPS sur la référence Internet (Web).
        // Ecart = Offset Web - Offset GPS.
        // Nouveau Fudge = Ancien Fudge + Ecart.
        // Attention aux unités : ntpq renvoie des ms, Time2Value est en secondes.
        
        double currentFudgeSec = _config.Time2Value;
        double gapMs = medianWeb - medianGps;
        double gapSec = gapMs / 1000.0;
        double newFudgeSec = currentFudgeSec + gapSec;

        _calculatedFudge = newFudgeSec;

        Log($"Compensation Actuelle : {currentFudgeSec:F4} s");
        Log($"Ecart constaté (Web - GPS) : {gapSec:F4} s ({gapMs:F3} ms)");
        Log($"Nouvelle Compensation Suggérée : {newFudgeSec:F4} s");
        Log("------------------------------------------------");

        var result = MessageBox.Show(
            this,
            $"Résultat de la calibration :\n\n" +
            $"Compensation actuelle : {currentFudgeSec:F4} s\n" +
            $"Ecart constaté (Web - GPS) : {gapSec:F4} s\n" +
            $"Nouvelle compensation : {newFudgeSec:F4} s\n\n" +
            "Voulez-vous appliquer cette compensation ?",
            "Validation de la compensation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        PnlDurationSettings.Visibility = Visibility.Visible;
        LblCountdown.Visibility = Visibility.Collapsed;

        if (result == MessageBoxResult.Yes)
        {
            await ApplyCompensationAsync();
        }
    }

    private async Task ApplyCompensationAsync()
    {
        try
        {
            _config.Time2Value = Math.Round(_calculatedFudge, 4);
            _configService.Save(_config);
            Log("Configuration sauvegardée.");

            await Task.Run(() => 
            {
                var ntpService = new NtpService();
                ntpService.GenerateConfFile(_config);
            });
            Log("Fichier ntp.conf régénéré.");

            await Task.Run(() => WindowsServiceHelper.RestartService("NTP"));
            Log("Service NTP redémarré.");
            _isNtpModified = false; // La config a été écrasée par la nouvelle version propre

            MessageBox.Show(this, "Compensation appliquée !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        await StopAnalysisAsync(userRequested: true);
        Close();
    }
    
    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Ensure we stop analysis and revert config if running
        if (_isRunning)
        {
            e.Cancel = true; // Prevent closing immediately
            await StopAnalysisAsync(userRequested: false);
            Close(); // Now close for real
        }
        else if (_isNtpModified) // If not running but still modified (e.g. error)
        {
            e.Cancel = true;
            IsEnabled = false;
            await ModifyNtpConfigAsync(false);
            Close();
        }
    }

    // --- GESTION GRAPHIQUE ---

    private void CnvGraph_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawGraph();
    }

    private void DrawGraph()
    {
        if (CnvGraph.ActualWidth == 0 || CnvGraph.ActualHeight == 0) return;

        // Nettoyage de l'ancienne grille
        foreach (var el in _gridElements) CnvGraph.Children.Remove(el);
        _gridElements.Clear();

        // 1. Déterminer les échelles
        var allTimes = _historyGps.Select(p => p.Time)
            .Concat(_historyWebServers.Values.SelectMany(l => l.Select(p => p.Time)))
            .Concat(_historyMedianGps.Select(p => p.Time))
            .Concat(_historyMedianWeb.Select(p => p.Time));

        if (_isAutoScroll)
        {
            double maxDataTime = allTimes.Any() ? allTimes.Max() : 0;
            _viewMinX = 0;
            _viewMaxX = maxDataTime / 0.75; // Données courantes à 75% de la largeur
            if (_viewMaxX < 60) _viewMaxX = 60; // Ensure a minimum view of 60s
        }
        
        // Min/Max Y pour l'échelle verticale
        double minY = -10, maxY = 10;
        var allPoints = _historyGps.Concat(_historyWebServers.Values.SelectMany(x => x))
                                   .Concat(_historyMedianGps)
                                   .Concat(_historyMedianWeb)
                                   .ToList();
        if (allPoints.Count > 0)
        {
            minY = allPoints.Min(p => p.Offset);
            maxY = allPoints.Max(p => p.Offset);
            // Marge de 10%
            double range = maxY - minY;
            if (range < 1) range = 1; // Évite division par zéro
            minY -= range * 0.1;
            maxY += range * 0.1;
        }

        // Dessin de la grille
        DrawGrid(_viewMinX, _viewMaxX, minY, maxY);

        // 2. Transformation des points
        _lineGps.Points.Clear();
        foreach (var p in _historyGps)
        {
            _lineGps.Points.Add(ProjectPoint(p.Time, p.Offset, _viewMinX, _viewMaxX, minY, maxY));
        }

        foreach (var kvp in _historyWebServers)
        {
            var line = _linesWebServers[kvp.Key];
            line.Points.Clear();
            foreach (var p in kvp.Value)
            {
                line.Points.Add(ProjectPoint(p.Time, p.Offset, _viewMinX, _viewMaxX, minY, maxY));
            }
        }

        // Médianes
        _lineMedianGps.Points.Clear();
        var smoothGps = SmoothData(_historyMedianGps);
        foreach (var p in smoothGps)
        {
            _lineMedianGps.Points.Add(ProjectPoint(p.Time, p.Offset, _viewMinX, _viewMaxX, minY, maxY));
        }

        _lineMedianWeb.Points.Clear();
        var smoothWeb = SmoothData(_historyMedianWeb);
        foreach (var p in smoothWeb)
        {
            _lineMedianWeb.Points.Add(ProjectPoint(p.Time, p.Offset, _viewMinX, _viewMaxX, minY, maxY));
        }

        // Mise à jour des labels des médianes
        if (_historyMedianGps.Any())
        {
            var lastPoint = _historyMedianGps.Last();
            var lastSmooth = smoothGps.Last();
            var projectedPoint = ProjectPoint(lastSmooth.Time, lastSmooth.Offset, _viewMinX, _viewMaxX, minY, maxY);
            _labelMedianGps.Text = $"{lastPoint.Offset:F2}";
            Canvas.SetLeft(_labelMedianGps, Math.Min(projectedPoint.X + 5, CnvGraph.ActualWidth - _labelMedianGps.ActualWidth - 5));
            Canvas.SetTop(_labelMedianGps, Math.Clamp(projectedPoint.Y - 8, 0, CnvGraph.ActualHeight - _labelMedianGps.ActualHeight));
            _labelMedianGps.Visibility = Visibility.Visible;
        }
        else { _labelMedianGps.Visibility = Visibility.Collapsed; }

        if (_historyMedianWeb.Any())
        {
            var lastPoint = _historyMedianWeb.Last();
            var lastSmooth = smoothWeb.Last();
            var projectedPoint = ProjectPoint(lastSmooth.Time, lastSmooth.Offset, _viewMinX, _viewMaxX, minY, maxY);
            _labelMedianWeb.Text = $"{lastPoint.Offset:F2}";
            Canvas.SetLeft(_labelMedianWeb, Math.Min(projectedPoint.X + 5, CnvGraph.ActualWidth - _labelMedianWeb.ActualWidth - 5));
            Canvas.SetTop(_labelMedianWeb, Math.Clamp(projectedPoint.Y - 8, 0, CnvGraph.ActualHeight - _labelMedianWeb.ActualHeight));
            _labelMedianWeb.Visibility = Visibility.Visible;
        }
        else { _labelMedianWeb.Visibility = Visibility.Collapsed; }
    }

    private List<(double Time, double Offset)> SmoothData(List<(double Time, double Offset)> data)
    {
        if (data.Count < 3) return new List<(double Time, double Offset)>(data);

        var result = new List<(double Time, double Offset)>(data.Count);
        int range = 2; // Fenêtre de 5 points (2 avant, 2 après)

        for (int i = 0; i < data.Count; i++)
        {
            double sum = 0;
            int count = 0;
            for (int j = i - range; j <= i + range; j++)
            {
                if (j >= 0 && j < data.Count)
                {
                    sum += data[j].Offset;
                    count++;
                }
            }
            result.Add((data[i].Time, sum / count));
        }
        return result;
    }

    private void DrawGrid(double minX, double maxX, double minY, double maxY)
    {
        double range = maxY - minY;
        if (range <= 0) return;

        // Détermination du pas (step)
        double rawStep = range / 5.0;
        double step = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        if (rawStep / step >= 5) step *= 5;
        else if (rawStep / step >= 2) step *= 2;

        double startY = Math.Ceiling(minY / step) * step;

        for (double y = startY; y <= maxY; y += step)
        {
            // Ligne horizontale
            double py = CnvGraph.ActualHeight - ((y - minY) / (maxY - minY) * CnvGraph.ActualHeight);
            
            var line = new Line
            {
                X1 = 0, Y1 = py,
                X2 = CnvGraph.ActualWidth, Y2 = py,
                Stroke = Brushes.Gray,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                Opacity = 0.5
            };
            CnvGraph.Children.Insert(0, line);
            _gridElements.Add(line);

            // Texte Valeur
            var tb = new TextBlock
            {
                Text = $"{y:F2}",
                Foreground = Brushes.Gray,
                FontSize = 10,
                Background = Brushes.Black
            };
            Canvas.SetLeft(tb, 5);
            Canvas.SetTop(tb, py - 7);
            CnvGraph.Children.Add(tb);
            _gridElements.Add(tb);
        }

        // Graduation X (Temps)
        double startX = Math.Ceiling(minX / 10.0) * 10.0;
        for (double x = startX; x <= maxX; x += 10)
        {
            double px = ((x - minX) / (maxX - minX)) * CnvGraph.ActualWidth;
            if (px < 0 || px > CnvGraph.ActualWidth) continue;

            bool isMinute = (Math.Abs(x % 60) < 0.1);
            
            var line = new Line
            {
                X1 = px, Y1 = 0,
                X2 = px, Y2 = CnvGraph.ActualHeight,
                Stroke = Brushes.Gray,
                StrokeThickness = isMinute ? 1 : 0.5,
                StrokeDashArray = isMinute ? null : new DoubleCollection { 2, 4 },
                Opacity = 0.3
            };
            CnvGraph.Children.Insert(0, line);
            _gridElements.Add(line);

            if (isMinute)
            {
                var tb = new TextBlock { Text = $"{(int)(x/60)}m", Foreground = Brushes.Gray, FontSize = 10, Background = Brushes.Black };
                Canvas.SetLeft(tb, px + 2);
                Canvas.SetBottom(tb, 2);
                CnvGraph.Children.Add(tb);
                _gridElements.Add(tb);
            }
        }
        
        // Ligne Zéro
        if (minY < 0 && maxY > 0)
        {
            double pyZero = CnvGraph.ActualHeight - ((0 - minY) / (maxY - minY) * CnvGraph.ActualHeight);
            var zeroLine = new Line { X1 = 0, Y1 = pyZero, X2 = CnvGraph.ActualWidth, Y2 = pyZero, Stroke = Brushes.Red, StrokeThickness = 1, Opacity = 0.8 };
            CnvGraph.Children.Insert(0, zeroLine);
            _gridElements.Add(zeroLine);
        }
    }

    private Point ProjectPoint(double x, double y, double minX, double maxX, double minY, double maxY)
    {
        double w = CnvGraph.ActualWidth;
        double h = CnvGraph.ActualHeight;

        double px = ((x - minX) / (maxX - minX)) * w;
        double py = h - ((y - minY) / (maxY - minY) * h);

        return new Point(px, py);
    }

    private async Task ModifyNtpConfigAsync(bool enable)
    {
        string ntpConfPath = _config.NtpConfPath;
        if (!File.Exists(ntpConfPath)) return;

        string action = enable ? "Configuration Mode Calibration" : "Restauration Mode Normal";
        Log($"{action}...");

        await Task.Run(() =>
        {
            try
            {
                var lines = File.ReadAllLines(ntpConfPath).ToList();
                bool modified = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith("server"))
                    {
                        if (line.Contains("127.127."))
                        {
                            // GPS
                            if (enable)
                            {
                                if (lines[i].Contains("prefer"))
                                {
                                    lines[i] = lines[i].Replace("prefer true", "noselect").Replace("prefer", "noselect");
                                    modified = true;
                                }
                            }
                            else
                            {
                                if (lines[i].Contains("noselect"))
                                {
                                    lines[i] = lines[i].Replace("noselect", "prefer");
                                    modified = true;
                                }
                            }
                        }
                        else
                        {
                            // Web
                            if (enable && !lines[i].Contains("minpoll 4 maxpoll 4"))
                            {
                                lines[i] += " minpoll 4 maxpoll 4";
                                modified = true;
                            }
                            else if (!enable && lines[i].Contains("minpoll 4 maxpoll 4"))
                            {
                                lines[i] = lines[i].Replace(" minpoll 4 maxpoll 4", "").Replace("minpoll 4 maxpoll 4", "");
                                modified = true;
                            }
                        }
                    }
                }

                if (modified)
                {
                    File.WriteAllLines(ntpConfPath, lines);
                    Dispatcher.Invoke(() => Log("ntp.conf mis à jour. Redémarrage NTP..."));
                    WindowsServiceHelper.RestartService("NTP");
                    Dispatcher.Invoke(() => Log("NTP Redémarré."));
                }
                _isNtpModified = enable;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log($"Erreur modif ntp.conf: {ex.Message}"));
            }
        });
    }
    private bool _isWebStabilized;

    // --- EVENTS SOURIS (ZOOM / PAN) ---

    private void Graph_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        _isAutoScroll = false;
        double w = CnvGraph.ActualWidth;
        if (w <= 0) return;

        Point mousePos = e.GetPosition(CnvGraph);
        double mouseTime = _viewMinX + (mousePos.X / w) * (_viewMaxX - _viewMinX);
        
        double range = _viewMaxX - _viewMinX;
        double zoomFactor = e.Delta > 0 ? 0.9 : 1.1;
        double newRange = range * zoomFactor;

        // Limite de zoom
        if (newRange < 10) newRange = 10; // Min 10s visible

        // Recalcul des bornes pour garder la souris au même point temporel
        double ratio = (mousePos.X / w);
        _viewMinX = mouseTime - ratio * newRange;
        _viewMaxX = _viewMinX + newRange;

        DrawGraph();
    }

    private void Graph_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _lastMousePos = e.GetPosition(CnvGraph);
        _isDragging = true;
        if (sender is IInputElement el) el.CaptureMouse();
    }

    private void Graph_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging)
        {
            _isAutoScroll = false;
            Point currentPos = e.GetPosition(CnvGraph);
            double deltaPix = currentPos.X - _lastMousePos.X;
            double w = CnvGraph.ActualWidth;
            
            if (w > 0)
            {
                double range = _viewMaxX - _viewMinX;
                double deltaTime = -(deltaPix / w) * range;
                _viewMinX += deltaTime;
                _viewMaxX += deltaTime;
                DrawGraph();
            }
            _lastMousePos = currentPos;
        }
    }

    private void Graph_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDragging = false;
        if (sender is IInputElement el) el.ReleaseMouseCapture();
    }

    private void SetStartButtonState(bool isStart)
    {
        if (isStart)
        {
            // Icône Play (Vert/Bleu)
            BtnStart.Content = new System.Windows.Shapes.Path { Data = Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z"), Fill = Brushes.White, Stretch = Stretch.Uniform, Height = 16, Width = 16 };
            BtnStart.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // #FF007ACC
        }
        else
        {
            // Icône Stop (Rouge)
            BtnStart.Content = new System.Windows.Shapes.Path { Data = Geometry.Parse("M6,6H18V18H6V6Z"), Fill = Brushes.White, Stretch = Stretch.Uniform, Height = 16, Width = 16 };
            BtnStart.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40)); // #FFC62828
        }
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