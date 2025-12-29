using System;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App
{
    public partial class SettingsWindow : Window
    {
        private readonly ConfigService _configService;
        private AppConfig _config = new AppConfig();

        public SettingsWindow()
        {
            InitializeComponent();
            _configService = new ConfigService();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                _config = _configService.Load();

                TxtSerialPort.Text = _config.SerialPort;
                CmbBaudRate.Text = _config.BaudRate.ToString();
                TxtFudge.Text = _config.Time2Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                TxtNtpPath.Text = _config.NtpConfPath;
                
                // Chargement des serveurs (Liste -> Texte multiligne)
                if (_config.Servers != null)
                {
                    TxtServers.Text = string.Join(Environment.NewLine, _config.Servers);
                }
                
                TxtServerOptions.Text = _config.ServerOptions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur chargement config : {ex.Message}");
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _config.SerialPort = TxtSerialPort.Text;
                
                if (int.TryParse(CmbBaudRate.Text, out int baud))
                    _config.BaudRate = baud;

                if (double.TryParse(TxtFudge.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double fudge))
                    _config.Time2Value = fudge;

                _config.NtpConfPath = TxtNtpPath.Text;

                // Sauvegarde des serveurs (Texte multiligne -> Liste)
                _config.Servers = TxtServers.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                _config.ServerOptions = TxtServerOptions.Text;

                _configService.Save(_config);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur sauvegarde : {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnMonitor_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("Ouverture du Moniteur NTP (ClockVar) depuis Paramètres.");
            var monitorWindow = new ClockVarWindow();
            monitorWindow.Owner = this;
            monitorWindow.Closed += (s, args) => Logger.Info("Fermeture du Moniteur NTP (ClockVar).");
            monitorWindow.Show();
        }
    }
}
