using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App
{
    public partial class IqtWindow : Window
    {
        private readonly AppConfig _config;
        private readonly SerialGpsReader _gpsReader;
        private readonly IqtService _iqtService;
        private readonly DispatcherTimer _uiTimer;

        public IqtWindow(AppConfig config)
        {
            InitializeComponent();
            _config = config;
            _gpsReader = new SerialGpsReader();
            _iqtService = new IqtService();

            // Timer pour rafraîchir l'UI toutes les secondes (calcul du score)
            _uiTimer = new DispatcherTimer();
            _uiTimer.Interval = TimeSpan.FromSeconds(1);
            _uiTimer.Tick += UiTimer_Tick;
        this.Loaded += (s, e) => EnsureVisible();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 2. Démarrer la lecture GPS
            _gpsReader.GpsDataReceived += OnGpsDataReceived;
            _gpsReader.ErrorOccurred += OnGpsError;

            await TryConnectAsync();
        }

        private Task TryConnectAsync()
        {
            TxtStatus.Text = "Connexion au GPS...";
            if (BtnRetry != null) BtnRetry.IsEnabled = false;

            // Sécurité : on s'assure que c'est fermé avant de tenter
            _gpsReader.Stop();

            try
            {
                // Tentative directe (sans boucle de retry)
                _gpsReader.Start(_config.SerialPort, _config.BaudRate);

                if (_gpsReader.IsConnected)
                {
                    TxtStatus.Text = $"Connecté à {_config.SerialPort}. Analyse en cours...";
                    _uiTimer.Start();
                }
                else
                {
                    TxtStatus.Text = "Erreur : Impossible d'ouvrir le port COM (Occupé ?).";
                    if (BtnRetry != null) BtnRetry.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Erreur connexion : {ex.Message}";
                if (BtnRetry != null) BtnRetry.IsEnabled = true;
            }
            
            return Task.CompletedTask;
        }

        private void OnGpsDataReceived(GpsData data)
        {
            // On passe la trame brute au service IQT
            if (!string.IsNullOrEmpty(data.RawNmea))
            {
                _iqtService.ProcessNmeaLine(data.RawNmea);
            }
        }

        private void OnGpsError(string error)
        {
            Dispatcher.Invoke(() => 
            {
                TxtStatus.Text = $"Erreur GPS : {error}";
                if (BtnRetry != null) BtnRetry.IsEnabled = true;
            });
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            // Calcul des scores
            var result = _iqtService.Calculate();

            // Mise à jour de l'UI
            UpdateValues(result);
            
            AnimateLed(_gpsReader.IsConnected);
        }

        private void UpdateValues(IqtResult result)
        {
            GaugeScore.Value = result.TotalScore;

            GaugeSnr.Value = result.RawAvgSnr;
            GaugeHdop.Value = result.RawHdop;
            GaugeSat.Value = result.RawSatCount;
        }

        private void AnimateLed(bool isConnected)
        {
            if (LedIndicator == null) return;

            LedIndicator.Fill = isConnected ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red;

            // Effet Flash : On part de 1.0 (Allumé) vers 0.2 (Dim)
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.2, TimeSpan.FromMilliseconds(400));
            LedIndicator.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _uiTimer.Stop();
            _gpsReader.Stop();
        }

        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            await TryConnectAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
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
}