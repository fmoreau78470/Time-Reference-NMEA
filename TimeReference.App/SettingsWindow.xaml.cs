using System;
using System.Linq;
using System.IO;
using System.Text.Json;
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

            // Mise à jour dynamique du label de pourcentage
            SldMiniOpacity.ValueChanged += (s, e) => { if (LblOpacityValue != null) LblOpacityValue.Text = $"{e.NewValue:F0}%"; };
            LoadSettings();
        this.Loaded += (s, e) => EnsureVisible();
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
                TxtMeinbergUrl.Text = _config.MeinbergUrl;

                // Chargement des paramètres du Mode Mini
                ChkMiniTop.IsChecked = _config.MiniModeAlwaysOnTop;

                // Assure que l'opacité est dans les bornes du slider et convertit en pourcentage
                double opacityPercent = (_config.MiniModeOpacity >= 0.2 && _config.MiniModeOpacity <= 1.0) ? _config.MiniModeOpacity * 100 : 100;
                SldMiniOpacity.Value = opacityPercent;
                LblOpacityValue.Text = $"{opacityPercent:F0}%";
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

                _config.ServerOptions = "iburst";
                
                // Validation basique de l'URL
                if (Uri.TryCreate(TxtMeinbergUrl.Text, UriKind.Absolute, out Uri? uriResult) 
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    _config.MeinbergUrl = TxtMeinbergUrl.Text;
                }
                else
                {
                    // On sauvegarde quand même pour ne pas bloquer l'utilisateur, mais on pourrait ajouter un avertissement
                    _config.MeinbergUrl = TxtMeinbergUrl.Text;
                }

                // Sauvegarde des paramètres du mode Mini
                if (ChkMiniTop != null) _config.MiniModeAlwaysOnTop = ChkMiniTop.IsChecked == true;
                if (SldMiniOpacity != null) _config.MiniModeOpacity = SldMiniOpacity.Value / 100.0;

                // Sauvegarde via le service (pour la session en cours)
                _configService.Save(_config);

                // FORCE la sauvegarde dans le fichier local pour la persistance (contourne le problème du dossier bin)
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText("config.json", json);
                
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

        private void BtnTestUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Uri.TryCreate(TxtMeinbergUrl.Text, UriKind.Absolute, out Uri? uriResult) 
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uriResult.AbsoluteUri) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("L'URL n'est pas valide.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir le lien : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBrowseNtp_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Fichiers de configuration (*.conf)|*.conf|Tous les fichiers (*.*)|*.*",
                Title = "Sélectionner le fichier ntp.conf"
            };

            if (!string.IsNullOrWhiteSpace(TxtNtpPath.Text) && File.Exists(TxtNtpPath.Text))
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(TxtNtpPath.Text);
            }

            if (openFileDialog.ShowDialog() == true)
            {
                TxtNtpPath.Text = openFileDialog.FileName;
            }
        }

        private void BtnFindServers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.ntppool.org") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir le lien : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
