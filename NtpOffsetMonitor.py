import subprocess
import time
import statistics
import os
import sys
from datetime import datetime

try:
    import matplotlib.pyplot as plt
    MATPLOTLIB_AVAILABLE = True
except ImportError:
    MATPLOTLIB_AVAILABLE = False

def get_ntpq_output():
    """Exécute la commande ntpq -pn et retourne la sortie standard."""
    cmd = ['ntpq', '-pn']
    # Sous Windows, évite l'apparition d'une fenêtre console
    creationflags = 0
    if sys.platform == "win32":
        creationflags = subprocess.CREATE_NO_WINDOW

    try:
        # Essai avec la commande dans le PATH
        result = subprocess.run(cmd, capture_output=True, text=True, check=True, creationflags=creationflags)
        return result.stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        # Essai avec le chemin par défaut sous Windows si ntpq n'est pas dans le PATH
        default_path = r"C:\Program Files (x86)\NTP\bin\ntpq.exe"
        if os.path.exists(default_path):
            try:
                result = subprocess.run([default_path, '-pn'], capture_output=True, text=True, check=True, creationflags=creationflags)
                return result.stdout
            except subprocess.CalledProcessError as e:
                print(f"Erreur lors de l'exécution de ntpq : {e}")
        else:
            print("Erreur : ntpq n'a pas été trouvé dans le PATH ni dans le dossier par défaut.")
    return ""

def parse_ntpq_output(output):
    """
    Parse la sortie de ntpq et retourne les offsets de l'itération actuelle.
    Retourne un dictionnaire {'adresse': offset} pour chaque source valide.
    """
    individual_offsets = {}
    lines = output.strip().splitlines()
    
    for line in lines:
        line = line.strip()
        if not line:
            continue
            
        parts = line.split()
        if len(parts) < 9: # On a besoin d'au moins jusqu'à la colonne offset (index 8)
            continue
            
        # Extraction de l'adresse et du tally
        token_remote = parts[0]
        first_char = token_remote[0]
        
        # Liste des indicateurs d'état (tally codes) possibles dans ntpq
        tally_codes = ['*', '+', 'o', '#', '-', '.', 'x']
        
        address = token_remote
        current_tally = ' ' # Par défaut (si strip a retiré l'espace)
        
        if first_char in tally_codes:
            current_tally = first_char
            if len(token_remote) > 1:
                address = token_remote[1:]
            else:
                # Cas rare où le tally est séparé par un espace
                address = parts[1] if len(parts) > 1 else ""
        
        if not address:
            continue
            
        # Filtrage :
        # 1. On garde TOUJOURS le GPS (127.127.x.x) quel que soit son état
        # 2. Pour les autres (Web), on ne garde que ceux qui sont actifs (*, +, o)
        is_gps = address.startswith("127.127.")
        
        if not is_gps and current_tally not in ['*', '+', 'o']:
            continue

        # Filtrage sur le Reach (doit être 377 pour être considéré stable)
        if parts[6] != '377':
            continue

        # Extraction de l'offset (9ème colonne, index 8)
        try:
            offset_val = float(parts[8])
            individual_offsets[address] = offset_val
        except (ValueError, IndexError):
            continue
            
    return individual_offsets

