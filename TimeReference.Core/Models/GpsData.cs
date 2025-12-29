using System;

namespace TimeReference.Core.Models;

/// <summary>
/// Représente les données extraites d'une trame GPS (NMEA).
/// </summary>
public class GpsData
{
    /// <summary>
    /// Date et Heure UTC fournies par le satellite.
    /// </summary>
    public DateTime UtcTime { get; set; }

    /// <summary>
    /// Latitude en degrés décimaux (ex: 48.8566).
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude en degrés décimaux (ex: 2.3522).
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Indique si les données sont valides (Fix GPS acquis).
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Nombre de satellites utilisés pour le calcul.
    /// </summary>
    public int SatelliteCount { get; set; }

    /// <summary>
    /// Précision horizontale (HDOP). Plus c'est bas, mieux c'est.
    /// </summary>
    public double Hdop { get; set; }

    /// <summary>
    /// La trame brute reçue (pour le debug et les logs).
    /// </summary>
    public string RawNmea { get; set; } = string.Empty;

    /// <summary>
    /// Affiche un résumé lisible pour le débogage.
    /// </summary>
    public override string ToString()
    {
        return $"[{UtcTime:HH:mm:ss} UTC] Valid:{IsValid} | Pos: {Latitude:F5}, {Longitude:F5} | Sats: {SatelliteCount}";
    }
}
