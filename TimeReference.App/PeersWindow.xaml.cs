using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimeReference.Core.Models;

namespace TimeReference.App;

public partial class PeersWindow : Window
{
    private readonly double _opacityValue;

    public PeersWindow(AppConfig config)
    {
        InitializeComponent();

        // Appliquer la politique de transparence
        _opacityValue = config.MiniModeOpacity > 0.1 ? config.MiniModeOpacity : 1.0;
        this.Opacity = _opacityValue;

        this.MouseEnter += (s, e) => this.Opacity = 1.0;
        this.MouseLeave += (s, e) => this.Opacity = _opacityValue;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Permet de déplacer la fenêtre en cliquant n'importe où
        if (e.ChangedButton == MouseButton.Left) this.DragMove();
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Fermeture par double clic
        this.Close();
    }

    public void UpdatePeers(string output)
    {
        PnlPeers.Children.Clear();

        if (string.IsNullOrWhiteSpace(output))
        {
            PnlPeers.Children.Add(new TextBlock { Text = "Pas de données NTP", Foreground = Brushes.Gray, FontStyle = FontStyles.Italic });
            return;
        }

        var lines = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var tb = new TextBlock { Text = line, FontFamily = new FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(0, 1, 0, 1) };

            if (line.Contains("remote") && line.Contains("refid"))
            {
                tb.FontWeight = FontWeights.Bold;
                tb.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            }
            else if (line.Length > 0)
            {
                // Coloration selon le code Tally (premier caractère)
                switch (line[0])
                {
                    case '*': tb.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor"); break;
                    case 'o': tb.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor"); break;
                    case '+': tb.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor"); break;
                    case '-': tb.SetResourceReference(TextBlock.ForegroundProperty, "WarningColor"); break;
                    case 'x': tb.SetResourceReference(TextBlock.ForegroundProperty, "ErrorColor"); break;
                    case '.': tb.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText"); break;
                    default: tb.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText"); break;
                }
            }
            else
            {
                tb.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
            }
            PnlPeers.Children.Add(tb);
        }
    }
}