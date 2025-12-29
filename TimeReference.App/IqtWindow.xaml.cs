using System;
using System.Windows;
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

            // Tentative directe (sans boucle de retry)
            _gpsReader.Start(_config.SerialPort, _config.BaudRate);

            if (_gpsReader.IsConnected)
            {
                TxtStatus.Text = $"Connecté à {_config.SerialPort}. Analyse en cours...";
                _uiTimer.Start();
            }
            else if (!TxtStatus.Text.StartsWith("Erreur GPS"))
            {
                TxtStatus.Text = "Erreur : Impossible d'ouvrir le port COM (Occupé ?).";
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
            Dispatcher.Invoke(() => TxtStatus.Text = $"Erreur GPS : {error}");
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            // Calcul des scores
            var result = _iqtService.Calculate();

            // Mise à jour de l'UI
            UpdateValues(result);
        }

        private void UpdateValues(IqtResult result)
        {
            TxtTotalScore.Text = $"{result.TotalScore:F1} %";

            // Couleur dynamique du score
            if (result.TotalScore > 80) TxtTotalScore.Foreground = System.Windows.Media.Brushes.LimeGreen;
            else if (result.TotalScore > 50) TxtTotalScore.Foreground = System.Windows.Media.Brushes.Orange;
            else TxtTotalScore.Foreground = System.Windows.Media.Brushes.Red;

            TxtSnrVal.Text = $"{result.RawAvgSnr:F1} dB";
            TxtHdopVal.Text = $"{result.RawHdop:F1}";
            TxtSatVal.Text = $"{result.RawSatCount}";
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
    }
}