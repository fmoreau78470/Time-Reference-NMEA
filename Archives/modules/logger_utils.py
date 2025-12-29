import logging
import os
from datetime import datetime

def setup_logger(name="GPS_NTP_App"):
    """
    Configure le système de logging.
    Crée un dossier 'logs' s'il n'existe pas et un fichier de log journalier.
    
    Args:
        name (str): Le nom du logger.
        
    Returns:
        logging.Logger: L'objet logger configuré.
    """
    # Chemin absolu basé sur l'emplacement de ce fichier script (dans modules/)
    # On remonte d'un cran pour mettre les logs à la racine du projet
    log_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "logs")
    if not os.path.exists(log_dir):
        os.makedirs(log_dir)
        
    log_filename = f"log_{datetime.now().strftime('%Y-%m-%d')}.txt"
    log_path = os.path.join(log_dir, log_filename)
    
    logger = logging.getLogger(name)
    logger.setLevel(logging.DEBUG) # On capture tout
    
    # Éviter d'ajouter des handlers multiples si la fonction est rappelée
    if not logger.handlers:
        # Handler Fichier (encodage utf-8 pour les accents)
        file_handler = logging.FileHandler(log_path, encoding='utf-8')
        file_handler.setLevel(logging.DEBUG)
        file_formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
        file_handler.setFormatter(file_formatter)
        
        # Handler Console
        console_handler = logging.StreamHandler()
        console_handler.setLevel(logging.INFO)
        console_formatter = logging.Formatter('%(levelname)s: %(message)s')
        console_handler.setFormatter(console_formatter)
        
        logger.addHandler(file_handler)
        logger.addHandler(console_handler)
        
    return logger