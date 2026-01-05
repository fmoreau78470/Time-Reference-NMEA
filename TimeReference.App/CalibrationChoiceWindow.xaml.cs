using System.Windows;
using TimeReference.Core.Models;

namespace TimeReference.App;

public partial class CalibrationChoiceWindow : Window
{
    private readonly AppConfig _config;

    public CalibrationChoiceWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        this.Loaded += (s, e) => EnsureVisible();
    }

    private void BtnSimple_Click(object sender, RoutedEventArgs e)
    {
        var simpleWin = new SimpleCalibrationWindow(_config);
        simpleWin.Owner = this.Owner;
        this.Close();
        simpleWin.ShowDialog();
    }

    private void BtnExpert_Click(object sender, RoutedEventArgs e)
    {
        // On ouvre la fenêtre Expert et on ferme celle-ci
        var expertWin = new ExpertCalibrationWindow(_config);
        expertWin.Owner = this.Owner; // Le propriétaire devient MainWindow
        this.Close();
        expertWin.ShowDialog();
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