def monitor_ntp(duration_max_sec=600, interval_sec=10):
    """Boucle principale de monitoring et affichage du graphique."""
    if not MATPLOTLIB_AVAILABLE:
        print("AVERTISSEMENT: matplotlib n'est pas installé. Le graphique ne sera pas généré.")
        print("Installez-le avec : pip install matplotlib")

    print(f"Démarrage du monitoring NTP (Durée max: {duration_max_sec}s, Intervalle: {interval_sec}s)")
    print("Appuyez sur Ctrl+C pour arrêter et afficher le graphique.")
    print("-" * 60)
    
    # Listes pour accumuler toutes les valeurs pour le calcul des médianes glissantes
    all_gps_offsets = []
    all_web_offsets = []
    
    # Dictionnaire pour stocker l'historique pour le graphique
    # Format: {'adresse_ou_median': [(timestamp, valeur), ...]}
    history = {}
    
    # Initialisation du graphique en temps réel
    fig, ax = None, None
    lines = {}
    ntpq_text_display = None
    buttons = [] # Références pour éviter le garbage collection

    if MATPLOTLIB_AVAILABLE:
        from matplotlib.widgets import Button
        plt.ion()  # Mode interactif
        fig, ax = plt.subplots(figsize=(12, 9))
        plt.subplots_adjust(bottom=0.35) # Espace pour les boutons et le texte

        ax.set_xlabel("Durée (s)")
        ax.set_ylabel("Offset (ms)")
        ax.set_title("Évolution des Offsets NTP en temps réel")
        ax.grid(True, which='both', linestyle='--', linewidth=0.5)

        # Zone de texte pour la sortie de ntpq
        ntpq_text_display = fig.text(0.05, 0.02, 'En attente de la sortie de ntpq...', 
                                     fontfamily='monospace', fontsize=8, 
                                     va='bottom', ha='left', wrap=True)

        # Fonctions de callback pour les boutons
        def start_ntp(event):
            print("\n[CMD] Démarrage du service NTP...")
            try:
                subprocess.run(["net", "start", "ntp"], check=False)
            except Exception as e:
                print(f"Erreur: {e}")

        def stop_ntp(event):
            print("\n[CMD] Arrêt du service NTP...")
            try:
                subprocess.run(["net", "stop", "ntp"], check=False)
            except Exception as e:
                print(f"Erreur: {e}")

        def restart_ntp(event):
            print("\n[CMD] Redémarrage du service NTP...")
            stop_ntp(event)
            time.sleep(1)
            start_ntp(event)

        def reset_monitor(event):
            nonlocal start_time
            print("\n[CMD] Réinitialisation du graphique et des statistiques...")
            start_time = time.time()
            all_gps_offsets.clear()
            all_web_offsets.clear()
            history.clear()
            for line in lines.values():
                line.remove()
            lines.clear()
            plt.draw()

        # Création des boutons
        btn_y_pos = 0.22
        ax_start = plt.axes([0.05, btn_y_pos, 0.2, 0.075])
        btn_start = Button(ax_start, 'Start NTP', color='lightgreen', hovercolor='0.9')
        btn_start.on_clicked(start_ntp)
        buttons.append(btn_start)

        ax_stop = plt.axes([0.28, btn_y_pos, 0.2, 0.075])
        btn_stop = Button(ax_stop, 'Stop NTP', color='salmon', hovercolor='0.9')
        btn_stop.on_clicked(stop_ntp)
        buttons.append(btn_stop)

        ax_restart = plt.axes([0.51, btn_y_pos, 0.2, 0.075])
        btn_restart = Button(ax_restart, 'Restart NTP', color='lightblue', hovercolor='0.9')
        btn_restart.on_clicked(restart_ntp)
        buttons.append(btn_restart)

        ax_reset = plt.axes([0.74, btn_y_pos, 0.2, 0.075])
        btn_reset = Button(ax_reset, 'Reset', color='gold', hovercolor='0.9')
        btn_reset.on_clicked(reset_monitor)
        buttons.append(btn_reset)

    start_time = time.time()
    
    try:
        while (time.time() - start_time) < duration_max_sec:
            loop_start_time = time.time()
            now = datetime.now()
            current_duration = time.time() - start_time
            
            # 1. Lancer la commande
            output = get_ntpq_output()

            # Afficher la sortie brute dans la zone de texte
            if MATPLOTLIB_AVAILABLE and ntpq_text_display:
                display_text = "--- Sortie ntpq -pn ---\n" + (output or "Aucune sortie reçue.")
                ntpq_text_display.set_text(display_text)
            
            if output:
                # 2. Parser et extraire les offsets de CETTE itération
                current_offsets = parse_ntpq_output(output)

                for addr, offset in current_offsets.items():
                    # Initialiser et stocker pour le graphique individuel
                    if addr not in history:
                        history[addr] = []
                    history[addr].append((current_duration, offset))

                    # Classifier pour le calcul des médianes cumulatives
                    if addr.startswith("127.127."):
                        all_gps_offsets.append(offset)
                    else:
                        all_web_offsets.append(offset)
                
                # 3. Calculer et afficher les médianes CUMULATIVES
                med_gps = statistics.median(all_gps_offsets) if all_gps_offsets else None
                med_web = statistics.median(all_web_offsets) if all_web_offsets else None
                
                # Stockage des médianes pour le graphique
                if med_gps is not None:
                    if "Médiane GPS" not in history:
                        history["Médiane GPS"] = []
                    history["Médiane GPS"].append((current_duration, med_gps))
                
                if med_web is not None:
                    if "Médiane Web" not in history:
                        history["Médiane Web"] = []
                    history["Médiane Web"].append((current_duration, med_web))

                # Affichage console
                gps_display = f"{med_gps:.3f} ms" if med_gps is not None else "N/A"
                web_display = f"{med_web:.3f} ms" if med_web is not None else "N/A"
                print(f"[{now.strftime('%H:%M:%S')}] Médiane GPS : {gps_display} | Médiane Web : {web_display}")
            
            # Mise à jour du graphique (toujours exécuté pour rafraîchir le texte et l'UI)
            if MATPLOTLIB_AVAILABLE and fig:
                for label, data in history.items():
                    if not data: continue
                    timestamps = [x[0] for x in data]
                    values = [x[1] for x in data]

                    if label not in lines:
                        # Création de la courbe
                        style = '--' if "Médiane" in label else '-'
                        marker = None if "Médiane" in label else '.'
                        lw = 2.5 if "Médiane" in label else 1.0
                        line, = ax.plot(timestamps, values, label=label, linestyle=style, marker=marker, linewidth=lw)
                        lines[label] = line
                        ax.legend(loc='upper left', fontsize='small')
                    else:
                        # Mise à jour des données
                        lines[label].set_xdata(timestamps)
                        lines[label].set_ydata(values)
                
                ax.relim()
                ax.autoscale_view()
                plt.draw()
                plt.pause(0.001)
            
            # Gestion du temps de boucle
            elapsed = time.time() - loop_start_time
            sleep_time = max(0, interval_sec - elapsed)
            
            if (time.time() - start_time + sleep_time) >= duration_max_sec:
                break # Sortir si le prochain sleep dépasse la durée max
            
            if sleep_time > 0:
                if MATPLOTLIB_AVAILABLE and fig:
                    plt.pause(sleep_time)
                else:
                    time.sleep(sleep_time)
                
    except KeyboardInterrupt:
        print("\nArrêt manuel par l'utilisateur.")
        
    finally:
        print("-" * 60)
        print("Fin de la collecte de données.")
        if MATPLOTLIB_AVAILABLE:
            plt.ioff() # Désactiver le mode interactif pour garder la fenêtre ouverte
            print("Fermez la fenêtre du graphique pour terminer.")
            plt.show()
        print("Fin du programme.")

if __name__ == "__main__":
    # Durée par défaut de 2 minutes (120s) pour un test rapide
    monitor_ntp(duration_max_sec=120, interval_sec=10)