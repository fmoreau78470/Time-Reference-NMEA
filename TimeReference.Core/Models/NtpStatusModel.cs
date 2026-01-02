using System;

namespace TimeReference.Core.Models;

public class NtpStatusModel
{
    // Données issues de ntpq -p (Peers)
    public double Offset { get; set; }
    public double Jitter { get; set; }
    public int Reach { get; set; } // Valeur convertie en entier (ex: "377" octal -> 255 décimal)
    public int PeerStratum { get; set; }
    public string PeerRefId { get; set; } = string.Empty;

    // Données issues de ntpq -c clockvar (Driver)
    public int DriverStratum { get; set; }
    public string DriverRefId { get; set; } = string.Empty;
    public int Poll { get; set; }
    public int NoReply { get; set; }
    public int BadFormat { get; set; }
    public string Timecode { get; set; } = string.Empty;

    // Données issues de ntpq -c rv (System Variables) - Prévision pour plus tard
    public double RootDispersion { get; set; }
    
    // Score de santé calculé
    public double HealthScore { get; set; } = 100;

    public NtpStatusModel Clone()
    {
        return (NtpStatusModel)this.MemberwiseClone();
    }

    public double CalculateHealthScore(NtpStatusModel? previous)
    {
        if (previous == null) return 100;

        // 1. Timecode Frozen (Critical) - GPS Figé
        // Si le timecode est identique au précédent (sur 10s), le GPS ne met plus à jour l'heure.
        if (!string.IsNullOrEmpty(this.Timecode) && this.Timecode == previous.Timecode)
        {
            return 0; 
        }

        double malus = 0;

        // 2. Protocol Errors (Delta)
        // On pénalise si les compteurs d'erreurs augmentent
        if (this.BadFormat > previous.BadFormat) malus += 30;
        if (this.NoReply > previous.NoReply) malus += 20;

        // 3. Synchronization Quality
        // Stratum 16 = Non synchronisé (ou 0 si non initialisé correctement)
        if (this.PeerStratum >= 16) malus += 60;

        // Offset > 128ms (Seuil critique NTP)
        if (Math.Abs(this.Offset) > 128) malus += 40;

        // Reach (Octal 377 = 11111111 = 8 succès)
        // On compte les bits à 0 sur les 8 derniers essais
        // Chaque bit manquant = -10 points
        int reach = this.Reach;
        int missingBits = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((reach & 1) == 0) missingBits++;
            reach >>= 1;
        }
        malus += (missingBits * 10);

        // Fast Recovery : Si tout est parfait (Stratum 1 et aucun malus), on remonte à 100 direct
        // Cela évite l'inertie de l'ancien algorithme
        if (malus == 0 && this.PeerStratum <= 1 && this.PeerStratum > 0)
        {
            return 100;
        }

        return Math.Max(0, 100 - malus);
    }
}