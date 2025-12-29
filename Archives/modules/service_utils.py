import subprocess
import logging

def is_service_active(service_name):
    """
    Vérifie si un service est en cours d'exécution (RUNNING).
    """
    try:
        output = subprocess.check_output(
            ["sc", "query", service_name], 
            universal_newlines=True,
            creationflags=0x08000000 
        )
        return "RUNNING" in output
    except Exception:
        return False

def is_service_installed(service_name):
    """
    Vérifie si un service est installé sur le système.
    """
    try:
        subprocess.check_output(
            ["sc", "query", service_name], 
            universal_newlines=True,
            creationflags=0x08000000 
        )
        return True
    except subprocess.CalledProcessError:
        return False
    except Exception:
        return False

def stop_service(service_name):
    """Arrête un service Windows via 'net stop'."""
    logger = logging.getLogger("GPS_NTP_App")
    logger.info(f"Tentative d'arrêt du service {service_name}...")
    try:
        subprocess.run(["net", "stop", service_name], check=True, creationflags=0x08000000)
        return True
    except Exception as e:
        logger.error(f"Erreur lors de l'arrêt du service {service_name}: {e}")
        return False

def start_service(service_name):
    """Démarre un service Windows via 'net start'."""
    logger = logging.getLogger("GPS_NTP_App")
    logger.info(f"Tentative de démarrage du service {service_name}...")
    try:
        subprocess.run(["net", "start", service_name], check=True, creationflags=0x08000000)
        return True
    except Exception as e:
        logger.error(f"Erreur lors du démarrage du service {service_name}: {e}")
        return False

def is_w32time_active():
    """
    Vérifie si le service Windows Time (W32Time) est en cours d'exécution.
    
    Returns:
        bool: True si le service est 'RUNNING', False sinon.
    """
    return is_service_active("W32Time")