// Création du fichier : d:\Francis\Documents\code\Time reference NMEA\CSharp_Version\TimeReference.App\ClockVarWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using TimeReference.Core.Services;

namespace TimeReference.App
{
    public partial class ClockVarWindow : Window
    {
        private readonly NtpQueryService _ntpService;
        private readonly DispatcherTimer _timer;
        private readonly ObservableCollection<NtpVarItem> _items = new();
        private bool _isPaused = false;
        
        // Dictionnaire des descriptions pour les tooltips
        private readonly Dictionary<string, string> _descriptions = new()
        {
            { "associd", "Identifiant d'association NTP interne." },
            { "status", "Statut hexadécimal du pilote." },
            { "device", "Nom du périphérique (ex: NMEA GPS Clock)." },
            { "timecode", "Dernière trame NMEA brute reçue par le pilote." },
            { "poll", "Nombre total de requêtes envoyées au pilote." },
            { "noreply", "Nombre de fois où le pilote n'a pas répondu (Perte de signal ?)." },
            { "badformat", "Nombre de trames NMEA mal formées ou corrompues." },
            { "baddata", "Nombre de données invalides reçues." },
            { "fudgetime1", "Correction de temps 1 (Interne)." },
            { "fudgetime2", "Correction de temps 2 (Fudge Time utilisateur)." },
            { "stratum", "Strate NTP (1 = Source primaire comme GPS)." },
            { "refid", "Identifiant de référence (ex: GPS, PPS)." },
            { "flags", "Drapeaux de configuration du pilote." }
        };

        public ClockVarWindow()
        {
            InitializeComponent();
            _ntpService = new NtpQueryService();
            DgVars.ItemsSource = _items;

            // Ajout des descriptions pour les champs décodés
            _descriptions.Add("GPS_Time", "Heure UTC extraite de la trame GPRMC.");
            _descriptions.Add("GPS_Pos", "Position Latitude / Longitude décodée.");
            _descriptions.Add("GPS_Status", "Statut du Fix GPS (A=Valide, V=Alerte).");
            _descriptions.Add("GPS_Date", "Date UTC extraite de la trame GPRMC.");
            _descriptions.Add("GPS_Speed", "Vitesse sol en nœuds.");
            _descriptions.Add("GPS_Course", "Route fond (Cap) en degrés.");
            _descriptions.Add("GPS_MagVar", "Déclinaison magnétique.");
            _descriptions.Add("GPS_Mode", "Indicateur de mode (A=Auto, D=Diff).");

            // Timer pour rafraîchir toutes les secondes
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => RefreshData();
            _timer.Start();

            // Premier chargement immédiat
            RefreshData();
        this.Loaded += (s, e) => EnsureVisible();
        }

        private void RefreshData()
        {
            if (_isPaused) return;

            string rawData = _ntpService.GetClockVar();
            if (TxtRaw != null) TxtRaw.Text = rawData;
            var newData = new Dictionary<string, string>();
            string? properTimecode = null;
            
            if (!string.IsNullOrWhiteSpace(rawData))
            {
                // 1. Extraction robuste du timecode (qui contient des virgules et casse le split)
                int tcIndex = rawData.IndexOf("timecode=\"");
                if (tcIndex != -1)
                {
                    int start = tcIndex + 10; // Longueur de timecode="
                    int end = rawData.IndexOf("\"", start);
                    if (end != -1)
                    {
                        properTimecode = rawData.Substring(start, end - start);
                    }
                }

                // Nettoyage des sauts de ligne éventuels et découpage par virgule
                var parts = rawData.Replace("\r", "").Replace("\n", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var part in parts)
                {
                    var kvp = part.Split('=');
                    if (kvp.Length == 2)
                    {
                        string key = kvp[0].Trim();
                        string value = kvp[1].Trim().Trim('"'); // Enlève les guillemets
                        newData[key] = value;
                    }
                }

                // 2. Restauration du timecode complet s'il a été trouvé
                if (properTimecode != null)
                {
                    newData["timecode"] = properTimecode;
                }
            }

            // Décodage GPRMC si présent
            if (newData.ContainsKey("timecode"))
            {
                DecodeGprmc(newData["timecode"], newData);
            }

            // Mise à jour intelligente de la collection (pour ne pas casser les tooltips)
            // 1. Mise à jour ou Ajout
            foreach (var kvp in newData)
            {
                var existingItem = _items.FirstOrDefault(i => i.Key == kvp.Key);
                if (existingItem != null)
                {
                    if (existingItem.Value != kvp.Value) existingItem.Value = kvp.Value;
                }
                else
                {
                    string desc = _descriptions.ContainsKey(kvp.Key) ? _descriptions[kvp.Key] : "Paramètre interne NTP.";
                    _items.Add(new NtpVarItem { Key = kvp.Key, Value = kvp.Value, Description = desc });
                }
            }

            // 2. Suppression des éléments absents
            var keysToRemove = _items.Where(i => !newData.ContainsKey(i.Key)).Select(i => i.Key).ToList();
            foreach (var key in keysToRemove)
            {
                var itemToRemove = _items.First(i => i.Key == key);
                _items.Remove(itemToRemove);
            }
        }

