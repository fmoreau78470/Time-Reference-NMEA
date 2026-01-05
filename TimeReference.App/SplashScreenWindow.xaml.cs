using System;
using System.Windows;
using System.Windows.Threading;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Documents;
using System.IO;
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
            // var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;

            if (!string.IsNullOrEmpty(author)) TxtAuthor.Text = $"Auteur : {author}";
            // if (!string.IsNullOrEmpty(company)) TxtCompany.Text = $"Compagnie : {company}";

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
                // Vérification mise à jour Application (GitHub)
                string? latestAppTag = await GetLatestAppVersionAsync();

                string? remoteNtp = await ntpService.GetLatestMeinbergVersionAsync(config.MeinbergUrl);
                
                Dispatcher.Invoke(() => 
                {
                    // 1. Mise à jour Application
                    if (!string.IsNullOrEmpty(latestAppTag))
                    {
                        var currentVersion = assembly.GetName().Version;
                        string cleanTag = latestAppTag.TrimStart('v');
                        if (Version.TryParse(cleanTag, out Version? remoteVersion) && currentVersion != null)
                        {
                            if (remoteVersion > currentVersion)
                            {
                                TxtVersion.Inlines.Add(new LineBreak());
                                TxtVersion.Inlines.Add(new Run($"Update dispo : {latestAppTag}") { Foreground = Brushes.Magenta, FontWeight = FontWeights.Bold });
                                
                                var link = new Hyperlink(new Run(" (Télécharger)"))
                                {
                                    NavigateUri = new Uri("https://github.com/fmoreau78470/Time-Reference-NMEA/releases/latest"),
                                    ToolTip = "Télécharger la nouvelle version"
                                };
                                link.RequestNavigate += (s, e) => 
                                {
                                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
                                    e.Handled = true;
                                };
                                TxtVersion.Inlines.Add(link);

                                // On arrête le timer pour laisser l'utilisateur voir
                                if (_autoCloseTimer != null && _autoCloseTimer.IsEnabled)
                                {
                                    _autoCloseTimer.Stop();
                                    TxtLoading.Text = "Nouvelle version détectée !";
                                    TxtLoading.Foreground = Brushes.Magenta;
                                    TxtLoading.Visibility = Visibility.Visible;
                                }
                            }
                        }
                    }

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

        this.Loaded += (s, e) => EnsureVisible();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnDocLocal_Click(object sender, RoutedEventArgs e)
        {
            // Stratégie Offline : On cherche le site statique local (index.html)
            string localDoc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "site", "index.html");
            
            if (File.Exists(localDoc))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(localDoc) { UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show("Erreur lors de l'ouverture de la documentation locale : " + ex.Message); }
            }
            else
            {
                MessageBox.Show($"La documentation locale n'est pas trouvée.\nChemin recherché : {localDoc}\n(Elle est incluse uniquement via l'installateur)", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnDocWeb_Click(object sender, RoutedEventArgs e)
        {
            string docUrl = "https://fmoreau78470.github.io/Time-Reference-NMEA/";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(docUrl) { UseShellExecute = true }); } catch { }
        }

        private void BtnKofi_Click(object sender, RoutedEventArgs e)
        {
            string supportUrl = "https://ko-fi.com/francismoreau";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(supportUrl) { UseShellExecute = true });
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

    private async Task<string?> GetLatestAppVersionAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TimeReferenceNMEA");
            client.Timeout = TimeSpan.FromSeconds(3);
            
            var response = await client.GetStringAsync("https://api.github.com/repos/fmoreau78470/Time-Reference-NMEA/releases/latest");
            
            using var doc = System.Text.Json.JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("tag_name", out var tagElement))
            {
                return tagElement.GetString();
            }
        }
        catch { }
        return null;
    }
    }
}