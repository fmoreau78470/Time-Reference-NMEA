import logging

def clamp(n, minn, maxn):
    """Restreint une valeur entre minn et maxn."""
    return max(min(maxn, n), minn)

class IQTCalculator:
    """
    Calculateur de l'Indice de Qualité Temporelle (IQT).
    Implémente l'algorithme décrit dans le mémoire du projet.
    """
    def __init__(self):
        self.sat_count = 0
        self.hdop = 9.9 # Valeur par défaut (mauvaise)
        self.snr_values = [] # Liste des SNR du cycle en cours
        self.iqt_history = [] # Historique pour la moyenne glissante (10 sec)
        self.logger = logging.getLogger("GPS_NTP_App")

    def process_line(self, line):
        """
        Analyse une trame NMEA et met à jour les indicateurs bruts.
        Gère $GPGGA, $GPGSA, $GPGSV.
        """
        if not line.startswith("$"): return
        
        # Nettoyage checksum
        try:
            content = line.split('*')[0]
            parts = content.split(',')
            cmd = parts[0][-3:] # Extrait GGA, GSA, GSV...

            if cmd == "GGA":
                # $GPGGA,time,lat,ns,lon,ew,qual,sats,hdop,...
                # sats est à l'index 7
                if len(parts) > 7 and parts[7]:
                    self.sat_count = int(parts[7])

            elif cmd == "GSA":
                # $GPGSA,mode,mode,sat...sat,pdop,hdop,vdop
                # hdop est à l'index 16
                if len(parts) > 16 and parts[16]:
                    self.hdop = float(parts[16])

            elif cmd == "GSV":
                # $GPGSV,num_msgs,msg_num,num_sats, (prn, el, az, snr) * 4
                if len(parts) > 3:
                    msg_num = int(parts[2])
                    # Si c'est le premier message d'une série, on réinitialise la liste des SNR
                    if msg_num == 1:
                        self.snr_values = []
                    
                    # Les blocs de satellites commencent à l'index 4
                    # Chaque bloc fait 4 champs : PRN, Elev, Azim, SNR
                    num_fields = len(parts)
                    for i in range(4, num_fields, 4):
                        if i + 3 < num_fields:
                            snr_str = parts[i+3]
                            if snr_str: # Si le champ SNR n'est pas vide
                                try:
                                    self.snr_values.append(int(snr_str))
                                except ValueError:
                                    pass
        except Exception:
            pass # On ignore les trames malformées

    def get_iqt(self):
        """Calcule l'IQT instantané lissé sur 10 secondes."""
        # 1. Score SNR (Moyenne des 4 meilleurs)
        top_snr = sorted(self.snr_values, reverse=True)[:4]
        avg_snr = sum(top_snr) / len(top_snr) if top_snr else 0
        score_snr = clamp((avg_snr - 20) * 5, 0, 100)

        # 2. Score Géométrie (HDOP)
        score_hdop = clamp((4.0 - self.hdop) * 33.3, 0, 100)

        # 3. Score Quantité
        score_qty = clamp((self.sat_count - 3) * 20, 0, 100)

        # Indice pondéré
        current_iqt = (score_snr * 0.5) + (score_hdop * 0.3) + (score_qty * 0.2)

        # Moyenne glissante sur 10 échantillons (supposant 1 appel par seconde)
        self.iqt_history.append(current_iqt)
        if len(self.iqt_history) > 10:
            self.iqt_history.pop(0)

        final_iqt = sum(self.iqt_history) / len(self.iqt_history)
        return final_iqt, score_snr, score_hdop, score_qty