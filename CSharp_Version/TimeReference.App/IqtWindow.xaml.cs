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
        private bool _ntpWasRunning = false;

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
            TxtStatus.Text = "Arrêt du service NTP en cours...";
            
            // 1. Arrêter NTP pour libérer le port COM
            await Task.Run(() =>
            {
                var status = WindowsServiceHelper.GetStatus("NTP");
                if (status == System.ServiceProcess.ServiceControllerStatus.Running)
                {
                    _ntpWasRunning = true;
                    WindowsServiceHelper.StopService("NTP");
                }
            });

            TxtStatus.Text = "Connexion au GPS...";

            // 2. Démarrer la lecture GPS
            _gpsReader.GpsDataReceived += OnGpsDataReceived;
            _gpsReader.ErrorOccurred += OnGpsError;
            _gpsReader.Start(_config.SerialPort, _config.BaudRate);

            if (_gpsReader.IsConnected)
            {
                TxtStatus.Text = $"Connecté à {_config.SerialPort}. Analyse en cours...";
                _uiTimer.Start();
            }
            else
            {
                TxtStatus.Text = "Erreur : Impossible d'ouvrir le port COM.";
            }
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
            UpdateGauges(result);
        }

        private void UpdateGauges(IqtResult result)
        {
            // 1. Score Global (Aiguille)
            // Map 0..100 vers -135..+135 degrés
            double angle = (result.TotalScore * 2.7) - 135;
            MainNeedleTransform.Angle = angle;
            TxtTotalScore.Text = $"{result.TotalScore:F1} %";

            // Couleur dynamique du score
            if (result.TotalScore > 80) TxtTotalScore.Foreground = System.Windows.Media.Brushes.LimeGreen;
            else if (result.TotalScore > 50) TxtTotalScore.Foreground = System.Windows.Media.Brushes.Orange;
            else TxtTotalScore.Foreground = System.Windows.Media.Brushes.Red;

            // 2. Sous-jauges
            PbSnr.Value = result.SnrScore;
            TxtSnrVal.Text = $"{result.RawAvgSnr:F1} dB";

            PbHdop.Value = result.HdopScore;
            TxtHdopVal.Text = $"{result.RawHdop:F1}";

            PbSat.Value = result.SatScore;
            TxtSatVal.Text = $"{result.RawSatCount}";
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _uiTimer.Stop();
            _gpsReader.Stop();

            if (_ntpWasRunning)
            {
                TxtStatus.Text = "Redémarrage du service NTP...";
                // On évite de bloquer la fermeture de la fenêtre, mais on lance la tâche
                // Note : Dans une vraie app, on pourrait afficher un spinner bloquant.
                await Task.Run(() => WindowsServiceHelper.StartService("NTP"));
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}