import urllib.request
import re
import ssl
import subprocess
import os

def get_latest_meinberg_version():
    """Récupère la version la plus haute listée sur le site de Meinberg."""
    url = "https://www.meinbergglobal.com/english/sw/ntp.htm"
    context = ssl._create_unverified_context()
    try:
        headers = {'User-Agent': 'Mozilla/5.0'}
        req = urllib.request.Request(url, headers=headers)
        with urllib.request.urlopen(req, context=context) as response:
            content = response.read().decode('utf-8', errors='ignore')
            # Recherche de tous les patterns type 4.2.8p18
            pattern = r'4\.\d+\.\d+p\d+'
            all_versions = re.findall(pattern, content)
            if all_versions:
                return sorted(list(set(all_versions)))[-1]
    except Exception as e:
        print(f"Erreur lors de la récupération distante : {e}")
    return None

def get_local_ntp_version():
    """Récupère la version du service NTP installé sur la machine via ntpq."""
    try:
        # On tente d'exécuter ntpq pour avoir la version du démon
        # -c "rv 0 version" interroge le service local
        result = subprocess.run(['ntpq', '-c', 'rv 0 version'], 
                                capture_output=True, text=True, check=True)
        
        # On cherche le pattern 4.x.xpx dans la sortie
        match = re.search(r'4\.\d+\.\d+p\d+', result.stdout)
        if match:
            return match.group(0)
    except (subprocess.CalledProcessError, FileNotFoundError):
        # Si ntpq n'est pas dans le PATH, on peut tenter le chemin par défaut de Meinberg
        default_path = r"C:\Program Files (x86)\NTP\bin\ntpq.exe"
        if os.path.exists(default_path):
            try:
                result = subprocess.run([default_path, '-c', 'rv 0 version'], 
                                        capture_output=True, text=True)
                match = re.search(r'4\.\d+\.\d+p\d+', result.stdout)
                if match: return match.group(0)
            except: pass
    return None

def compare_versions():
    print("Vérification des versions NTP...")
    
    local = get_local_ntp_version()
    remote = get_latest_meinberg_version()
    
    if not local:
        print("[-] Impossible de détecter une version locale (NTP est-il installé ?)")
    else:
        print(f"[+] Version locale installée : {local}")
        
    if not remote:
        print("[-] Impossible de détecter la version sur le site de Meinberg.")
    else:
        print(f"[+] Version distante disponible : {remote}")
        
    if local and remote:
        if local == remote:
            print(">>> Votre installation est à jour.")
        else:
            # Comparaison simple (les versions NTP se suivent logiquement par chaîne)
            if remote > local:
                print(f">>> MISE À JOUR DISPONIBLE : La version {remote} est disponible !")
            else:
                print(">>> Votre version locale est plus récente ou identique à la stable.")

if __name__ == "__main__":
    compare_versions()