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

            // Chargement de la configuration pour l'URL
            var configService = new ConfigService();
            var config = configService.Load();

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
                TxtVersion.Inlines.Add(new LineBreak());
                TxtVersion.Inlines.Add(new Run("NTP : "));
                var link = new Hyperlink(new Run(localNtp))
                {
                    NavigateUri = new Uri(config.MeinbergUrl),
                    ToolTip = "Site officiel Meinberg"
                };
                link.RequestNavigate += (s, e) => 
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
                    e.Handled = true;
                };
                TxtVersion.Inlines.Add(link);
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
                string? remoteNtp = await ntpService.GetLatestMeinbergVersionAsync(config.MeinbergUrl);
                
                Dispatcher.Invoke(() => 
                {
                    if (!string.IsNullOrEmpty(remoteNtp) && !string.IsNullOrEmpty(localNtp))
                    {
                        if (NtpVersionService.CompareNtpVersions(remoteNtp, localNtp) > 0)
                        {
                            // Mise à jour disponible
                            TxtVersion.Inlines.Add(new Run($" (Dispo : {remoteNtp})") { Foreground = Brushes.OrangeRed });
                            
                            // Message d'invitation et extension du délai
                            TxtLoading.Inlines.Clear();
                            TxtLoading.Inlines.Add(new Run("Mise à jour NTP recommandée ! "));
                            
                            var link = new Hyperlink(new Run("(Cliquer ici)"));
                            link.NavigateUri = new Uri(config.MeinbergUrl);
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
                            TxtVersion.Inlines.Add(new Run(" (À jour)") { Foreground = Brushes.Green });
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