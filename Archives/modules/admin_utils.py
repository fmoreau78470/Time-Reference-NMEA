import ctypes
import sys

def is_admin():
    """
    Vérifie si le script est exécuté avec les privilèges administrateur.
    
    Returns:
        bool: True si admin, False sinon.
    """
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except:
        return False

def elevate_privileges():
    """
    Relance le script actuel avec une demande d'élévation de privilèges (UAC).
    Termine le script actuel après le lancement de la nouvelle instance.
    """
    if not is_admin():
        # Récupération des arguments pour relancer à l'identique
        params = " ".join([f'"{arg}"' for arg in sys.argv])
        # ShellExecuteW lance une nouvelle instance en mode 'runas' (admin)
        ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, params, None, 1)
        sys.exit()