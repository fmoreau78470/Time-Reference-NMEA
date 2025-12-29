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
}