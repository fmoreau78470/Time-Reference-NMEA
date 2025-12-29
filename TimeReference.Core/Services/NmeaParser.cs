using System;
using System.Globalization;
using TimeReference.Core.Models;

namespace TimeReference.Core.Services;

public class NmeaParser
{
    /// <summary>
    /// Analyse une phrase NMEA brute et retourne un objet GpsData.
    /// </summary>
    public GpsData Parse(string rawNmea)
    {
        var data = new GpsData { RawNmea = rawNmea };

        if (string.IsNullOrWhiteSpace(rawNmea) || !rawNmea.StartsWith("$"))
        {
            return data;
        }

        // Nettoyage : on enlève le checksum (*XX) et les espaces
        var cleanLine = rawNmea.Trim();
        if (cleanLine.Contains("*"))
        {
            cleanLine = cleanLine.Split('*')[0];
        }

        var parts = cleanLine.Split(',');

        // On gère principalement $GPRMC (Recommended Minimum) ou $GNRMC (GNSS)
        if (parts[0] == "$GPRMC" || parts[0] == "$GNRMC")
        {
            ParseRmc(parts, data);
        }
        
        return data;
    }

    private void ParseRmc(string[] parts, GpsData data)
    {
        // Format attendu (min 10 champs): 
        // Index: 0      1         2 3       4 5        6 7   8   9
        //        $GPRMC,HHMMSS.ss,A,llll.ll,a,yyyyy.yy,a,x.x,x.x,ddmmyy,...

        if (parts.Length < 10) return;

        // 1. Validité : A = Active (Valid), V = Void (Invalid)
        data.IsValid = (parts[2] == "A");

        // 2. Heure et Date
        string timeStr = parts[1]; // HHMMSS.ss
        string dateStr = parts[9]; // DDMMYY

        if (timeStr.Length >= 6 && dateStr.Length == 6)
        {
            // On concatène Date + Heure (sans les ms pour simplifier) pour créer un DateTime
            string dateTimeStr = dateStr + timeStr.Substring(0, 6); 
            
            if (DateTime.TryParseExact(dateTimeStr, "ddMMyyHHmmss", 
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime dt))
            {
                data.UtcTime = dt;
            }
        }

        // Si invalide, on s'arrête souvent ici, mais on a au moins l'heure.
        if (!data.IsValid) return;

        // 3. Latitude (Format NMEA: DDMM.MMMM)
        if (double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double latRaw))
        {
            data.Latitude = NmeaToDecimal(latRaw, parts[4]);
        }

        // 4. Longitude (Format NMEA: DDDMM.MMMM)
        if (double.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out double lonRaw))
        {
            data.Longitude = NmeaToDecimal(lonRaw, parts[6]);
        }
    }

    /// <summary>
    /// Convertit le format NMEA (DDMM.MMMM) en degrés décimaux classiques.
    /// </summary>
    private double NmeaToDecimal(double nmeaPos, string quadrant)
    {
        double degrees = Math.Floor(nmeaPos / 100);
        double minutes = nmeaPos - (degrees * 100);
        double decimalDegrees = degrees + (minutes / 60);

        if (quadrant == "S" || quadrant == "W")
        {
            decimalDegrees *= -1;
        }

        return decimalDegrees;
    }
}
