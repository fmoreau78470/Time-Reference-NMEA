using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TimeReference.Core.Services
{
    // Structure pour retourner les résultats détaillés
    public class IqtResult
    {
        public double TotalScore { get; set; } // Score global 0-100
        public double SnrScore { get; set; }   // Score partiel SNR
        public double HdopScore { get; set; }  // Score partiel HDOP
        public double SatScore { get; set; }   // Score partiel Satellites
        
        // Valeurs brutes pour l'affichage
        public double RawAvgSnr { get; set; }
        public double RawHdop { get; set; }
        public int RawSatCount { get; set; }
    }

    public class IqtService
    {
        private int _satelliteCount = 0;
        private double _hdop = 99.9; // Valeur par défaut "mauvaise"
        private List<int> _snrList = new List<int>();

        /// <summary>
        /// Analyse une ligne NMEA pour mettre à jour les indicateurs de qualité.
        /// </summary>
        /// <param name="line">Trame NMEA brute ($GP...)</param>
        public void ProcessNmeaLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("$")) return;

            // Nettoyage du checksum (ex: *7A) et saut de ligne
            var cleanLine = line.Trim().Split('*')[0];
            var parts = cleanLine.Split(',');

            if (line.StartsWith("$GPGGA"))
            {
                // ,time,lat,NS,lon,EW,quality,numSV,HDOP,...
                // numSV est à l'index 7
                if (parts.Length > 7 && int.TryParse(parts[7], out int count))
                {
                    _satelliteCount = count;
                }
            }
            else if (line.StartsWith("$GPGSA"))
            {
                // ,mode,mode,sv1,sv2...sv12,PDOP,HDOP,VDOP
                // Les 12 slots de satellites sont fixes dans la norme, donc HDOP est à l'index 16.
                if (parts.Length > 16 && double.TryParse(parts[16], NumberStyles.Any, CultureInfo.InvariantCulture, out double hdop))
                {
                    _hdop = hdop;
                }
            }
            else if (line.StartsWith("$GPGSV"))
            {
                // ,numMsg,msgNum,numSV, prn,elev,az,snr, ...
                if (parts.Length > 2 && int.TryParse(parts[2], out int msgNum))
                {
                    // Si c'est le premier message de la séquence (1/x), on reset la liste pour un nouveau cycle
                    if (msgNum == 1)
                    {
                        _snrList.Clear();
                    }
                }

                // Les blocs satellites commencent à l'index 4, et font 4 champs chacun (PRN, Elev, Azim, SNR)
                // On boucle tant qu'il reste assez de champs pour un bloc complet
                for (int i = 4; i <= parts.Length - 4; i += 4)
                {
                    // Le SNR est le 4ème champ du bloc (donc i+3)
                    // Exemple : index 4 (PRN), 5 (Elev), 6 (Az), 7 (SNR)
                    if (i + 3 < parts.Length)
                    {
                        string snrStr = parts[i + 3];
                        if (!string.IsNullOrWhiteSpace(snrStr) && int.TryParse(snrStr, out int snr))
                        {
                            _snrList.Add(snr);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calcule l'Indice de Qualité Temporelle (IQT) basé sur les dernières données reçues.
        /// </summary>
        public IqtResult Calculate()
        {
            // 1. Score SNR (Moyenne des 4 meilleurs signaux)
            // On suppose que le récepteur utilise les meilleurs signaux pour le fix.
            double avgSnr = 0;
            if (_snrList.Count > 0)
            {
                var topSnr = _snrList.OrderByDescending(x => x).Take(4).ToList();
                avgSnr = topSnr.Average();
            }
            // Normalisation : 40 dB-Hz = 100%, 20 dB-Hz = 0%
            double scoreSnr = Clamp((avgSnr - 20) * 5.0, 0, 100);

            // 2. Score HDOP (Précision géométrique)
            // 1.0 ou moins = 100%, 4.0 ou plus = 0%
            // Facteur 33.33 car (4-1)=3 et 100/3 = 33.33
            double scoreHdop = Clamp((4.0 - _hdop) * 33.33, 0, 100);

            // 3. Score Quantité (Nombre de satellites)
            // 8 satellites ou plus = 100%, 3 ou moins = 0%
            // Facteur 20 car (8-3)=5 et 100/5 = 20
            double scoreQty = Clamp((_satelliteCount - 3) * 20.0, 0, 100);

            // Pondération finale selon spécifications
            // SNR: 50%, HDOP: 30%, Qty: 20%
            double total = (scoreSnr * 0.5) + (scoreHdop * 0.3) + (scoreQty * 0.2);

            return new IqtResult
            {
                TotalScore = Math.Round(total, 1),
                SnrScore = Math.Round(scoreSnr, 1),
                HdopScore = Math.Round(scoreHdop, 1),
                SatScore = Math.Round(scoreQty, 1),
                RawAvgSnr = Math.Round(avgSnr, 1),
                RawHdop = _hdop,
                RawSatCount = _satelliteCount
            };
        }

        private double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