        private void DecodeGprmc(string nmea, Dictionary<string, string> data)
        {
            if (string.IsNullOrEmpty(nmea) || !nmea.StartsWith("$GPRMC")) return;
            
            var parts = nmea.Split(',');
            // $GPRMC,HHMMSS.ss,Status,Lat,N/S,Lon,E/W,Spd,Cog,Date,...
            if (parts.Length > 9)
            {
                // Time
                if (parts[1].Length >= 6)
                {
                    string t = parts[1];
                    data["GPS_Time"] = $"{t.Substring(0, 2)}:{t.Substring(2, 2)}:{t.Substring(4, 2)} UTC";
                }

                // Status
                data["GPS_Status"] = parts[2] == "A" ? "Valide (A)" : "Invalide (V)";

                // Position
                string lat = parts[3];
                string latDir = parts[4];
                string lon = parts[5];
                string lonDir = parts[6];

                if (!string.IsNullOrEmpty(lat) && !string.IsNullOrEmpty(lon))
                {
                    string latDec = NmeaToDecimal(lat, latDir);
                    string lonDec = NmeaToDecimal(lon, lonDir);
                    data["GPS_Pos"] = $"{latDec}, {lonDec}";
                }

                // Speed (Index 7)
                if (parts.Length > 7 && !string.IsNullOrEmpty(parts[7]))
                {
                    data["GPS_Speed"] = $"{parts[7]} kn";
                }

                // Course (Index 8)
                if (parts.Length > 8 && !string.IsNullOrEmpty(parts[8]))
                {
                    data["GPS_Course"] = $"{parts[8]}°";
                }

                // Date (Index 9) - Format DDMMYY
                if (parts.Length > 9 && parts[9].Length == 6)
                {
                    string d = parts[9];
                    data["GPS_Date"] = $"{d.Substring(0, 2)}/{d.Substring(2, 2)}/20{d.Substring(4, 2)}";
                }

                // Magnetic Variation (Index 10 & 11)
                if (parts.Length > 11 && !string.IsNullOrEmpty(parts[10]))
                {
                    string magVar = parts[10];
                    string magDir = parts[11];
                    if (magDir.Contains("*")) magDir = magDir.Split('*')[0];
                    data["GPS_MagVar"] = $"{magVar}° {magDir}";
                }

                // Mode (Index 12)
                if (parts.Length > 12 && !string.IsNullOrEmpty(parts[12]))
                {
                    string mode = parts[12];
                    if (mode.Contains("*")) mode = mode.Split('*')[0];
                    data["GPS_Mode"] = mode;
                }
            }
        }

        private string NmeaToDecimal(string pos, string dir)
        {
            if (double.TryParse(pos, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                int degrees = (int)(val / 100);
                double minutes = val - (degrees * 100);
                double decimalDeg = degrees + (minutes / 60.0);
                if (dir == "S" || dir == "W") decimalDeg *= -1;
                return decimalDeg.ToString("F5", CultureInfo.InvariantCulture);
            }
            return "";
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            BtnPause.Content = _isPaused ? "Reprendre" : "Pause";
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (TxtRaw != null && !string.IsNullOrEmpty(TxtRaw.Text))
            {
                Clipboard.SetText(TxtRaw.Text);
                MessageBox.Show("Réponse brute copiée dans le presse-papier.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
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

    // Classe simple pour l'affichage dans la DataGrid
    public class NtpVarItem : INotifyPropertyChanged
    {
        private string _value = "";
        
        public string Key { get; set; } = "";
        public string Value 
        { 
            get => _value; 
            set { if (_value != value) { _value = value; OnPropertyChanged(); } } 
        }
        public string Description { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
