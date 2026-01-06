using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TimeReference.Core.Models;
using TimeReference.Core.Services;

namespace TimeReference.App
{
    public partial class LogWindow : Window
    {
        private readonly LogReaderService _logService;
        private List<LogEntry> _allEntries = new List<LogEntry>();

        public LogWindow()
        {
            InitializeComponent();
            _logService = new LogReaderService();
            LoadFileList();
        this.Loaded += (s, e) => EnsureVisible();
        }

        private void LoadFileList()
        {
            var files = _logService.GetLogFiles();
            CmbFiles.ItemsSource = files;
            if (files.Count > 0) CmbFiles.SelectedIndex = 0;
        }

        private void CmbFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFiles.SelectedItem is string filename)
            {
                _allEntries = _logService.ReadLog(filename);
                ApplyFilters();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadFileList();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (CmbFiles.SelectedItem is string filename)
            {
                // Protection : ne pas effacer le log d'aujourd'hui
                if (filename.Contains(DateTime.Now.ToString("yyyyMMdd")))
                {
                    MessageBox.Show(TranslationManager.Instance["MSG_DELETE_CURRENT_LOG_ERROR"], TranslationManager.Instance["TITLE_WARNING"], MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show(string.Format(TranslationManager.Instance["MSG_CONFIRM_DELETE"], filename), TranslationManager.Instance["TITLE_CONFIRMATION"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _logService.DeleteLog(filename);
                    LoadFileList();
                }
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            // Protection : lors de l'initialisation, les contrôles UI peuvent être encore nuls
            if (_allEntries == null || TxtSearch == null || GridLogs == null || CmbLevel == null) return;

            var filtered = _allEntries.AsEnumerable();

            // Filtre Niveau
            if (CmbLevel.SelectedItem is ComboBoxItem item && CmbLevel.SelectedIndex > 0)
            {
                filtered = filtered.Where(x => x.Level == item.Content.ToString());
            }

            // Filtre Texte
            string search = TxtSearch.Text.ToLower();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(x => x.Message.ToLower().Contains(search));
            }

            GridLogs.ItemsSource = filtered.ToList();
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