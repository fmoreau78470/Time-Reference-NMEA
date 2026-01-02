using System;
using System.Windows;
using System.Windows.Threading;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Documents;
using TimeReference.Core.Services;

namespace TimeReference.App
{
    public partial class SplashScreenWindow : Window
    {
        private DispatcherTimer? _autoCloseTimer;

        public SplashScreenWindow(bool autoClose = true)
        {
            InitializeComponent();

            // Récupération dynamique de la version
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            TxtVersion.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";

            // Récupération dynamique de l'auteur et de la compagnie
            var author = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
            var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;

            if (!string.IsNullOrEmpty(author)) TxtAuthor.Text = $"Auteur : {author}";
            if (!string.IsNullOrEmpty(company)) TxtCompany.Text = $"Compagnie : {company}";

            // --- Affichage Version NTP ---
            var ntpService = new NtpVersionService();
            string? localNtp = ntpService.GetLocalNtpVersion();
            if (!string.IsNullOrEmpty(localNtp))
            {
                TxtVersion.Text += $"\nNTP : {localNtp}";
            }

            if (autoClose)
            {
                // Timer pour fermer automatiquement après 3 secondes
                _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                _autoCloseTimer.Tick += (s, e) => { _autoCloseTimer.Stop(); Close(); };
                _autoCloseTimer.Start();
            }
            else
            {
                TxtLoading.Visibility = Visibility.Collapsed;
            }

            // Vérification mise à jour en arrière-plan
            Task.Run(async () => 
            {
                string? remoteNtp = await ntpService.GetLatestMeinbergVersionAsync();
                
                Dispatcher.Invoke(() => 
                {
                    if (!string.IsNullOrEmpty(remoteNtp) && !string.IsNullOrEmpty(localNtp))
                    {
                        if (NtpVersionService.CompareNtpVersions(remoteNtp, localNtp) > 0)
                        {
                            // Mise à jour disponible
                            TxtVersion.Text += $" (Dispo : {remoteNtp})";
                            
                            // Message d'invitation et extension du délai
                            TxtLoading.Inlines.Clear();
                            TxtLoading.Inlines.Add(new Run("Mise à jour NTP recommandée ! "));
                            
                            var link = new Hyperlink(new Run("(Cliquer ici)"));
                            link.NavigateUri = new Uri("https://www.meinbergglobal.com/english/sw/ntp.htm");
                            link.RequestNavigate += (s, e) => 
                            {
                                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
                                e.Handled = true;
                            };
                            TxtLoading.Inlines.Add(link);

                            TxtLoading.Foreground = Brushes.OrangeRed;
                            TxtLoading.FontWeight = FontWeights.Bold;
                            TxtLoading.Visibility = Visibility.Visible;

                            if (_autoCloseTimer != null && _autoCloseTimer.IsEnabled)
                            {
                                _autoCloseTimer.Stop();
                                _autoCloseTimer.Interval = TimeSpan.FromSeconds(10);
                                _autoCloseTimer.Start();
                            }
                        }
                        else
                        {
                            // À jour
                            TxtVersion.Text += " (À jour)";
                        }
                    }
                });
            });
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}