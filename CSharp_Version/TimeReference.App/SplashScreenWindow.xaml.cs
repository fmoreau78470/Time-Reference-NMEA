using System;
using System.Windows;
using System.Windows.Threading;
using System.Reflection;

namespace TimeReference.App
{
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
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

            // Timer pour fermer automatiquement après 3 secondes
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) => { timer.Stop(); Close(); };
            timer.Start();
        }
    }
}