using System;
using System.IO;
using System.Text.Json;
using TimeReference.Core.Models;

namespace TimeReference.Core.Services;

public class ConfigService
{
    private readonly string _configPath;

    public ConfigService(string fileName = "config.json")
    {
        // Le fichier sera stocké à côté de l'exécutable (.exe)
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            return new AppConfig(); // Retourne les valeurs par défaut si pas de fichier
        }

        try
        {
            string json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json);
            return config ?? new AppConfig();
        }
        catch
        {
            // En cas de fichier corrompu, on repart sur une config neuve
            return new AppConfig(); 
        }
    }

    public void Save(AppConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(_configPath, json);
    }
}
