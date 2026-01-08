using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using TimeReference.App.MarkupExtensions;
using TimeReference.Core.Services;

namespace TimeReference.App
{
    public partial class LoopstatsWindow : Window
    {
        private List<LoopStatData> _data = new List<LoopStatData>();
        
        // État de la vue (Zoom/Pan)
        private DateTime _viewMinT;
        private DateTime _viewMaxT;
        private double _viewMinYLeft; // ms
        private double _viewMaxYLeft; // ms
        private double _viewMinYRight; // ppm
        private double _viewMaxYRight; // ppm
        private bool _isAutoFit = true;
        private Point _lastMousePos;
        private bool _isDragging;

        // Visibilité
        private bool _showOffset = true;
        private bool _showJitter = true;
        private bool _showDrift = true;

        public LoopstatsWindow()
        {
            InitializeComponent();
            LoadFileList();
        }

        private void LoadFileList()
        {
            string ntpLogPath = @"C:\Program Files (x86)\NTP\etc";
            if (!Directory.Exists(ntpLogPath)) return;

            var files = Directory.GetFiles(ntpLogPath, "loopstats*");
            var items = new List<LoopstatsFileItem>();

            foreach (var file in files)
            {
                var filename = System.IO.Path.GetFileName(file);
                string datePart = "";

                // Tentative de parsing : priorité à la virgule (demande utilisateur), sinon point (standard NTP)
                if (filename.Contains(','))
                {
                    var parts = filename.Split(',');
                    if (parts.Length > 1) datePart = parts[1];
                }
                else if (filename.Contains('.'))
                {
                    var parts = filename.Split('.');
                    if (parts.Length > 1) datePart = parts[1];
                }

                if (DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                {
                    items.Add(new LoopstatsFileItem { FilePath = file, Date = date });
                }
            }

            CmbFiles.ItemsSource = items.OrderByDescending(i => i.Date).ToList();
            CmbFiles.DisplayMemberPath = "DisplayName";

            if (CmbFiles.Items.Count > 0) CmbFiles.SelectedIndex = 0;
        }

        private void LoadFile(string path)
        {
            try
            {
                _data.Clear();
                _isAutoFit = true; // Reset zoom au chargement
                
                string content;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    content = sr.ReadToEnd();
                }
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Format Loopstats: day second offset drift error stability poll
                    // Ex: 56686 1234.567 0.000123 15.4 0.000012 0.003 6
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        if (int.TryParse(parts[0], out int mjd) &&
                            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double seconds) &&
                            double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double offset) &&
                            double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double drift) &&
                            double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double jitter))
                        {
                            _data.Add(new LoopStatData 
                            { 
                                Time = MjdToDateTime(mjd, seconds), 
                                Offset = offset,
                                Drift = drift,
                                Jitter = jitter
                            });
                        }
                    }
                }

                DrawGraph();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lecture fichier: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DateTime MjdToDateTime(int mjd, double seconds)
        {
            // MJD 0 = 17 Nov 1858
            DateTime mjdEpoch = new DateTime(1858, 11, 17, 0, 0, 0, DateTimeKind.Utc);
            return mjdEpoch.AddDays(mjd).AddSeconds(seconds);
        }

        private void DrawGraph()
        {
            CnvGraph.Children.Clear();
            if (_data.Count < 2) return;

            double w = CnvGraph.ActualWidth;
            double h = CnvGraph.ActualHeight;
            if (w == 0 || h == 0) return;

            // Marges pour les axes (Left for Offset/Jitter, Right for Drift, Bottom for Time)
            double mx = 60; 
            double my = 40;
            double mr = 60; // Marge droite pour Drift

            double graphW = w - mx - mr;
            double graphH = h - my * 2;

            if (graphW <= 0 || graphH <= 0) return;

            // Calcul des échelles (Auto ou Zoom)
            if (_isAutoFit)
            {
                _viewMinT = _data.Min(d => d.Time);
                _viewMaxT = _data.Max(d => d.Time);

                // Axe Gauche (Offset/Jitter) en MS (* 1000)
                double minOff = _data.Min(d => d.Offset) * 1000.0;
                double maxOff = _data.Max(d => d.Offset) * 1000.0;
                double minJit = _data.Min(d => d.Jitter) * 1000.0;
                double maxJit = _data.Max(d => d.Jitter) * 1000.0;
                
                _viewMinYLeft = Math.Min(minOff, minJit);
                _viewMaxYLeft = Math.Max(maxOff, maxJit);
                ExpandRange(ref _viewMinYLeft, ref _viewMaxYLeft);

                // Axe Droit (Drift) en PPM
                _viewMinYRight = _data.Min(d => d.Drift);
                _viewMaxYRight = _data.Max(d => d.Drift);
                ExpandRange(ref _viewMinYRight, ref _viewMaxYRight);
            }

            double totalSeconds = (_viewMaxT - _viewMinT).TotalSeconds;
            if (totalSeconds <= 0) totalSeconds = 1;

            // Labels des axes (Unités)
            var lblUnitL = new TextBlock { Text = "ms", Foreground = Brushes.Cyan, FontSize = 11, FontWeight = FontWeights.Bold };
            Canvas.SetLeft(lblUnitL, 5); Canvas.SetTop(lblUnitL, 5);
            CnvGraph.Children.Add(lblUnitL);

            var lblUnitR = new TextBlock { Text = "ppm", Foreground = Brushes.Yellow, FontSize = 11, FontWeight = FontWeights.Bold };
            Canvas.SetRight(lblUnitR, 5); Canvas.SetTop(lblUnitR, 5);
            CnvGraph.Children.Add(lblUnitR);

            // Titre Axe X (UTC)
            var lblAxisX = new TextBlock { Text = "UTC", Foreground = Brushes.Gray, FontSize = 10, FontStyle = FontStyles.Italic, Width = 100, TextAlignment = TextAlignment.Center };
            Canvas.SetLeft(lblAxisX, mx + (graphW / 2) - 50);
            Canvas.SetTop(lblAxisX, h - 15);
            CnvGraph.Children.Add(lblAxisX);

            // --- Dessin Grille et Axes (Dynamique 1-2-5) ---

            // 1. Axe Gauche (Offset/Jitter)
            double rangeL = _viewMaxYLeft - _viewMinYLeft;
            double stepL = CalculateNiceStep(rangeL);
            double startL = Math.Floor(_viewMinYLeft / stepL) * stepL;
            if (startL < _viewMinYLeft) startL += stepL;

            for (double val = startL; val <= _viewMaxYLeft + (stepL * 0.001); val += stepL)
            {
                if (Math.Abs(val) < stepL * 1e-4) val = 0; // Snap to 0

                double yRatio = (val - _viewMinYLeft) / rangeL;
                double yPos = my + graphH - (yRatio * graphH);

                bool isZero = (val == 0);
                var line = new Line
                {
                    X1 = mx, Y1 = yPos,
                    X2 = mx + graphW, Y2 = yPos,
                    Stroke = isZero ? Brushes.White : Brushes.DarkGray,
                    StrokeThickness = isZero ? 1.0 : 0.5,
                    StrokeDashArray = isZero ? null : new DoubleCollection { 2, 2 },
                    Opacity = isZero ? 0.8 : 0.5
                };
                CnvGraph.Children.Add(line);

                var txtL = new TextBlock { Text = val.ToString("G5"), Foreground = Brushes.Cyan, FontSize = 10, TextAlignment = TextAlignment.Right, Width = 55 };
                Canvas.SetLeft(txtL, 0);
                Canvas.SetTop(txtL, yPos - 7);
                CnvGraph.Children.Add(txtL);
            }

            // 2. Axe Droit (Drift)
            double rangeR = _viewMaxYRight - _viewMinYRight;
            double stepR = CalculateNiceStep(rangeR);
            double startR = Math.Floor(_viewMinYRight / stepR) * stepR;
            if (startR < _viewMinYRight) startR += stepR;

            for (double val = startR; val <= _viewMaxYRight + (stepR * 0.001); val += stepR)
            {
                if (Math.Abs(val) < stepR * 1e-4) val = 0;

                double yRatio = (val - _viewMinYRight) / rangeR;
                double yPos = my + graphH - (yRatio * graphH);

                bool isZero = (val == 0);
                if (isZero)
                {
                    var zeroLine = new Line
                    {
                        X1 = mx, Y1 = yPos,
                        X2 = mx + graphW, Y2 = yPos,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 1.0,
                        Opacity = 0.6
                    };
                    CnvGraph.Children.Add(zeroLine);
                }

                // Tick mark
                var line = new Line { X1 = mx + graphW, Y1 = yPos, X2 = mx + graphW + 5, Y2 = yPos, Stroke = Brushes.Yellow, StrokeThickness = 1 };
                CnvGraph.Children.Add(line);

                var txtR = new TextBlock { Text = val.ToString("G5"), Foreground = Brushes.Yellow, FontSize = 10, TextAlignment = TextAlignment.Left, Width = 55 };
                Canvas.SetLeft(txtR, mx + graphW + 5);
                Canvas.SetTop(txtR, yPos - 7);
                CnvGraph.Children.Add(txtR);
            }

            // Axe Temps (X) - Heures entières
            // On cherche la première heure entière après MinT
            DateTime t = _viewMinT.AddMinutes(60 - _viewMinT.Minute).AddSeconds(-_viewMinT.Second); 
            // Arrondi à l'heure pile
            t = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0);

            // Calcul du pas pour ne pas surcharger (max 15 labels)
            double hoursSpan = (_viewMaxT - _viewMinT).TotalHours;
            int stepHours = 1;
            if (hoursSpan > 15) stepHours = (int)Math.Ceiling(hoursSpan / 15.0);

            while (t <= _viewMaxT)
            {
                if (t >= _viewMinT)
                {
                    double xRatio = (t - _viewMinT).TotalSeconds / totalSeconds;
                    double xPos = mx + (xRatio * graphW);

                var line = new Line { X1 = xPos, Y1 = my + graphH, X2 = xPos, Y2 = my + graphH + 5, Stroke = Brushes.Gray };
                CnvGraph.Children.Add(line);

                var txtT = new TextBlock { Text = t.ToString("HH:mm"), Foreground = Brushes.Gray, FontSize = 10, TextAlignment = TextAlignment.Center, Width = 40 };
                Canvas.SetLeft(txtT, xPos - 20);
                Canvas.SetTop(txtT, my + graphH + 5);
                CnvGraph.Children.Add(txtT);
                }
                t = t.AddHours(stepHours);
            }

            // Tracé Courbes
            // Note: Offset et Jitter sont multipliés par 1000 pour passer en ms
            if (_showOffset)
                DrawPolyline(_data.Select(d => new Point((d.Time - _viewMinT).TotalSeconds, d.Offset * 1000.0)), _viewMinYLeft, _viewMaxYLeft, totalSeconds, graphW, graphH, mx, my, Brushes.Cyan);
            
            if (_showJitter)
                DrawPolyline(_data.Select(d => new Point((d.Time - _viewMinT).TotalSeconds, d.Jitter * 1000.0)), _viewMinYLeft, _viewMaxYLeft, totalSeconds, graphW, graphH, mx, my, Brushes.Magenta);
            
            if (_showDrift)
                DrawPolyline(_data.Select(d => new Point((d.Time - _viewMinT).TotalSeconds, d.Drift)), _viewMinYRight, _viewMaxYRight, totalSeconds, graphW, graphH, mx, my, Brushes.Yellow);
        }

        private void ExpandRange(ref double min, ref double max)
        {
            double range = max - min;
            if (range == 0) range = (min == 0) ? 1 : Math.Abs(min) * 0.1;
            min -= range * 0.05;
            max += range * 0.05;
        }

        private void DrawPolyline(IEnumerable<Point> points, double minY, double maxY, double maxX, double w, double h, double mx, double my, Brush color)
        {
            var pl = new Polyline { Stroke = color, StrokeThickness = 1 };
            double rangeY = maxY - minY;
            foreach (var p in points)
            {
                double x = mx + (p.X / maxX) * w;
                double y = my + h - ((p.Y - minY) / rangeY * h);
                pl.Points.Add(new Point(x, y));
            }
            CnvGraph.Children.Add(pl);
        }

        private double CalculateNiceStep(double range)
        {
            if (range == 0) return 1;
            double roughStep = range / 6.0; // Viser environ 6 divisions
            
            double powerOf10 = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
            double normalizedStep = roughStep / powerOf10;
            
            if (normalizedStep < 1.5) return 1 * powerOf10;
            if (normalizedStep < 3.5) return 2 * powerOf10;
            return 5 * powerOf10;
        }

        private void CnvGraph_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawGraph();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CmbFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFiles.SelectedItem is LoopstatsFileItem item)
            {
                LoadFile(item.FilePath);
                BtnRefresh.Visibility = (item.Date.Date == DateTime.Today) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void BtnResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _isAutoFit = true;
            DrawGraph();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (CmbFiles.SelectedItem is LoopstatsFileItem item)
            {
                LoadFile(item.FilePath);
            }
        }

        private void Chk_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Content is string content)
            {
                bool isChecked = cb.IsChecked == true;
                if (content == "Offset") _showOffset = isChecked;
                else if (content == "Jitter") _showJitter = isChecked;
                else if (content == "Drift") _showDrift = isChecked;
                DrawGraph();
            }
        }

        // --- Gestion Souris (Zoom/Pan) ---

        private void Graph_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_data.Count < 2) return;
            _isAutoFit = false;

            double zoomFactor = e.Delta > 0 ? 0.9 : 1.1;
            Point mousePos = e.GetPosition(CnvGraph);
            double w = CnvGraph.ActualWidth;
            double h = CnvGraph.ActualHeight;
            double mx = 60, my = 30, mr = 60;
            double graphW = w - mx - mr;
            double graphH = h - my * 2;
            
            if (graphW <= 0 || graphH <= 0) return;

            double relX = (mousePos.X - mx) / graphW;
            double relY = (h - my - mousePos.Y) / graphH; // Y inversé (0 en bas)

            // Zoom X (Temps) - Toujours actif
            double totalSeconds = (_viewMaxT - _viewMinT).TotalSeconds;
            double newTotalSeconds = totalSeconds * zoomFactor;
            // On centre le zoom sur la souris
            DateTime timeAtMouse = _viewMinT.AddSeconds(totalSeconds * relX);
            _viewMinT = timeAtMouse.AddSeconds(-newTotalSeconds * relX);
            _viewMaxT = _viewMinT.AddSeconds(newTotalSeconds);

            // Zoom Y (Valeurs) - Si Ctrl enfoncé
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Axe Gauche
                double rangeL = _viewMaxYLeft - _viewMinYLeft;
                double valLAtMouse = _viewMinYLeft + rangeL * relY;
                double newRangeL = rangeL * zoomFactor;
                _viewMinYLeft = valLAtMouse - newRangeL * relY;
                _viewMaxYLeft = _viewMinYLeft + newRangeL;

                // Axe Droit
                double rangeR = _viewMaxYRight - _viewMinYRight;
                double valRAtMouse = _viewMinYRight + rangeR * relY;
                double newRangeR = rangeR * zoomFactor;
                _viewMinYRight = valRAtMouse - newRangeR * relY;
                _viewMaxYRight = _viewMinYRight + newRangeR;
            }

            DrawGraph();
        }

        private void Graph_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePos = e.GetPosition(CnvGraph);
            _isDragging = true;
            if (sender is IInputElement el) el.CaptureMouse();
        }

        private void Graph_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            if (sender is IInputElement el) el.ReleaseMouseCapture();
        }

        private void Graph_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _data.Count > 0)
            {
                _isAutoFit = false;
                Point currentPos = e.GetPosition(CnvGraph);
                double dx = currentPos.X - _lastMousePos.X;
                double dy = currentPos.Y - _lastMousePos.Y;
                
                double w = CnvGraph.ActualWidth;
                double h = CnvGraph.ActualHeight;
                double mx = 60, my = 30, mr = 60;
                double graphW = w - mx - mr;
                double graphH = h - my * 2;

                // Pan X
                double totalSeconds = (_viewMaxT - _viewMinT).TotalSeconds;
                double dt = -(dx / graphW) * totalSeconds;
                _viewMinT = _viewMinT.AddSeconds(dt);
                _viewMaxT = _viewMaxT.AddSeconds(dt);

                // Pan Y
                double rangeL = _viewMaxYLeft - _viewMinYLeft;
                double dYL = (dy / graphH) * rangeL;
                _viewMinYLeft += dYL;
                _viewMaxYLeft += dYL;

                double rangeR = _viewMaxYRight - _viewMinYRight;
                double dYR = (dy / graphH) * rangeR;
                _viewMinYRight += dYR;
                _viewMaxYRight += dYR;

                _lastMousePos = currentPos;
                DrawGraph();
            }
        }
    }

    public class LoopStatData
    {
        public DateTime Time { get; set; }
        public double Offset { get; set; }
        public double Drift { get; set; }
        public double Jitter { get; set; }
    }

    public class LoopstatsFileItem
    {
        public string FilePath { get; set; } = "";
        public DateTime Date { get; set; }
        public string DisplayName => Date.ToShortDateString();
    }
}