// Création du fichier : d:\Francis\Documents\code\Time reference NMEA\CSharp_Version\TimeReference.App\ClockVarWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Threading;
using TimeReference.Core.Services;

namespace TimeReference.App
{
    public partial class ClockVarWindow : Window
    {
        private readonly NtpQueryService _ntpService;
        private readonly DispatcherTimer _timer;

        public ClockVarWindow()
        {
            InitializeComponent();
            _ntpService = new NtpQueryService();

            // Timer pour rafraîchir toutes les secondes
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => RefreshData();
            _timer.Start();

            // Premier chargement immédiat
            RefreshData();
        }

        private void RefreshData()
        {
            string rawData = _ntpService.GetClockVar();
            TxtRaw.Text = rawData;

            // Extraction et affichage des valeurs clés
            LblFudge.Text = _ntpService.ExtractValue(rawData, "fudgetime2");
            LblStratum.Text = _ntpService.ExtractValue(rawData, "stratum");
            LblRefId.Text = _ntpService.ExtractValue(rawData, "refid");
            
            string poll = _ntpService.ExtractValue(rawData, "poll");
            string noreply = _ntpService.ExtractValue(rawData, "noreply");
            LblHealth.Text = $"Poll: {poll} / NoReply: {noreply}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }
    }
}
