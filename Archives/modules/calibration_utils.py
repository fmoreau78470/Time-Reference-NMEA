import os
import statistics
import logging
from datetime import datetime

def get_loopstats_path(stats_dir="C:\\Program Files (x86)\\NTP\\etc\\"):
    """
    Retourne le chemin du fichier loopstats du jour.
    Format: loopstats.YYYYMMDD
    """
    date_str = datetime.now().strftime("%Y%m%d")
    return os.path.join(stats_dir, f"loopstats.{date_str}")

def calculate_new_fudge(loopstats_path, current_fudge):
    """
    Calcule le nouveau fudge basé sur la médiane des offsets dans loopstats.
    
    Args:
        loopstats_path (str): Chemin vers le fichier loopstats.
        current_fudge (float): La valeur actuelle du fudge time2.
        
    Returns:
        tuple: (median_offset, new_fudge) ou (None, None) si erreur.
    """
    logger = logging.getLogger("GPS_NTP_App")
    offsets = []
    
    if not os.path.exists(loopstats_path):
        logger.error(f"Fichier loopstats introuvable: {loopstats_path}")
        return None, None

    try:
        with open(loopstats_path, 'r') as f:
            for line in f:
                parts = line.split()
                # Format loopstats: MJD Sec Offset Drift ...
                if len(parts) >= 3:
                    try:
                        # Colonne 3 : Offset en secondes
                        offset = float(parts[2])
                        offsets.append(offset)
                    except ValueError:
                        continue
    except Exception as e:
        logger.error(f"Erreur lecture loopstats: {e}")
        return None, None

    if not offsets:
        logger.warning("Aucune donnée d'offset trouvée dans loopstats.")
        return None, None

    median_offset = statistics.median(offsets)
    # Formule : Nouveau Fudge = Actuel + Offset Médian
    new_fudge = current_fudge + median_offset
    
    return median_offset, new_fudge