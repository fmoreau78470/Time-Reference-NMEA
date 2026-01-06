using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace TimeReference.Core.Services
{
    public class TranslationManager : INotifyPropertyChanged
    {
        private static TranslationManager? _instance;
        public static TranslationManager Instance => _instance ??= new TranslationManager();

        private Dictionary<string, string> _translations = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        private TranslationManager() { }

        public string CurrentLanguage { get; private set; } = "fr";

        public void LoadLanguage(string cultureCode)
        {
            CurrentLanguage = cultureCode;
            var dict = new Dictionary<string, string>();

            // Chemin : TimeReference.App/bin/Debug/netX.X/lang/fr.json
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang", $"{cultureCode}.json");
            
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loaded != null) dict = loaded;
                }
                catch (Exception)
                {
                    // En cas d'erreur de parsing, on reste vide (ou on loggue)
                }
            }

            _translations = dict;
            
            // Notifie l'interface que TOUTES les propriétés (indexeur) ont changé
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        // Indexeur pour le Binding : Text="{Binding [KEY], Source={x:Static ...}}"
        public string this[string key]
        {
            get => _translations.TryGetValue(key, out string? value) ? value : $"#{key}#";
        }
    }
}