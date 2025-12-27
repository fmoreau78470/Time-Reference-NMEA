import sys
import os
import json
import ctypes
import tkinter as tk
from modules.admin_utils import elevate_privileges, is_admin
from modules.logger_utils import setup_logger
from modules.service_utils import is_w32time_active, is_service_installed, is_service_active, stop_service, start_service
from modules.gui_main import GPSNTPApp
from modules.ntp_utils import generate_ntp_conf_content, read_file_content, write_file_content

def check_single_instance():
    """
    Vérifie si une autre instance de l'application est déjà en cours d'exécution
    en utilisant un Mutex nommé Windows.
    """
    mutex_name = "Global\\GPS_NTP_Time_Reference_App_Mutex"
    ERROR_ALREADY_EXISTS = 183
    
    kernel32 = ctypes.windll.kernel32
    # Création du mutex. Le handle doit rester vivant tant que l'app tourne.
    mutex = kernel32.CreateMutexW(None, False, mutex_name)
    last_error = kernel32.GetLastError()
    
    if last_error == ERROR_ALREADY_EXISTS:
        return False, mutex
    return True, mutex

# Variable globale pour maintenir le handle du Mutex vivant
_app_mutex = None

def main():
    """
    Point d'entrée principal de l'application.
    Orchestre les vérifications et le lancement de l'interface.
    """
    # 1. Gestion des droits administrateur (Spec Générale)
    # Si le programme n'est pas admin, il se relance et quitte cette instance.
    if not is_admin():
        elevate_privileges()
        return

    # 1b. Vérification Instance Unique (Spec 1)
    # Placé après l'élévation pour éviter qu'elle ne détecte l'instance non-admin en cours de fermeture.
    global _app_mutex
    is_unique, _app_mutex = check_single_instance()
    if not is_unique:
        ctypes.windll.user32.MessageBoxW(
            0, 
            "Une instance du programme est déjà en cours d'exécution.", 
            "Information", 
            0x40 | 0x1000  # MB_ICONINFORMATION | MB_SYSTEMMODAL
        )
        sys.exit(0)

    # 2. Initialisation du Logging (Spec Générale)
    # Doit être fait après l'élévation pour être sûr d'avoir les droits d'écriture si nécessaire
    logger = setup_logger()
    logger.info("=== Démarrage de l'application ===")
    logger.info("Droits administrateur confirmés.")

    # 3. Vérification du service W32Time (Spec 1)
    if is_w32time_active():
        # Si NTP est lancé, on considère que c'est une configuration valide (ou tolérée)
        if is_service_installed("NTP") and is_service_active("NTP"):
            logger.warning("Service W32Time détecté actif mais le service NTP est fonctionnel. L'application continue.")
        else:
            logger.warning("Service W32Time détecté actif. Arrêt de l'application requis.")
            GPSNTPApp.show_w32time_warning()
            logger.info("Fermeture de l'application suite au conflit W32Time.")
            sys.exit(0)

    # 4. Gestion de la configuration NTP (Spec 2)
    config = {}
    config_path = ""
    
    try:
        # Chargement config.json
        config_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'config.json')
        if os.path.exists(config_path):
            with open(config_path, 'r') as f:
                config = json.load(f)
            
            # Initialisation par défaut du mode UTC
            if "utc_mode" not in config:
                config["utc_mode"] = False
            
            # Vérification présence service NTP
            if is_service_installed("NTP"):
                target_ntp_conf = config.get("config_ntp", "")
                new_conf_content = generate_ntp_conf_content(config)
                
                # Sauvegarde locale systématique (Spec 2)
                local_conf_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'ntp.conf')
                write_file_content(local_conf_path, new_conf_content)
                logger.info(f"Copie locale de ntp.conf créée : {local_conf_path}")

                # Comparaison avec le fichier existant
                current_content = read_file_content(target_ntp_conf)
                
                # Normalisation pour comparaison (strip whitespace)
                if current_content is None or current_content.strip() != new_conf_content.strip():
                    logger.info("Différence détectée dans ntp.conf ou fichier manquant.")
                    
                    # Proposition à l'utilisateur
                    if GPSNTPApp.show_config_proposal(new_conf_content, current_content, target_ntp_conf):
                        logger.info("Utilisateur a accepté la mise à jour de ntp.conf.")
                        
                        # Gestion du service pour l'écriture
                        service_was_running = is_service_active("NTP")
                        if service_was_running:
                            stop_service("NTP")
                        
                        if write_file_content(target_ntp_conf, new_conf_content):
                            logger.info(f"Fichier {target_ntp_conf} mis à jour avec succès.")
                        
                        # Redémarrage ou Démarrage
                        start_service("NTP")
                    else:
                        logger.info("Utilisateur a refusé la mise à jour de ntp.conf.")
            else:
                logger.warning("Service 'NTP' non trouvé. Spec 2 ignorée partiellement.")
    except Exception as e:
        logger.error(f"Erreur lors du traitement de la Spec 2 (Config NTP): {e}")

    # 5. Lancement de l'interface graphique (Spec Générale)
    try:
        root = tk.Tk()
        app = GPSNTPApp(root, config=config, config_path=config_path)
        root.mainloop()
    except Exception as e:
        logger.critical(f"Erreur critique dans l'interface graphique : {e}", exc_info=True)
        sys.exit(1)
    finally:
        logger.info("=== Arrêt de l'application ===")

if __name__ == "__main__":
    main()