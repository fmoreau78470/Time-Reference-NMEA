import re
import os
import logging
import subprocess

def generate_ntp_conf_content(config):
    """
    Génère le contenu de ntp.conf à partir du template 'ntp.template' et de la config.
    """
    # 1. Localisation du template (à la racine, un niveau au-dessus de modules/)
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    template_path = os.path.join(base_dir, 'ntp.template')
    
    # 2. Lecture du template
    content = read_file_content(template_path)
    if not content:
        # Fallback de sécurité si le template est introuvable
        return "# ERREUR CRITIQUE : Le fichier 'ntp.template' est introuvable à la racine de l'application."

    # 3. Récupération des variables de configuration
    serial_port = config.get("serial_port", "COM1")
    time2_value = config.get("time2_value", "0.1350")
    servers = config.get("servers", [])
    server_options = config.get("server_options", "iburst")
    
    # Extraction du numéro de port (ex: "COM3" -> "3")
    match = re.search(r'\d+', serial_port)
    port_number = match.group() if match else "1"
    
    # Construction du bloc serveurs
    if servers:
        server_block = "\n".join([f"server {s} {server_options}" for s in servers])
    else:
        server_block = f"pool pool.ntp.org {server_options}"

    # 4. Remplacement des variables dans le contenu
    replacements = {
        "{{ COM_PORT }}": port_number,
        "{{ TIME2_VALUE }}": time2_value,
        "{{ SERVERS_BLOCK }}": server_block
    }
    
    for key, value in replacements.items():
        content = content.replace(key, value)
    
    return content

def read_file_content(path):
    """Lit le contenu d'un fichier texte."""
    if not os.path.exists(path):
        return None
    try:
        with open(path, 'r', encoding='utf-8', errors='ignore') as f:
            return f.read()
    except Exception as e:
        logging.getLogger("GPS_NTP_App").error(f"Erreur lecture fichier {path}: {e}")
        return None

def write_file_content(path, content):
    """Écrit le contenu dans un fichier."""
    try:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        return True
    except Exception as e:
        logging.getLogger("GPS_NTP_App").error(f"Erreur écriture fichier {path}: {e}")
        return False

_cached_ntpq_cmd = None

def get_ntpq_command():
    """Retourne la commande ntpq valide (chemin complet si nécessaire)."""
    global _cached_ntpq_cmd
    if _cached_ntpq_cmd:
        return _cached_ntpq_cmd
        
    commands = ["ntpq", r"C:\Program Files (x86)\NTP\bin\ntpq.exe", r"C:\Program Files\NTP\bin\ntpq.exe"]
    for cmd in commands:
        try:
            # On teste juste si l'exécutable existe et est lançable
            subprocess.run([cmd, "-c", "quit"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=0x08000000)
            _cached_ntpq_cmd = cmd
            return cmd
        except:
            continue
    return "ntpq"

def get_gps_reach(com_port_number):
    """
    Exécute ntpq -pn et récupère le reach pour 127.127.20.X.
    Retourne int (reach en décimal) ou -1 si erreur/non trouvé.
    """
    logger = logging.getLogger("GPS_NTP_App")
    cmd = get_ntpq_command()

    try:
        # creationflags=0x08000000 empêche l'ouverture de fenêtre console
        # stderr=subprocess.STDOUT permet de ne pas crasher si ntpq écrit sur la sortie d'erreur
        output = subprocess.check_output(
            [cmd, "-pn"], 
            universal_newlines=True, 
            creationflags=0x08000000,
            stderr=subprocess.STDOUT
        )
        
        target_ip = f"127.127.20.{com_port_number}"
        
        for line in output.splitlines():
            if target_ip in line:
                parts = line.split()
                # Format standard ntpq -pn :
                # remote refid st t when poll reach delay offset jitter
                # reach est généralement la 7ème colonne (index 6)
                if len(parts) >= 7:
                    try:
                        return int(parts[6]) # On retourne la valeur telle quelle (ex: 377) sans conversion octale
                    except ValueError:
                        return 0
        return 0 # Ligne non trouvée (pas encore sync ?)
        
    except FileNotFoundError:
        return -1
    except subprocess.CalledProcessError:
        return -1
    except Exception as e:
        logger.error(f"Erreur inattendue lors de l'exécution de {cmd}: {e}")
        return -1