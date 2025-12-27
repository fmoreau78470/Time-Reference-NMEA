import serial
import pynmea2
import json
import os
import sys
import time
import ctypes
from datetime import datetime

class GPSNTPBridge:
    def __init__(self, config_path):
        self.config = self.load_config(config_path)
        self.ser = None
        
        # Création du dossier log relatif au script
        base_dir = os.path.dirname(os.path.abspath(__file__))
        self.log_dir = os.path.join(base_dir, 'log')
        if not os.path.exists(self.log_dir):
            os.makedirs(self.log_dir)
        
    def load_config(self, path):
        """Charge la configuration depuis un fichier JSON."""
        if not os.path.exists(path):
            print(f"Erreur : Le fichier de configuration {path} est introuvable.")
            sys.exit(1)
            
        with open(path, 'r') as f:
            return json.load(f)

    def is_admin(self):
        """Vérifie si le script a les droits administrateur."""
        try:
            return ctypes.windll.shell32.IsUserAnAdmin()
        except:
            return False

    def connect_serial(self):
        """Établit la connexion avec le port série du GPS."""
        try:
            self.ser = serial.Serial(
                port=self.config['serial_port'],
                baudrate=self.config['baud_rate'],
                timeout=self.config['timeout']
            )
            print(f"Connecté au GPS sur {self.config['serial_port']}")
        except serial.SerialException as e:
            print(f"Erreur de connexion série : {e}")
            sys.exit(1)

    def update_ntp_file(self, gps_time, latitude, longitude):
        """
        Modifie le fichier nécessitant des droits admin.
        Ici, on écrit le statut GPS, mais cela pourrait être une modification de ntp.conf.
        """
        if not self.config.get('create_log_file', True):
            return

        file_path = os.path.join(self.log_dir, 'gps_status.log')
        
        # Formatage des données pour le fichier
        content = (
            f"LAST_UPDATE={datetime.now().isoformat()}\n"
            f"GPS_TIME={gps_time}\n"
            f"LAT={latitude}\n"
            f"LON={longitude}\n"
            f"STATUS=LOCKED\n"
        )

        try:
            # Le mode 'w' écrase le fichier, 'a' ajoute à la fin.
            with open(file_path, 'w') as f:
                f.write(content)
            # print(f"Fichier mis à jour : {file_path}") # Décommenter pour debug verbeux
        except PermissionError:
            print(f"ERREUR CRITIQUE : Accès refusé à {file_path}. Droits admin requis.")
        except Exception as e:
            print(f"Erreur d'écriture fichier : {e}")

    def run(self):
        """Boucle principale du programme."""
        
        # 1. Vérification des droits
        if not self.is_admin():
            print("ATTENTION : Ce programme n'est pas exécuté en tant qu'administrateur.")
            print("L'écriture dans les dossiers système (Program Files) échouera.")
            
            # La seule méthode (imposée par la sécurité Windows) est que le programme détecte qu'il n'est pas administrateur,
            # puis se relance lui-même (crée une nouvelle instance) en demandant explicitement les droits.
            params = " ".join([f'"{arg}"' for arg in sys.argv])
            ctypes.windll.shell32.ShellExecuteW(
                None, "runas", sys.executable, params, None, 1
            )
            sys.exit()

        # 2. Connexion
        self.connect_serial()
        
        print("Démarrage de l'écoute NMEA...")
        
        buffer = ""
        
        try:
            while True:
                # Lecture d'une ligne NMEA
                line = self.ser.readline().decode('ascii', errors='replace').strip()
                
                # On filtre généralement sur $GPRMC ou  pour l'heure et la position
                if line.startswith('$GPRMC') or line.startswith('$GPGGA'):
                    try:
                        msg = pynmea2.parse(line)
                        
                        # Extraction des données pertinentes
                        # Note: msg.timestamp est un objet time, msg.datestamp (pour RMC) est une date
                        if hasattr(msg, 'timestamp') and msg.timestamp:
                            gps_time = msg.timestamp.strftime('%H:%M:%S')
                            lat = getattr(msg, 'latitude', 0.0)
                            lon = getattr(msg, 'longitude', 0.0)
                            
                            # Mise à jour du fichier cible
                            self.update_ntp_file(gps_time, lat, lon)
                            
                    except pynmea2.ParseError:
                        continue # Ignorer les lignes mal formées
                        
        except KeyboardInterrupt:
            print("\nArrêt du programme.")
            if self.ser:
                self.ser.close()

if __name__ == "__main__":
    # Assurez-vous que config.json est dans le même dossier
    config_file = os.path.join(os.path.dirname(__file__), 'config.json')
    bridge = GPSNTPBridge(config_file)
    bridge.run()
