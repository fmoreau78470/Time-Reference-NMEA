import tkinter as tk
from tkinter import messagebox
from tkinter import scrolledtext
from tkinter import ttk, filedialog
import webbrowser
import logging
import threading
import time
import os
import json
import re
import serial
import subprocess
from modules.service_utils import stop_service, start_service, is_service_active
from modules.ntp_utils import get_gps_reach, generate_ntp_conf_content, write_file_content, get_ntpq_command
from modules.calibration_utils import get_loopstats_path, calculate_new_fudge
from modules.iqt_utils import IQTCalculator
from datetime import datetime, timezone
import statistics
import math

# --- WIDGETS PERSONNALISÉS ---

class ToolTip:
    """Affiche une bulle d'aide au survol d'un widget."""
    def __init__(self, widget, text):
        self.widget = widget
        self.text = text
        self.tip_window = None
        self.widget.bind("<Enter>", self.show_tip)
        self.widget.bind("<Leave>", self.hide_tip)

    def show_tip(self, event=None):
        if self.tip_window or not self.text:
            return
        x = self.widget.winfo_rootx() + 20
        y = self.widget.winfo_rooty() + self.widget.winfo_height() + 5
        
        self.tip_window = tw = tk.Toplevel(self.widget)
        tw.wm_overrideredirect(True)
        tw.wm_geometry(f"+{x}+{y}")
        
        label = tk.Label(tw, text=self.text, justify=tk.LEFT,
                         background="#ffffe0", relief=tk.SOLID, borderwidth=1,
                         font=("Arial", 9, "normal"))
        label.pack(ipadx=1)

    def hide_tip(self, event=None):
        if self.tip_window:
            self.tip_window.destroy()
            self.tip_window = None

class LedBar(tk.Canvas):
    """Barre de 8 LEDs pour visualiser le Reach (Registre 8 bits)."""
    def __init__(self, master, width=200, height=30, **kwargs):
        super().__init__(master, width=width, height=height, bg=master["bg"], highlightthickness=0, **kwargs)
        self.leds = []
        self.width = width
        self.height = height
        self.setup_leds()

    def setup_leds(self):
        # Calcul dynamique de la taille
        padding = 2
        available_width = self.width - (2 * padding)
        # Diamètre max possible
        led_dia = min((available_width / 8) - 4, self.height - 4)
        gap = (available_width - (8 * led_dia)) / 7
        y_center = self.height / 2
        
        for i in range(8):
            x = padding + i * (led_dia + gap)
            # Création du cercle (LED éteinte = rouge foncé)
            led = self.create_oval(x, y_center - led_dia/2, x + led_dia, y_center + led_dia/2, 
                                   fill="#440000", outline="#666666")
            self.leds.append(led)

    def set_value(self, reach_int):
        """Met à jour les LEDs selon la valeur du reach (ex: 377 octal -> 11111111)."""
        try:
            # Le reach arrive sous forme d'entier "visuel" (ex: 377 pour octal 377)
            # Il faut le convertir en chaîne puis l'interpréter en base 8 pour obtenir les bits corrects
            val = int(str(reach_int), 8)
        except:
            val = 0
        
        # Conversion en binaire sur 8 bits (ex: '11111111')
        binary_str = format(val, '08b')[-8:]
        
        # Inversion des bits pour que le remplissage (LSB/Nouveau) se fasse de la Gauche vers la Droite
        binary_str = binary_str[::-1]
        
        for i in range(8):
            # Bit 0 (MSB à gauche) -> LED 0
            bit = binary_str[i]
            # Vert vif si 1, Rouge foncé si 0
            color = "#00FF00" if bit == '1' else "#440000"
            self.itemconfig(self.leds[i], fill=color)

class CircularTimer(tk.Canvas):
    """Compte à rebours circulaire."""
    def __init__(self, master, size=100, **kwargs):
        super().__init__(master, width=size, height=size, bg=master["bg"], highlightthickness=0, **kwargs)
        self.size = size
        center = size / 2
        radius = (size / 2) - 8
        
        # Fond gris
        self.create_oval(center-radius, center-radius, center+radius, center+radius, outline="#e0e0e0", width=8)
        # Arc de progression (Vert)
        self.arc = self.create_arc(center-radius, center-radius, center+radius, center+radius, 
                                   start=90, extent=360, outline="#4caf50", width=8, style="arc")
        # Texte central
        self.text_id = self.create_text(center, center, text="00:00", font=("Arial", 14, "bold"), fill="#333333")

    def update_timer(self, remaining_sec, total_sec):
        extent = (remaining_sec / total_sec) * 360 if total_sec > 0 else 0
        self.itemconfig(self.arc, extent=extent)
        self.itemconfig(self.text_id, text=f"{remaining_sec // 60:02}:{remaining_sec % 60:02}")

class IQTGauge(tk.Canvas):
    """Jauge rotative style vintage pour l'Indice de Qualité Temporelle (0-100)."""
    def __init__(self, master, size=200, **kwargs):
        # Fond du composant : Noir (Boîtier)
        super().__init__(master, width=size, height=size, bg="#1a1a1a", highlightthickness=2, highlightbackground="#000000", **kwargs)
        self.size = size
        self.center = size / 2
        
        # Zone "Ecran" (Jaune/Beige)
        pad = 4
        self.create_rectangle(pad, pad, size-pad, size-pad, fill="#fdf5e6", outline="#333333")
        
        self.radius = (size / 2) - 15
        self.setup_gauge()

    def setup_gauge(self):
        c = self.center
        r = self.radius
        
        # Facteur d'échelle basé sur la taille par défaut (200px)
        scale = self.size / 200.0
        w_band = max(5, 12 * scale)
        
        # Zones de couleur (Fond statique)
        # Rouge (0-50) -> 50% de 270° = 135°
        self.create_arc(c-r, c-r, c+r, c+r, start=225, extent=-135, outline="#d32f2f", width=w_band, style="arc")
        # Orange (50-80) -> 30% de 270° = 81°
        self.create_arc(c-r, c-r, c+r, c+r, start=90, extent=-81, outline="#fb8c00", width=w_band, style="arc")
        # Vert (80-100) -> 20% de 270° = 54°
        self.create_arc(c-r, c-r, c+r, c+r, start=9, extent=-54, outline="#43a047", width=w_band, style="arc")

        # Aiguille (Ligne)
        width_needle = max(2, 4 * scale)
        self.needle_len = r - 5
        self.needle = self.create_line(c, c, c, c-self.needle_len, width=width_needle, fill="#cc0000", capstyle=tk.ROUND)
        
        # Pivot
        self.create_oval(c-5*scale, c-5*scale, c+5*scale, c+5*scale, fill="#333333")
        
        # Texte Valeur
        self.text_val = self.create_text(c, c + r/2, text="--", font=("Consolas", int(24*scale), "bold"), fill="#333333")
        self.text_lbl = self.create_text(c, c + r/2 + 20*scale, text="IQT", font=("Times New Roman", int(10*scale), "bold"), fill="#444444")

    def set_value(self, value):
        """Met à jour l'aiguille et la couleur du texte."""
        import math
        val = max(0, min(100, value))
        
        # Mapping 0-100 vers angle 225 -> -45 (Total 270 degrés)
        angle_deg = 225 - (val * 2.7)
        angle_rad = math.radians(angle_deg)
        
        x = self.center + self.needle_len * math.cos(angle_rad)
        y = self.center - self.needle_len * math.sin(angle_rad) # Y inversé en canvas
        
        self.coords(self.needle, self.center, self.center, x, y)
        self.itemconfig(self.text_val, text=f"{int(val)}")

class OffsetVuMeter(tk.Canvas):
    """Vu-mètre style vintage (inspiré Audiofanzine) pour visualiser l'offset."""
    def __init__(self, master, width=400, height=150, **kwargs):
        # Fond du composant : Noir (Boîtier)
        super().__init__(master, width=width, height=height, bg="#1a1a1a", highlightthickness=2, highlightbackground="#000000", **kwargs)
        self.width = width
        self.height = height
        
        # Zone "Ecran" (Jaune/Beige)
        pad = 4
        self.create_rectangle(pad, pad, width-pad, height-pad, fill="#fdf5e6", outline="#333333") # OldLace color
        
        self.cx = width / 2
        
        # Géométrie agrandie pour remplir le cadre
        # Rayon calculé pour que l'arc (135°-45°) remplisse la largeur moins une marge
        margin = 40
        self.radius = (width - margin) / 1.414
        # Pivot positionné pour que le haut de l'arc soit à y=25
        self.cy = self.radius + 25
        
        # Plage angulaire (Gauche 135° -> Droite 45°)
        self.angle_min = 135
        self.angle_max = 45
        self.val_min = -10
        self.val_max = 10
        
        self.setup_dial()
        
        # Pivot visuel
        self.create_oval(self.cx-3, self.cy-3, self.cx+3, self.cy+3, fill="#333333")
        
        # Aiguille
        self.needle = self.create_line(self.cx, self.cy, self.cx, self.cy - self.radius, width=2, fill="#cc0000", capstyle=tk.ROUND)
        
        # Affichage numérique
        self.text_val = self.create_text(15, 15, text="0.000 ms", font=("Consolas", 10), fill="#333333", anchor="w")
        
        self.set_value(0.0)

    def value_to_angle(self, val):
        """Convertit une valeur en angle (Logarithmique symétrique)."""
        # On utilise log10(1 + |x|) pour dilater le centre (0) et compresser les extrêmes
        val_clamped = max(-10.0, min(10.0, val))
        sign = 1 if val_clamped >= 0 else -1
        v = abs(val_clamped)
        
        # log10(1+10) = 1.041...
        max_log = math.log10(1 + 10.0)
        curr_log = math.log10(1 + v)
        ratio = curr_log / max_log
        
        # 90° est le centre. 45° par côté.
        return 90 - (sign * ratio * 45)

    def setup_dial(self):
        r = self.radius - 10
        bbox = (self.cx-r, self.cy-r, self.cx+r, self.cy+r)
        
        # Zones de couleur (Largeur de bande)
        w_band = 8
        
        def get_angle(val):
            return self.value_to_angle(val)

        # Dessin des arcs
        self.create_arc(bbox, start=get_angle(-10), extent=get_angle(-5)-get_angle(-10), outline="#d32f2f", width=w_band, style="arc")
        self.create_arc(bbox, start=get_angle(-5), extent=get_angle(-2)-get_angle(-5), outline="#fb8c00", width=w_band, style="arc")
        self.create_arc(bbox, start=get_angle(-2), extent=get_angle(2)-get_angle(-2), outline="#43a047", width=w_band, style="arc")
        self.create_arc(bbox, start=get_angle(2), extent=get_angle(5)-get_angle(2), outline="#fb8c00", width=w_band, style="arc")
        self.create_arc(bbox, start=get_angle(5), extent=get_angle(10)-get_angle(5), outline="#d32f2f", width=w_band, style="arc")
        
        # Ticks
        for val in [-10, -5, -2, 0, 2, 5, 10]:
            angle_deg = get_angle(val)
            angle_rad = math.radians(angle_deg)
            
            r_out = r + w_band/2
            r_in = r - w_band/2 - 5
            
            x_out = self.cx + r_out * math.cos(angle_rad)
            y_out = self.cy - r_out * math.sin(angle_rad)
            x_in = self.cx + r_in * math.cos(angle_rad)
            y_in = self.cy - r_in * math.sin(angle_rad)
            
            self.create_line(x_in, y_in, x_out, y_out, fill="#222222", width=1)
            
            # Texte
            r_txt = r_in - 15
            x_t = self.cx + r_txt * math.cos(angle_rad)
            y_t = self.cy - r_txt * math.sin(angle_rad)
            self.create_text(x_t, y_t, text=str(val), font=("Arial", 9, "bold"), fill="#444444")

    def set_value(self, val_ms):
        # Update needles
        self.update_needle(self.needle, val_ms)
        
        # Mise à jour texte
        self.itemconfig(self.text_val, text=f"{val_ms:.3f} ms")

    def update_needle(self, item, val):
        angle_rad = math.radians(self.value_to_angle(val))
        
        r_needle = self.radius - 15
        x = self.cx + r_needle * math.cos(angle_rad)
        y = self.cy - r_needle * math.sin(angle_rad)
        
        self.coords(item, self.cx, self.cy, x, y)

class JitterVuMeter(tk.Canvas):
    """Vu-mètre style vintage pour visualiser le Jitter (0-20ms)."""
    def __init__(self, master, width=400, height=150, **kwargs):
        # Fond du composant : Noir (Boîtier)
        super().__init__(master, width=width, height=height, bg="#1a1a1a", highlightthickness=2, highlightbackground="#000000", **kwargs)
        self.width = width
        self.height = height
        
        # Zone "Ecran" (Jaune/Beige)
        pad = 4
        self.create_rectangle(pad, pad, width-pad, height-pad, fill="#fdf5e6", outline="#333333")
        
        self.cx = width / 2
        
        # Géométrie agrandie
        margin = 40
        self.radius = (width - margin) / 1.414
        self.cy = self.radius + 25
        
        # Plage angulaire (Gauche 135° -> Droite 45°)
        self.angle_min = 135
        self.angle_max = 45
        self.val_min = 0
        self.val_max = 20 # 20+ est critique
        
        self.setup_dial()
        
        # Pivot visuel
        self.create_oval(self.cx-3, self.cy-3, self.cx+3, self.cy+3, fill="#333333")
        
        # Aiguille
        self.needle = self.create_line(self.cx, self.cy, self.cx, self.cy - self.radius, width=2, fill="#cc0000", capstyle=tk.ROUND)
        
        # Affichage numérique
        self.text_val = self.create_text(15, 15, text="0.000 ms", font=("Consolas", 10), fill="#333333", anchor="w")
        
        self.set_value(0.0)

    def value_to_angle(self, val):
        """Convertit une valeur en angle (Logarithmique)."""
        # Echelle log de 0.01 à 20
        v = max(0.01, min(20.0, val))
        
        log_min = math.log10(0.01) # -2
        log_max = math.log10(20.0) # 1.301
        
        log_val = math.log10(v)
        ratio = (log_val - log_min) / (log_max - log_min)
        
        # 135 -> 45
        return 135 + ratio * (45 - 135)

    def setup_dial(self):
        r = self.radius - 10
        bbox = (self.cx-r, self.cy-r, self.cx+r, self.cy+r)
        w_band = 8
        
        def get_angle(val):
            return self.value_to_angle(val)

        # Dessin des arcs (Seuils: 1ms, 5ms)
        # 0 - 1 : Vert (Elite/Très Bon)
        self.create_arc(bbox, start=get_angle(0), extent=get_angle(1)-get_angle(0), outline="#43a047", width=w_band, style="arc")
        # 1 - 5 : Orange (Moyen)
        self.create_arc(bbox, start=get_angle(1), extent=get_angle(5)-get_angle(1), outline="#fb8c00", width=w_band, style="arc")
        # 5 - 20 : Rouge (Critique)
        self.create_arc(bbox, start=get_angle(5), extent=get_angle(20)-get_angle(5), outline="#d32f2f", width=w_band, style="arc")
        
        # Ticks
        for val in [0, 0.05, 1, 5, 10, 20]:
            angle_deg = get_angle(val)
            angle_rad = math.radians(angle_deg)
            
            r_out = r + w_band/2
            r_in = r - w_band/2 - 5
            
            x_out = self.cx + r_out * math.cos(angle_rad)
            y_out = self.cy - r_out * math.sin(angle_rad)
            x_in = self.cx + r_in * math.cos(angle_rad)
            y_in = self.cy - r_in * math.sin(angle_rad)
            
            self.create_line(x_in, y_in, x_out, y_out, fill="#222222", width=1)
            
            # Texte
            r_txt = r_in - 15
            x_t = self.cx + r_txt * math.cos(angle_rad)
            y_t = self.cy - r_txt * math.sin(angle_rad)
            self.create_text(x_t, y_t, text=str(val), font=("Arial", 9, "bold"), fill="#444444")

    def set_value(self, val_ms):
        self.update_needle(self.needle, val_ms)
        
        self.itemconfig(self.text_val, text=f"{val_ms:.3f} ms")
        
        # Couleur texte selon qualité
        if val_ms <= 0.05: color = "#006400" # DarkGreen (Elite)
        elif val_ms <= 1.0: color = "#2e7d32" # Green (Très Bon)
        elif val_ms <= 5.0: color = "#ef6c00" # Orange (Moyen)
        else: color = "#c62828" # Red (Critique)
        self.itemconfig(self.text_val, fill=color)

    def update_needle(self, item, val):
        angle_rad = math.radians(self.value_to_angle(val))
        
        r_needle = self.radius - 15
        x = self.cx + r_needle * math.cos(angle_rad)
        y = self.cy - r_needle * math.sin(angle_rad)
        
        self.coords(item, self.cx, self.cy, x, y)

class TimeStatusFrame(tk.LabelFrame):
    """Frame affichant l'heure PC, l'heure GPS et les Vu-mètres."""
    def __init__(self, parent, app):
        super().__init__(parent, text="Horloges & Qualité", padx=5, pady=5)
        self.app = app
        self.utc_var = tk.BooleanVar(value=self.app.config.get("utc_mode", False))
        
        # Conteneur horizontal
        frame_container = tk.Frame(self)
        frame_container.pack(fill=tk.X)
        
        # --- Zone Gauche : Horloges ---
        frame_clocks = tk.Frame(frame_container)
        frame_clocks.pack(side=tk.LEFT, padx=10)
        
        tk.Label(frame_clocks, text="PC :", font=("Arial", 9)).grid(row=0, column=0, sticky="e")
        self.lbl_pc = tk.Label(frame_clocks, text="--:--:--", font=("Consolas", 12, "bold"))
        self.lbl_pc.grid(row=0, column=1, sticky="w", padx=5)
        
        tk.Label(frame_clocks, text="GPS :", font=("Arial", 9)).grid(row=1, column=0, sticky="e")
        self.lbl_gps = tk.Label(frame_clocks, text="--:--:--", font=("Consolas", 12, "bold"), fg="#0066cc")
        self.lbl_gps.grid(row=1, column=1, sticky="w", padx=5)
        
        self.chk_utc = tk.Checkbutton(frame_clocks, text="Mode UTC", variable=self.utc_var, command=self.on_toggle)
        self.chk_utc.grid(row=2, column=0, columnspan=2, pady=(5,0))

        # --- Zone Droite : Vu-mètres ---
        frame_offset = tk.Frame(frame_container)
        frame_offset.pack(side=tk.LEFT, padx=5, expand=True)
        tk.Label(frame_offset, text="Offset", font=("Arial", 8, "bold")).pack()
        self.vu_offset = OffsetVuMeter(frame_offset, width=220, height=110)
        self.vu_offset.pack()

        frame_jitter = tk.Frame(frame_container)
        frame_jitter.pack(side=tk.LEFT, padx=5, expand=True)
        tk.Label(frame_jitter, text="Jitter", font=("Arial", 8, "bold")).pack()
        self.vu_jitter = JitterVuMeter(frame_jitter, width=220, height=110)
        self.vu_jitter.pack()
        
        self.stop_event = threading.Event()
        
        # Démarrage des boucles
        threading.Thread(target=self.fetch_gps_loop, daemon=True).start()

    def on_toggle(self):
        self.app.config["utc_mode"] = self.utc_var.get()
        if self.app.config_path:
            try:
                with open(self.app.config_path, 'w') as f:
                    json.dump(self.app.config, f, indent=4)
            except: pass

    def update_clocks(self, pc_now_utc, gps_str, offset_val, jitter_val):
        """Met à jour l'affichage des deux horloges simultanément."""
        if not self.winfo_exists(): return
        
        is_utc = self.utc_var.get()
        
        # Heure PC
        if is_utc:
            t_pc = pc_now_utc
        else:
            t_pc = pc_now_utc.astimezone() # Local
            
        self.lbl_pc.config(text=t_pc.strftime("%H:%M:%S"))
        
        # Heure GPS (Conversion si nécessaire)
        if gps_str != "--:--:--" and gps_str != "NTP OFF":
            try:
                if is_utc:
                    self.lbl_gps.config(text=gps_str)
                else:
                    # Conversion UTC string -> Local datetime
                    t_utc = datetime.strptime(gps_str, "%H:%M:%S").replace(tzinfo=timezone.utc)
                    # On combine avec la date du jour pour la conversion timezone correcte
                    t_utc = t_utc.replace(year=pc_now_utc.year, month=pc_now_utc.month, day=pc_now_utc.day)
                    t_local = t_utc.astimezone()
                    self.lbl_gps.config(text=t_local.strftime("%H:%M:%S"))
            except:
                self.lbl_gps.config(text=gps_str)
        else:
            self.lbl_gps.config(text=gps_str)

        # Update meters
        try:
            if offset_val is not None:
                self.vu_offset.set_value(offset_val)
            if jitter_val is not None:
                self.vu_jitter.set_value(jitter_val)
        except: pass

    def fetch_gps_loop(self):
        """Récupère l'heure GPS via ntpq clockvar pour ne pas bloquer le port série."""
        while not self.stop_event.is_set():
            if not self.winfo_exists(): break
            
            gps_time_str = "--:--:--"
            offset_val = None
            jitter_val = None
            
            if is_service_active("NTP"):
                try:
                    startupinfo = subprocess.STARTUPINFO()
                    startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                    
                    cmd = get_ntpq_command()
                    # On demande les peers (pour offset/jitter GPS) ET clockvar (pour timecode)
                    proc = subprocess.run([cmd, "-n", "-c", "peers", "-c", "clockvar"], capture_output=True, text=True, startupinfo=startupinfo, timeout=2)
                    
                    output = proc.stdout
                    
                    # 1. Parsing Peers pour Offset/Jitter
                    for line in output.splitlines():
                        if "127.127.20" in line:
                            parts = line.split()
                            # remote refid st t when poll reach delay offset jitter
                            if len(parts) >= 10:
                                try:
                                    offset_val = float(parts[8])
                                    jitter_val = float(parts[9])
                                except ValueError: pass
                            break

                    # 2. Parsing Clockvar pour Timecode
                    match = re.search(r'timecode="([^"]*)"', output)
                    if match:
                        # Format attendu: $GPRMC,HHMMSS,...
                        parts = match.group(1).split(',')
                        if len(parts) > 1 and len(parts[1]) >= 6:
                            raw = parts[1]
                            gps_time_str = f"{raw[0:2]}:{raw[2:4]}:{raw[4:6]}"

                except:
                    pass
            else:
                gps_time_str = "NTP OFF"
            
            # Capture de l'heure PC juste après la récupération GPS pour synchronisation visuelle
            pc_now = datetime.now(timezone.utc)
            
            # Mise à jour UI
            self.after(0, self.update_clocks, pc_now, gps_time_str, offset_val, jitter_val)
            
            time.sleep(1)

class CalibrationWizard(tk.Toplevel):
    """Assistant de calibration pas-à-pas (Machine à états)."""
    def __init__(self, parent, app):
        super().__init__(parent)
        self.app = app
        self.title("Assistant de Calibration Expert")
        self.geometry("600x650")
        self.protocol("WM_DELETE_WINDOW", self.on_close)
        
        self.steps = [
            "1. Initialisation (Mode Observation)",
            "2. Analyse de Santé (Jitter/Reach)",
            "3. Mesure & Calcul de l'écart",
            "4. Application & Convergence",
            "5. Validation Finale"
        ]
        self.current_step_idx = 0
        self.stop_event = threading.Event()
        self.pause_event = threading.Event()
        self.original_options = self.app.config.get("server_options", "iburst")
        self.config_modified = False
        
        self.setup_ui()
        
    def setup_ui(self):
        tk.Label(self, text="Assistant de Calibration NTP", font=("Arial", 14, "bold")).pack(pady=10)
        
        # Liste des étapes
        self.step_labels = []
        frame_steps = tk.Frame(self, relief="groove", borderwidth=1)
        frame_steps.pack(fill=tk.X, padx=20, pady=10)
        
        for i, step_name in enumerate(self.steps):
            lbl = tk.Label(frame_steps, text=step_name, fg="#888888", font=("Arial", 10))
            lbl.pack(anchor="w", padx=5, pady=2)
            self.step_labels.append(lbl)
            
        # Zone de Log
        self.txt_log = scrolledtext.ScrolledText(self, height=15, state=tk.DISABLED, bg="#f0f0f0", font=("Consolas", 9))
        self.txt_log.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)
        
        # Contrôles
        frame_btns = tk.Frame(self)
        frame_btns.pack(pady=10)
        
        self.btn_start = tk.Button(frame_btns, text="Démarrer l'Assistant", command=self.start_wizard, bg="#ddffdd", font=("Arial", 10, "bold"))
        self.btn_start.pack(side=tk.LEFT, padx=5)
        
        self.btn_pause = tk.Button(frame_btns, text="Pause", command=self.toggle_pause, state=tk.DISABLED, bg="#fff9c4")
        self.btn_pause.pack(side=tk.LEFT, padx=5)
        
        self.btn_stop = tk.Button(frame_btns, text="Arrêter", command=self.stop_wizard, state=tk.DISABLED, bg="#ffcccc")
        self.btn_stop.pack(side=tk.LEFT, padx=5)

    def log(self, msg, color="black"):
        # Log système (Fichier) avec indication de la fenêtre
        prefix = "[Assistant Calibration] "
        if color == "red":
            self.app.logger.error(f"{prefix}{msg}")
        elif color == "orange":
            self.app.logger.warning(f"{prefix}{msg}")
        else:
            self.app.logger.info(f"{prefix}{msg}")

        if not self.winfo_exists(): return
        self.txt_log.config(state=tk.NORMAL)
        ts = datetime.now().strftime("%H:%M:%S")
        self.txt_log.insert(tk.END, f"[{ts}] {msg}\n")
        self.txt_log.see(tk.END)
        self.txt_log.config(state=tk.DISABLED)

    def highlight_step(self, index):
        for i, lbl in enumerate(self.step_labels):
            if i < index:
                lbl.config(fg="green", font=("Arial", 10))
            elif i == index:
                lbl.config(fg="blue", font=("Arial", 10, "bold"))
            else:
                lbl.config(fg="#888888", font=("Arial", 10))

    def toggle_pause(self):
        if self.pause_event.is_set():
            self.pause_event.clear()
            self.btn_pause.config(text="Pause", bg="#fff9c4")
            self.log("Reprise de l'assistant...")
        else:
            self.pause_event.set()
            self.btn_pause.config(text="Reprendre", bg="#ffcc80")
            self.log("Assistant en PAUSE...", "orange")

    def start_wizard(self):
        self.stop_event.clear()
        self.pause_event.clear()
        self.btn_start.config(state=tk.DISABLED)
        self.btn_pause.config(state=tk.NORMAL, text="Pause", bg="#fff9c4")
        self.btn_stop.config(state=tk.NORMAL)
        threading.Thread(target=self.run_process, daemon=True).start()

    def stop_wizard(self):
        self.stop_event.set()
        self.log("Arrêt demandé par l'utilisateur...", "red")

    def wait_if_paused(self):
        """Bloque l'exécution si la pause est active."""
        while self.pause_event.is_set():
            if self.stop_event.is_set(): return True
            time.sleep(0.5)
        return False

    def on_close(self):
        if self.btn_start['state'] == tk.DISABLED:
            if not messagebox.askyesno("Attention", "Calibration en cours. Voulez-vous vraiment quitter ?"):
                return
        self.stop_event.set()
        
        if self.config_modified:
            self.restore_original_config()
            
        self.app.logger.info("[Assistant Calibration] Fermeture de la fenêtre.")
        self.destroy()

    def restore_original_config(self):
        """Restaure la configuration NTP initiale."""
        self.log("Restauration de la configuration initiale...")
        try:
            new_conf = generate_ntp_conf_content(self.app.config)
            target_path = self.app.config.get("config_ntp")
            stop_service("NTP")
            write_file_content(target_path, new_conf)
            start_service("NTP")
            self.log("Configuration restaurée.")
            self.config_modified = False
        except Exception as e:
            self.log(f"Erreur restauration: {e}", "red")

    def run_process(self):
        try:
            # --- ÉTAPE 1 : INIT ---
            self.highlight_step(0)
            self.log("=== Étape 1 : Initialisation ===")
            
            # Passage en mode observation (noselect) pour les serveurs internet
            self.log("Configuration NTP en mode 'Observation'...")
            temp_config = self.app.config.copy()
            temp_config["server_options"] = "iburst noselect"
            
            new_conf = generate_ntp_conf_content(temp_config)
            target_path = self.app.config.get("config_ntp")
            
            stop_service("NTP")
            write_file_content(target_path, new_conf)
            start_service("NTP")
            self.config_modified = True
            
            self.log("Service redémarré. Stabilisation (15s)...")
            for i in range(15):
                if self.stop_event.is_set(): return
                if self.wait_if_paused(): return
                time.sleep(1)

            # --- ÉTAPE 2 : SANTÉ ---
            self.highlight_step(1)
            self.log("=== Étape 2 : Analyse de Santé ===")
            self.log("Attente de la synchronisation complète (Reach=377)...")
            
            gps_ok = False
            for i in range(40): # Essai pendant 200s max (Reach 377 prend ~130s)
                if self.stop_event.is_set(): return
                if self.wait_if_paused(): return
                
                # Lecture ntpq -p
                proc = subprocess.run(["ntpq", "-pn"], capture_output=True, text=True)
                found = False
                for line in proc.stdout.splitlines():
                    if "127.127.20" in line:
                        found = True
                        parts = line.split()
                        if len(parts) >= 10:
                            reach = int(parts[6], 8) # Octal
                            jitter = float(parts[9])
                            self.log(f"Essai {i+1}/40 : Reach={reach:o}, Jitter={jitter}ms")
                            
                            if reach == 0o377 and jitter < 100:
                                gps_ok = True
                                break
                if not found:
                    self.log(f"Essai {i+1}/40 : Source GPS absente...")

                if gps_ok: break
                time.sleep(5)
            
            if not gps_ok:
                self.log("ERREUR: Signal GPS instable ou absent.", "red")
                return

            # --- ÉTAPE 3 : MESURE ---
            self.highlight_step(2)
            self.log("=== Étape 3 : Mesure (2 min) ===")
            gps_offsets = []
            net_offsets = []
            
            for i in range(24): # 24 * 5s = 120s = 2 min
                if self.stop_event.is_set(): return
                if self.wait_if_paused(): return
                proc = subprocess.run([get_ntpq_command(), "-pn"], capture_output=True, text=True)
                
                iter_gps = []
                iter_net = []
                
                for line in proc.stdout.splitlines():
                    # Ignorer les en-têtes et lignes vides
                    if not line or line.startswith("remote") or line.startswith("="): continue
                    
                    parts = line.split()
                    if len(parts) >= 9:
                        try:
                            off = float(parts[8]) # Offset en ms
                            remote = parts[0]
                            # Nettoyage du caractère d'état (*, +, space, etc.)
                            if not remote[0].isalnum(): remote = remote[1:]
                            
                            if "127.127.20" in remote:
                                gps_offsets.append(off)
                                iter_gps.append(off)
                            elif "127.127" not in remote:
                                # On considère que tout ce qui n'est pas 127.127.x.x est une source Internet
                                # Spec: reach <> 0 pour prendre en compte
                                if int(parts[6], 8) > 0:
                                    net_offsets.append(off)
                                    iter_net.append(off)
                        except ValueError: pass
                        
                # Affichage des valeurs de cette itération
                g_txt = f"{iter_gps[0]:.3f}" if iter_gps else "Abs"
                n_txt = f"{statistics.median(iter_net):.3f}" if iter_net else "Abs"
                self.log(f"Mesure {i+1}/24 : GPS={g_txt} ms | Net(med)={n_txt} ms")
                time.sleep(5)
            
            if not gps_offsets:
                self.log("Pas de données GPS.", "red"); return
                
            median_gps = statistics.median(gps_offsets)
            self.log(f"Offset GPS Médian : {median_gps:.3f} ms")
            
            median_net = 0.0
            if net_offsets:
                median_net = statistics.median(net_offsets)
                self.log(f"Offset Internet Médian : {median_net:.3f} ms")
            else:
                self.log("Aucune source Internet pour comparaison (Référence = 0)", "orange")
            
            # Calcul de l'écart (Ce qu'il faut ajouter au GPS pour qu'il rejoigne Internet)
            # Delta = Net - GPS
            delta_ms = median_net - median_gps
            self.log(f"Écart constaté (Net - GPS) : {delta_ms:.3f} ms")

            # --- ÉTAPE 4 : APPLICATION ---
            self.highlight_step(3)
            self.log("=== Étape 4 : Application ===")
            
            current_fudge = float(self.app.config.get("time2_value", "0.0"))
            
            # Formule : Nouveau = Actuel + Delta
            new_fudge = current_fudge + (delta_ms / 1000.0)
            
            self.log(f"Ancien Fudge: {current_fudge:.4f} s")
            self.log(f"Nouveau Fudge: {new_fudge:.4f} s")
            
            # Restauration options originales + Nouveau Fudge
            self.app.config["time2_value"] = f"{new_fudge:.4f}"
            # On garde les options originales (ex: iburst tout court si mode maison)
            # Mais attention, si on est en mode terrain, on veut peut-être garder noselect ?
            # Pour l'instant on restaure ce qu'il y avait avant le wizard.
            
            # Sauvegarde et Redémarrage final géré par l'App principale pour cohérence
            # On utilise une méthode thread-safe via after
            self.after(0, lambda: self.finish_wizard(new_fudge, delta_ms, current_fudge))

        except Exception as e:
            self.log(f"Erreur critique: {e}", "red")
        finally:
            self.btn_start.config(state=tk.NORMAL)
            self.btn_pause.config(state=tk.DISABLED)
            self.btn_stop.config(state=tk.DISABLED)

    def finish_wizard(self, new_fudge, delta_ms, current_fudge):
        self.highlight_step(4)
        current_fudge_ms = current_fudge * 1000.0
        new_fudge_ms = new_fudge * 1000.0
        
        # Création de la boîte de dialogue personnalisée
        dialog = tk.Toplevel(self)
        dialog.title("Validation Calibration")
        dialog.geometry("600x350")
        dialog.transient(self)
        dialog.grab_set()
        
        # Centrage
        self.update_idletasks()
        x = self.winfo_rootx() + (self.winfo_width() - 600) // 2
        y = self.winfo_rooty() + (self.winfo_height() - 350) // 2
        dialog.geometry(f"+{x}+{y}")

        tk.Label(dialog, text="Résultat de la Calibration", font=("Arial", 14, "bold")).pack(pady=15)
        
        msg = (f"Calibration terminée avec succès !\n\n"
               f"Écart constaté (Net - GPS) : {delta_ms:.3f} ms\n"
               f"Ancienne compensation : {current_fudge_ms:.3f} ms\n"
               f"Nouvelle Compensation du retard : {new_fudge_ms:.3f} ms\n\n"
               f"Que souhaitez-vous faire ?")
        
        tk.Label(dialog, text=msg, justify=tk.LEFT, font=("Arial", 10)).pack(pady=10, padx=20)
        
        self.user_choice = "cancel"

        def on_fine_tune():
            self.user_choice = "fine_tune"
            dialog.destroy()

        def on_apply():
            self.user_choice = "apply"
            dialog.destroy()

        def on_cancel():
            self.user_choice = "cancel"
            dialog.destroy()

        btn_frame = tk.Frame(dialog)
        btn_frame.pack(pady=20, fill=tk.X, padx=20)
        btn_frame.columnconfigure(0, weight=1)
        btn_frame.columnconfigure(1, weight=1)
        btn_frame.columnconfigure(2, weight=1)

        tk.Button(btn_frame, text="Lancer l'affinage\n(Recommandé)", command=on_fine_tune, bg="#e1f5fe", height=2).grid(row=0, column=0, padx=5, sticky="ew")
        tk.Button(btn_frame, text="Appliquer\nla valeur", command=on_apply, bg="#ddffdd", height=2).grid(row=0, column=1, padx=5, sticky="ew")
        tk.Button(btn_frame, text="Sortir\nsans rien faire", command=on_cancel, bg="#ffcccc", height=2).grid(row=0, column=2, padx=5, sticky="ew")

        dialog.protocol("WM_DELETE_WINDOW", on_cancel)
        self.wait_window(dialog)

        if self.user_choice in ["apply", "fine_tune"]:
            # Mise à jour config persistante
            if self.app.config_path:
                with open(self.app.config_path, 'w') as f:
                    json.dump(self.app.config, f, indent=4)
            
            # Régénération ntp.conf avec options originales
            new_conf = generate_ntp_conf_content(self.app.config)
            target_path = self.app.config.get("config_ntp")
            
            stop_service("NTP")
            write_file_content(target_path, new_conf)
            start_service("NTP")
            self.config_modified = False
            
            self.log("Configuration sauvegardée et service redémarré.")
            
            if self.user_choice == "fine_tune":
                self.setup_fine_tuning_ui()
            else:
                messagebox.showinfo("Succès", "Le système est calibré.", parent=self)
                self.destroy()
        else:
            self.log("Annulé par l'utilisateur. Retour à la configuration précédente.")
            # Restauration de la valeur en mémoire avant de régénérer le fichier
            self.app.config["time2_value"] = f"{current_fudge:.4f}"
            self.restore_original_config()
            self.destroy()

    def setup_fine_tuning_ui(self):
        """Configure l'interface pour la phase d'affinage précis."""
        # Nettoyage de l'interface existante
        for widget in self.winfo_children():
            widget.destroy()
            
        self.title("Assistant de Calibration - Affinage Précis")
        
        tk.Label(self, text="Affinage Précis (Loopstats)", font=("Arial", 14, "bold")).pack(pady=10)
        
        # Choix durée
        frame_duration = tk.LabelFrame(self, text="Durée d'analyse", padx=10, pady=5)
        frame_duration.pack(fill=tk.X, padx=20, pady=5)
        
        self.duration_var = tk.IntVar(value=5)
        tk.Radiobutton(frame_duration, text="Standard (5 min)", variable=self.duration_var, value=5).pack(side=tk.LEFT, padx=10)
        tk.Radiobutton(frame_duration, text="Ultra Précis (20 min)", variable=self.duration_var, value=20).pack(side=tk.LEFT, padx=10)
        
        # Visualisations
        frame_viz = tk.Frame(self)
        frame_viz.pack(fill=tk.X, padx=20, pady=10)
        
        # Reach
        frame_reach = tk.Frame(frame_viz)
        frame_reach.pack(side=tk.LEFT, expand=True)
        tk.Label(frame_reach, text="Qualité Sync (Reach)", font=("Arial", 9)).pack()
        self.led_bar = LedBar(frame_reach, width=180, height=30)
        self.led_bar.pack(pady=5)
        
        # Timer
        frame_timer = tk.Frame(frame_viz)
        frame_timer.pack(side=tk.RIGHT, expand=True)
        tk.Label(frame_timer, text="Temps Restant", font=("Arial", 9)).pack()
        self.timer_display = CircularTimer(frame_timer, size=80)
        self.timer_display.pack(pady=5)
        
        # Log area (recréé)
        self.txt_log = scrolledtext.ScrolledText(self, height=12, state=tk.DISABLED, bg="#f0f0f0", font=("Consolas", 9))
        self.txt_log.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)
        
        # Boutons
        frame_btns = tk.Frame(self)
        frame_btns.pack(pady=10)
        
        self.btn_start_fine = tk.Button(frame_btns, text="Démarrer l'Affinage", command=self.start_fine_tuning, bg="#ddffdd", font=("Arial", 10, "bold"))
        self.btn_start_fine.pack(side=tk.LEFT, padx=5)
        
        self.btn_stop_fine = tk.Button(frame_btns, text="Fermer", command=self.destroy, bg="#ffcccc")
        self.btn_stop_fine.pack(side=tk.LEFT, padx=5)
        
        # Alias pour compatibilité avec on_close
        self.btn_start = self.btn_start_fine 
        
        self.log("Prêt pour l'affinage. Sélectionnez une durée et démarrez.")

    def start_fine_tuning(self):
        self.stop_event.clear()
        self.btn_start_fine.config(state=tk.DISABLED)
        self.btn_stop_fine.config(state=tk.DISABLED)
        threading.Thread(target=self.run_fine_tuning_process, daemon=True).start()

    def run_fine_tuning_process(self):
        duration_minutes = self.duration_var.get()
        total_seconds = duration_minutes * 60
        self.timer_display.update_timer(total_seconds, total_seconds)
        
        try:
            # 1. Arrêt NTP
            self.log("Arrêt du service NTP...")
            if not stop_service("NTP"):
                self.log("Erreur: Impossible d'arrêter le service NTP.", "red")
                return

            # 2. Effacement loopstats
            self.log("Suppression du fichier loopstats...")
            loopstats_path = get_loopstats_path()
            if os.path.exists(loopstats_path):
                try:
                    os.remove(loopstats_path)
                except Exception as e:
                    self.log(f"Erreur suppression loopstats: {e}", "red")
                    start_service("NTP")
                    return

            # 3. Démarrage NTP
            self.log("Démarrage du service NTP...")
            if not start_service("NTP"):
                self.log("Erreur: Impossible de démarrer le service NTP.", "red")
                return

            # 4. Attente Reach 377
            self.log("Attente synchronisation GPS (Reach 377)...")
            
            # Extraction numéro port COM
            serial_port = self.app.config.get("serial_port", "COM1")
            match = re.search(r'\d+', serial_port)
            com_number = match.group() if match else "1"
            
            while True:
                if not self.winfo_exists(): return
                if self.stop_event.is_set(): return
                
                reach = get_gps_reach(com_number)
                self.led_bar.set_value(reach)
                
                if reach == 377:
                    break
                time.sleep(5)

            # 5. Échantillonnage
            self.log(f"Synchronisation OK. Échantillonnage ({duration_minutes} min)...")
            elapsed = 0
            while elapsed < total_seconds:
                if not self.winfo_exists(): return
                if self.stop_event.is_set(): return
                
                self.timer_display.update_timer(total_seconds - elapsed, total_seconds)
                time.sleep(1)
                elapsed += 1
            
            self.timer_display.update_timer(0, total_seconds)

            # 6. Calcul
            self.log("Calcul du nouveau Fudge...")
            current_fudge = float(self.app.config.get("time2_value", "0.0"))
            median_offset, new_fudge = calculate_new_fudge(loopstats_path, current_fudge)
            
            if median_offset is None:
                self.log("Erreur: Pas de données valides dans loopstats.", "red")
                return

            median_offset_ms = median_offset * 1000.0
            new_fudge_ms = new_fudge * 1000.0
            self.log(f"Offset Médian: {median_offset_ms:.3f} ms")
            self.log(f"Nouveau Fudge proposé: {new_fudge_ms:.3f} ms")

            # 7. Validation
            msg = (f"Affinage terminé.\n\n"
                   f"Offset Médian : {median_offset_ms:.3f} ms\n"
                   f"Nouveau Fudge : {new_fudge_ms:.3f} ms\n\n"
                   f"Appliquer ?")
            
            if messagebox.askyesno("Résultat Affinage", msg, parent=self):
                self.log("Application des changements...")
                
                self.app.config["time2_value"] = f"{new_fudge:.4f}"
                
                if self.app.config_path:
                    with open(self.app.config_path, 'w') as f:
                        json.dump(self.app.config, f, indent=4)
                
                new_conf = generate_ntp_conf_content(self.app.config)
                target_path = self.app.config.get("config_ntp")
                
                stop_service("NTP")
                write_file_content(target_path, new_conf)
                start_service("NTP")
                
                self.log("Succès ! Configuration mise à jour.", "green")
                messagebox.showinfo("Succès", "Affinage terminé et appliqué.", parent=self)
                self.after(0, self.destroy)
            else:
                self.log("Annulé par l'utilisateur.", "orange")

        except Exception as e:
            self.log(f"Erreur critique: {e}", "red")
        finally:
            if self.winfo_exists():
                self.btn_start_fine.config(state=tk.NORMAL)
                self.btn_stop_fine.config(state=tk.NORMAL)

class GPSNTPApp:
    """
    Classe principale de l'interface graphique.
    """
    def __init__(self, root, config=None, config_path=None):
        """
        Initialise l'application GUI.
        
        Args:
            root (tk.Tk): La fenêtre racine Tkinter.
            config (dict): La configuration actuelle.
            config_path (str): Chemin du fichier config.json.
        """
        self.logger = logging.getLogger("GPS_NTP_App")
        self.root = root
        self.config = config if config else {}
        self.config_path = config_path
        self.root.title("Time Reference NMEA Bridge")
        self.root.geometry("750x800")
        
        self.setup_ui()
        self.logger.info("Interface graphique initialisée.")

    def setup_ui(self):
        """Configure les widgets de l'interface."""
        # Titre
        lbl_title = tk.Label(self.root, text="Pont GPS NMEA vers NTP", font=("Arial", 16, "bold"))
        lbl_title.pack(pady=20)
        
        # Spec 12: Horloges
        self.time_frame = TimeStatusFrame(self.root, self)
        self.time_frame.pack(side="top", fill="x", padx=20, pady=5)
        
        # Zone de statut (Placeholder pour les specs futures)
        self.lbl_status = tk.Label(self.root, text="Système prêt.", fg="green")
        self.lbl_status.pack(pady=10)
        
        # --- Actions & Diagnostics ---
        frame_actions = tk.LabelFrame(self.root, text="Actions & Diagnostics", padx=10, pady=5)
        frame_actions.pack(pady=5, fill=tk.X, padx=20)
        frame_actions.columnconfigure(0, weight=1)
        frame_actions.columnconfigure(1, weight=1)

        # Bouton Calibration (unifié)
        btn_wizard = tk.Button(frame_actions, text="Lancer une Calibration", command=self.open_calibration_wizard, bg="#d1c4e9")
        btn_wizard.grid(row=0, column=0, padx=5, pady=5, sticky="ew")
        
        # Bouton Test GPS (Spec 4)
        btn_test = tk.Button(frame_actions, text="Test GPS (Visualisation NMEA)", command=self.open_gps_test_window, bg="#fff9c4")
        btn_test.grid(row=0, column=1, padx=5, pady=5, sticky="ew")

        # Bouton Clockvar (Spec 6)
        btn_clockvar = tk.Button(frame_actions, text="Status Détaillé (Clockvar)", command=self.open_clockvar_window, bg="#e0f7fa")
        btn_clockvar.grid(row=1, column=0, padx=5, pady=5, sticky="ew")

        # Bouton IQT (Spec 9)
        btn_iqt = tk.Button(frame_actions, text="Indice Qualité Temporelle (IQT)", command=self.open_iqt_window, bg="#f3e5f5")
        btn_iqt.grid(row=1, column=1, padx=5, pady=5, sticky="ew")

        # --- Spec 5: Contrôle Service NTP ---
        frame_service = tk.LabelFrame(self.root, text="Contrôle Service NTP", padx=10, pady=5)
        frame_service.pack(pady=5, fill=tk.X, padx=20)
        
        self.lbl_service_status = tk.Label(frame_service, text="...", font=("Arial", 9, "bold"))
        self.lbl_service_status.pack(side=tk.TOP, pady=(0, 5))
        
        frame_btns_svc = tk.Frame(frame_service)
        frame_btns_svc.pack(fill=tk.X)
        
        tk.Button(frame_btns_svc, text="Démarrer", command=lambda: self.service_action("start"), bg="#ccffcc").pack(side=tk.LEFT, expand=True, padx=5)
        tk.Button(frame_btns_svc, text="Arrêter", command=lambda: self.service_action("stop"), bg="#ffcccc").pack(side=tk.LEFT, expand=True, padx=5)
        tk.Button(frame_btns_svc, text="Redémarrer", command=lambda: self.service_action("restart"), bg="#ffffcc").pack(side=tk.LEFT, expand=True, padx=5)
        
        self.update_service_status_loop()

        # --- Spec 5: Outils (Paramètres & Logs) ---
        frame_tools = tk.Frame(self.root)
        frame_tools.pack(pady=10)
        
        tk.Button(frame_tools, text="Paramètres", command=self.open_settings_window).pack(side=tk.LEFT, padx=10)
        tk.Button(frame_tools, text="Voir les Logs", command=self.open_log_viewer).pack(side=tk.LEFT, padx=10)
        tk.Button(frame_tools, text="Voir ntp.conf", command=self.open_ntp_conf_viewer).pack(side=tk.LEFT, padx=10)

        # --- Spec 7: Visualisation ntpq -p ---
        frame_monitor = tk.LabelFrame(self.root, text="Monitoring Peers (ntpq -p)", padx=5, pady=5)
        frame_monitor.pack(pady=5, fill=tk.BOTH, expand=True, padx=10)
        
        self.txt_monitor = scrolledtext.ScrolledText(frame_monitor, height=10, state=tk.DISABLED, bg="#f0f0f0", font=("Consolas", 9))
        self.txt_monitor.pack(fill=tk.BOTH, expand=True)
        
        self.update_ntpq_p_loop()

        # Bouton Quitter
        btn_quit = tk.Button(self.root, text="Quitter", command=self.root.quit)
        btn_quit.pack(side=tk.BOTTOM, pady=20)

    @staticmethod
    def show_w32time_warning():
        """
        Affiche une boîte de dialogue modale avertissant que W32Time est actif.
        Propose d'ouvrir le site de Meinberg.
        """
        logger = logging.getLogger("GPS_NTP_App")
        logger.warning("Affichage de l'avertissement W32Time à l'utilisateur.")
        
        msg = (
            "Le service Windows Time (W32Time) est actif.\n\n"
            "Cela indique que 'NTP by Meinberg' n'est probablement pas installé ou actif.\n"
            "Ce programme nécessite NTP by Meinberg pour fonctionner correctement.\n\n"
            "Voulez-vous ouvrir la page de téléchargement maintenant ?"
        )
        
        # On crée une fenêtre temporaire cachée car la mainloop n'est pas encore lancée
        root = tk.Tk()
        root.withdraw() 
        
        response = messagebox.askyesno("Conflit de Service NTP", msg, icon='warning')
        
        if response:
            url = "https://www.meinbergglobal.com/english/sw/ntp.htm"
            logger.info(f"Ouverture du navigateur vers {url}")
            webbrowser.open(url)
        
        root.destroy()

    @staticmethod
    def show_config_proposal(new_content, current_content, file_path):
        """
        Affiche une fenêtre proposant la mise à jour du fichier de configuration.
        Affiche l'ancien et le nouveau contenu côte à côte.
        
        Returns:
            bool: True si l'utilisateur accepte, False sinon.
        """
        # Création d'une fenêtre temporaire si aucune racine n'existe
        root = tk.Tk()
        root.title("Mise à jour Configuration NTP")
        root.geometry("1000x600") # Plus large pour l'affichage côte à côte
        
        lbl = tk.Label(root, text=f"Le fichier de configuration NTP diffère ou est manquant.\nCible : {file_path}", justify=tk.LEFT)
        lbl.pack(pady=10, padx=10)
        
        # Conteneur pour les deux zones de texte
        frame_compare = tk.Frame(root)
        frame_compare.pack(fill=tk.BOTH, expand=True, padx=10)

        # Zone Gauche : Actuel
        frame_left = tk.Frame(frame_compare)
        frame_left.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(0, 5))
        tk.Label(frame_left, text="Fichier Actuel (Disque)", font=("Arial", 10, "bold")).pack()
        txt_current = scrolledtext.ScrolledText(frame_left, height=20)
        txt_current.insert(tk.END, current_content if current_content else "--- Fichier inexistant ou vide ---")
        txt_current.config(state=tk.DISABLED, bg="#f0f0f0")
        txt_current.pack(fill=tk.BOTH, expand=True)

        # Zone Droite : Nouveau
        frame_right = tk.Frame(frame_compare)
        frame_right.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(5, 0))
        tk.Label(frame_right, text="Nouvelle Configuration (Proposée)", font=("Arial", 10, "bold")).pack()
        txt_new = scrolledtext.ScrolledText(frame_right, height=20)
        txt_new.insert(tk.END, new_content)
        txt_new.config(state=tk.DISABLED, bg="#e6ffe6") # Légèrement vert
        txt_new.pack(fill=tk.BOTH, expand=True)
        
        result = tk.BooleanVar(value=False)
        
        def on_accept():
            result.set(True)
            root.destroy()
            
        def on_cancel():
            result.set(False)
            root.destroy()
            
        btn_frame = tk.Frame(root)
        btn_frame.pack(pady=10)
        
        tk.Button(btn_frame, text="Accepter et Mettre à jour", command=on_accept, bg="#ddffdd").pack(side=tk.LEFT, padx=10)
        tk.Button(btn_frame, text="Ignorer", command=on_cancel).pack(side=tk.LEFT, padx=10)
        
        # Rend la fenêtre modale
        root.wait_window()
        return result.get()

    def open_calibration_wizard(self):
        """Ouvre une boîte de dialogue pour choisir le mode de calibration."""
        self.logger.info("Ouverture du choix de mode de calibration.")
        
        dialog = tk.Toplevel(self.root)
        dialog.title("Choix du Mode de Calibration")
        dialog.geometry("450x250")
        dialog.transient(self.root)
        dialog.grab_set()
        
        # Centrage
        self.root.update_idletasks()
        x = self.root.winfo_rootx() + (self.root.winfo_width() - 450) // 2
        y = self.root.winfo_rooty() + (self.root.winfo_height() - 250) // 2
        dialog.geometry(f"+{x}+{y}")

        tk.Label(dialog, text="Quel type de calibration souhaitez-vous lancer ?", font=("Arial", 11, "bold")).pack(pady=20)
        
        def on_gps_only():
            dialog.destroy()
            self.logger.info("Choix : Calibration GPS Seul (Loopstats).")
            self.open_calibration_window()

        def on_expert():
            dialog.destroy()
            self.logger.info("Choix : Assistant Calibration Expert (GPS vs Net).")
            CalibrationWizard(self.root, self)

        btn_frame = tk.Frame(dialog)
        btn_frame.pack(pady=10, fill=tk.X, padx=20)
        
        btn_gps_only = tk.Button(btn_frame, text="GPS Seul (via Loopstats)\nRecommandé si Internet est instable ou absent.", command=on_gps_only, height=3, bg="#e1f5fe", justify=tk.LEFT)
        btn_gps_only.pack(fill=tk.X, pady=5)
        
        btn_expert = tk.Button(btn_frame, text="GPS vs Serveurs Net (Assistant Expert)\nPlus précis si Internet est stable.", command=on_expert, height=3, bg="#d1c4e9", justify=tk.LEFT)
        btn_expert.pack(fill=tk.X, pady=5)

        dialog.protocol("WM_DELETE_WINDOW", dialog.destroy)

    def open_calibration_window(self):
        """Ouvre la fenêtre dédiée à la calibration (Spec 3)."""
        self.logger.info("Ouverture de la fenêtre de calibration.")
        calib_win = tk.Toplevel(self.root)
        calib_win.title("Calibration Fudge Time2")
        calib_win.geometry("500x550") # Un peu plus haut pour les widgets
        
        tk.Label(calib_win, text="Calibration Automatique du Fudge", font=("Arial", 12, "bold")).pack(pady=10)
        
        # Choix de la durée
        tk.Label(calib_win, text="Choisir la durée d'échantillonnage :").pack(pady=5)
        duration_var = tk.IntVar(value=5)
        
        frame_radio = tk.Frame(calib_win)
        frame_radio.pack()
        tk.Radiobutton(frame_radio, text="Rapide (2 min)", variable=duration_var, value=2).pack(side=tk.LEFT, padx=5)
        tk.Radiobutton(frame_radio, text="Standard (5 min)", variable=duration_var, value=5).pack(side=tk.LEFT, padx=5)
        tk.Radiobutton(frame_radio, text="Haute Précision (20 min)", variable=duration_var, value=20).pack(side=tk.LEFT, padx=5)
        
        # --- Indicateurs Visuels ---
        frame_indicators = tk.Frame(calib_win, pady=15)
        frame_indicators.pack(fill=tk.X, padx=20)
        
        # Zone Reach (Gauche)
        frame_reach = tk.Frame(frame_indicators)
        frame_reach.pack(side=tk.LEFT, expand=True)
        tk.Label(frame_reach, text="Qualité Sync (Reach)", font=("Arial", 9)).pack()
        led_bar = LedBar(frame_reach, width=180, height=30)
        led_bar.pack(pady=5)
        
        # Zone Timer (Droite)
        frame_timer = tk.Frame(frame_indicators)
        frame_timer.pack(side=tk.RIGHT, expand=True)
        tk.Label(frame_timer, text="Temps Restant", font=("Arial", 9)).pack()
        timer_display = CircularTimer(frame_timer, size=80)
        timer_display.pack(pady=5)

        # Zone de progression
        lbl_status = tk.Label(calib_win, text="Prêt à démarrer.", fg="blue", wraplength=450)
        lbl_status.pack(pady=20)
        
        # Frame boutons
        frame_btns = tk.Frame(calib_win)
        frame_btns.pack(pady=10)

        stop_event = threading.Event()
        pause_event = threading.Event()

        btn_toggle = tk.Button(frame_btns, text="Démarrer la Calibration", bg="#ddffdd", font=("Arial", 10, "bold"))
        btn_pause = tk.Button(frame_btns, text="Pause", state=tk.DISABLED, bg="#fff9c4", font=("Arial", 10))

        def on_pause():
            if pause_event.is_set():
                pause_event.clear()
                btn_pause.config(text="Pause", bg="#fff9c4")
            else:
                pause_event.set()
                btn_pause.config(text="Reprendre", bg="#ffcc80")
        
        btn_pause.config(command=on_pause)

        def on_toggle():
            if btn_toggle['text'].startswith("Démarrer"):
                stop_event.clear()
                pause_event.clear()
                btn_toggle.config(text="Arrêter la Calibration", bg="#ffdddd")
                btn_pause.config(state=tk.NORMAL, text="Pause", bg="#fff9c4")
                self.start_calibration_thread(duration_var.get(), calib_win, lbl_status, btn_toggle, btn_pause, stop_event, pause_event, led_bar, timer_display)
            else:
                stop_event.set()
                btn_toggle.config(state=tk.DISABLED)
                btn_pause.config(state=tk.DISABLED)
                lbl_status.config(text="Arrêt demandé...", fg="orange")

        btn_toggle.config(command=on_toggle)
        btn_toggle.pack(side=tk.LEFT, padx=5)
        btn_pause.pack(side=tk.LEFT, padx=5)

        def on_close():
            if btn_toggle['text'].startswith("Arrêter"):
                messagebox.showwarning("Attention", "Veuillez arrêter la calibration avant de fermer la fenêtre.", parent=calib_win)
                return
            self.logger.info("Fermeture de la fenêtre de calibration.")
            calib_win.destroy()
        calib_win.protocol("WM_DELETE_WINDOW", on_close)

    def start_calibration_thread(self, duration_minutes, win, lbl_status, btn_toggle, btn_pause, stop_event, pause_event, led_bar, timer_display):
        """Lance le processus de calibration dans un thread séparé."""
        self.logger.info(f"Lancement de la calibration (Durée: {duration_minutes} min).")
        thread = threading.Thread(target=self.run_calibration_process, args=(duration_minutes, win, lbl_status, btn_toggle, btn_pause, stop_event, pause_event, led_bar, timer_display))
        thread.daemon = True
        thread.start()

    def run_calibration_process(self, duration_minutes, win, lbl_status, btn_toggle, btn_pause, stop_event, pause_event, led_bar, timer_display):
        """Logique métier de la calibration (Spec 3)."""
        def update_status(text, color="blue"):
            lbl_status.config(text=text, fg=color)
        
        try:
            if stop_event.is_set(): return

            # Réinitialisation du timer visuel au début du processus
            total_seconds = duration_minutes * 60
            timer_display.update_timer(total_seconds, total_seconds)

            # 1. Arrêt NTP
            if is_service_active("NTP"):
                update_status("Arrêt du service NTP...")
                if not stop_service("NTP"):
                    update_status("Erreur: Impossible d'arrêter le service NTP.", "red")
                    return
            else:
                update_status("Service NTP déjà arrêté.")

            # 2. Effacement loopstats
            update_status("Suppression du fichier loopstats...")
            loopstats_path = get_loopstats_path()
            if os.path.exists(loopstats_path):
                try:
                    os.remove(loopstats_path)
                except Exception as e:
                    update_status(f"Erreur suppression loopstats: {e}", "red")
                    start_service("NTP")
                    return

            # 3. Démarrage NTP
            update_status("Démarrage du service NTP...")
            if not start_service("NTP"):
                update_status("Erreur: Impossible de démarrer le service NTP.", "red")
                return

            # 4. Attente Reach 377
            update_status("Attente synchronisation GPS (Reach 377)...")
            
            # Extraction numéro port COM
            serial_port = self.config.get("serial_port", "COM1")
            match = re.search(r'\d+', serial_port)
            com_number = match.group() if match else "1"
            
            while True:
                if not win.winfo_exists(): return # Fenêtre fermée
                if stop_event.is_set():
                    update_status("Calibration arrêtée par l'utilisateur.", "orange")
                    return
                reach = get_gps_reach(com_number)
                update_status("Synchronisation en cours...")
                led_bar.set_value(reach) # Mise à jour visuelle des LEDs
                
                if reach == 377: # 377 octal = 255 decimal (8 bits à 1)
                    break
                
                time.sleep(5)

            # 5. Échantillonnage
            update_status(f"Synchronisation OK. Échantillonnage pendant {duration_minutes} minutes...")
            elapsed = 0
            while elapsed < total_seconds:
                if not win.winfo_exists(): return
                if stop_event.is_set():
                    update_status("Calibration arrêtée par l'utilisateur.", "orange")
                    return
                
                if pause_event.is_set():
                    update_status("Calibration en PAUSE...", "orange")
                    time.sleep(0.5)
                    continue

                if elapsed % 5 == 0: # Mise à jour affichage toutes les 5s
                    update_status("Acquisition de données...")
                
                timer_display.update_timer(total_seconds - elapsed, total_seconds) # Mise à jour visuelle du timer
                time.sleep(1)
                elapsed += 1
            
            # Force l'affichage à 00:00 à la fin
            timer_display.update_timer(0, total_seconds)

            # 6. Calcul
            update_status("Calcul du nouveau Fudge...")
            current_fudge = float(self.config.get("time2_value", "0.400"))
            median_offset, new_fudge = calculate_new_fudge(loopstats_path, current_fudge)
            
            if median_offset is None:
                update_status("Erreur: Pas de données valides dans loopstats.", "red")
                return

            # 7. Validation Utilisateur (via messagebox dans le thread principal)
            msg = (
                f"Calibration terminée.\n\n"
                f"Compensation actuelle : {current_fudge:.4f} s\n"
                f"Offset Médian mesuré : {median_offset:.6f} s\n"
                f"Nouvelle compensation proposée : {new_fudge:.4f} s\n\n"
                f"Voulez-vous appliquer cette valeur ?"
            )
            
            if messagebox.askyesno("Résultat Calibration", msg, parent=win):
                # 8. Application
                update_status("Application des changements...")
                
                self.logger.info(f"Calibration validée par l'utilisateur. Ancien Fudge: {current_fudge:.4f}, Nouveau Fudge: {new_fudge:.4f}")
                
                # Mise à jour config objet
                self.config["time2_value"] = f"{new_fudge:.4f}"
                
                # Sauvegarde config.json
                if self.config_path:
                    with open(self.config_path, 'w') as f:
                        json.dump(self.config, f, indent=4)
                
                # Génération et écriture ntp.conf
                new_conf_content = generate_ntp_conf_content(self.config)
                target_ntp_conf = self.config.get("config_ntp", "")
                local_conf_path = os.path.join(os.path.dirname(os.path.abspath(self.config_path)), 'ntp.conf')
                
                write_file_content(local_conf_path, new_conf_content)
                
                stop_service("NTP")
                write_file_content(target_ntp_conf, new_conf_content)
                start_service("NTP")
                
                update_status("Succès ! Configuration mise à jour et NTP redémarré.", "green")
                self.logger.info("Fermeture de la fenêtre de calibration (Succès).")
                win.destroy()
            else:
                update_status("Annulé par l'utilisateur.", "orange")

        except Exception as e:
            self.logger.error(f"Erreur process calibration: {e}")
            update_status(f"Erreur inattendue: {e}", "red")
        finally:
            self.logger.info("Fin de la procédure de calibration.")
            if win.winfo_exists():
                btn_toggle.config(text="Démarrer la Calibration", bg="#ddffdd", state=tk.NORMAL)
                btn_pause.config(state=tk.DISABLED, text="Pause", bg="#fff9c4")

    def open_gps_test_window(self):
        """Ouvre la fenêtre de test GPS (Spec 4)."""
        self.logger.info("Ouverture de la fenêtre de test GPS.")
        test_win = tk.Toplevel(self.root)
        test_win.title("Test du GPS (NMEA)")
        test_win.geometry("700x500")

        # Zone de texte défilante (Look terminal)
        txt_console = scrolledtext.ScrolledText(test_win, state=tk.DISABLED, bg="black", fg="#00FF00", font=("Consolas", 10))
        txt_console.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        # Frame boutons
        frame_btns = tk.Frame(test_win)
        frame_btns.pack(pady=10)

        btn_toggle = tk.Button(frame_btns, text="Démarrer le Test", bg="#ddffdd", font=("Arial", 10, "bold"))
        btn_toggle.pack()

        # État local pour gérer le service NTP et le thread
        state = {"ntp_stopped_by_us": False, "running": False}
        stop_event = threading.Event()

        def append_log(text):
            """Ajoute du texte dans la console avec gestion intelligente du scroll."""
            if not test_win.winfo_exists(): return
            
            # Si l'ascenseur est tout en bas (1.0), on scrollera automatiquement.
            # Sinon (l'utilisateur regarde l'historique), on ne bouge pas.
            try:
                is_at_bottom = txt_console.yview()[1] >= 1.0
            except:
                is_at_bottom = True

            txt_console.config(state=tk.NORMAL)
            txt_console.insert(tk.END, text + "\n")
            if is_at_bottom:
                txt_console.see(tk.END)
            txt_console.config(state=tk.DISABLED)

        def serial_thread_func():
            port = self.config.get("serial_port", "COM1")
            baud = self.config.get("baud_rate", 9600)
            try:
                with serial.Serial(port, baud, timeout=0.5) as ser:
                    test_win.after(0, append_log, f"--- Port {port} ouvert ({baud} bauds) ---")
                    while not stop_event.is_set():
                        if not test_win.winfo_exists(): break
                        try:
                            line = ser.readline()
                            if line:
                                decoded = line.decode('ascii', errors='replace').strip()
                                if decoded:
                                    test_win.after(0, append_log, decoded)
                        except Exception as e:
                            test_win.after(0, append_log, f"Erreur lecture: {e}")
                            break
            except Exception as e:
                test_win.after(0, append_log, f"Erreur ouverture port: {e}")
                test_win.after(0, lambda: btn_toggle.config(text="Démarrer le Test", bg="#ddffdd"))
                state["running"] = False

        def on_toggle():
            if not state["running"]:
                # Démarrage du test
                if is_service_active("NTP"):
                    if messagebox.askyesno("Conflit NTP", "Le service NTP est actif et utilise le port série.\nVoulez-vous l'arrêter pour lancer le test ?", parent=test_win):
                        append_log("Arrêt du service NTP...")
                        if stop_service("NTP"):
                            state["ntp_stopped_by_us"] = True
                            append_log("Service NTP arrêté.")
                        else:
                            append_log("Erreur: Impossible d'arrêter NTP.")
                            return
                    else:
                        return # Annulation par l'utilisateur
                
                state["running"] = True
                stop_event.clear()
                btn_toggle.config(text="Arrêter le Test", bg="#ffdddd")
                threading.Thread(target=serial_thread_func, daemon=True).start()
            else:
                # Arrêt du test
                state["running"] = False
                stop_event.set()
                btn_toggle.config(text="Démarrer le Test", bg="#ddffdd")
                append_log("--- Test arrêté ---")

        btn_toggle.config(command=on_toggle)

        def on_close():
            if state["running"]:
                messagebox.showwarning("Test en cours", "Veuillez arrêter le test avant de fermer la fenêtre.", parent=test_win)
                return
            
            if state["ntp_stopped_by_us"]:
                self.logger.info("Redémarrage du service NTP suite fermeture test GPS.")
                start_service("NTP")
            
            test_win.destroy()
        
        test_win.protocol("WM_DELETE_WINDOW", on_close)

    def update_service_status_loop(self):
        """Met à jour le label d'état du service périodiquement."""
        try:
            if not self.root.winfo_exists(): return
            
            if is_service_active("NTP"):
                self.lbl_service_status.config(text="Service NTP : DÉMARRÉ", fg="green")
            else:
                self.lbl_service_status.config(text="Service NTP : ARRÊTÉ", fg="red")
        except Exception:
            pass
        finally:
            self.root.after(500, self.update_service_status_loop)

    def update_ntpq_p_loop(self):
        """Lance le thread de récupération ntpq -p (Spec 7)."""
        if not self.root.winfo_exists(): return
        
        # Exécution dans un thread pour éviter le gel de l'interface dû au DNS
        thread = threading.Thread(target=self._run_ntpq_p)
        thread.daemon = True
        thread.start()

    def _run_ntpq_p(self):
        """Exécute la commande bloquante en arrière-plan."""
        try:
            startupinfo = subprocess.STARTUPINFO()
            startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            cmd = get_ntpq_command()
            process = subprocess.Popen([cmd, "-p"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, startupinfo=startupinfo)
            stdout, stderr = process.communicate()
            result = stdout if stdout else stderr
        except Exception as e:
            result = f"Erreur ntpq: {e}"

        # Retour sur le thread principal pour l'affichage
        try:
            self.root.after(0, self._update_monitor_ui, result)
        except Exception:
            pass

    def _update_monitor_ui(self, text):
        """Met à jour l'UI et reprogramme la prochaine exécution."""
        try:
            if not self.root.winfo_exists(): return

            self.txt_monitor.config(state=tk.NORMAL)
            self.txt_monitor.delete(1.0, tk.END)
            self.txt_monitor.insert(tk.END, text)
            self.txt_monitor.config(state=tk.DISABLED)
        except Exception:
            pass
        finally:
            # On relance dans 5s après la fin de la mise à jour
            self.root.after(5000, self.update_ntpq_p_loop)

    def service_action(self, action):
        """Gère les actions sur le service NTP (Spec 5)."""
        is_running = is_service_active("NTP")
        
        if action == "start":
            if is_running: return
            if not start_service("NTP"):
                messagebox.showerror("Service NTP", "Échec du démarrage du service.")
        elif action == "stop":
            if not is_running: return
            if not stop_service("NTP"):
                messagebox.showerror("Service NTP", "Échec de l'arrêt du service.")
        elif action == "restart":
            stop_service("NTP")
            if not start_service("NTP"):
                messagebox.showerror("Service NTP", "Échec du redémarrage du service.")

    def open_settings_window(self):
        """Ouvre la fenêtre de paramétrage (Spec 5)."""
        win = tk.Toplevel(self.root)
        win.title("Paramètres")
        win.geometry("500x520")
        
        tk.Label(win, text="Port Série (ex: COM1):").pack(pady=(10, 0))
        entry_port = tk.Entry(win)
        entry_port.insert(0, self.config.get("serial_port", "COM1"))
        entry_port.pack()
        
        tk.Label(win, text="Vitesse (Baud Rate):").pack(pady=(10, 0))
        entry_baud = tk.Entry(win)
        entry_baud.insert(0, str(self.config.get("baud_rate", 9600)))
        entry_baud.pack()
        
        tk.Label(win, text="Chemin ntp.conf:").pack(pady=(10, 0))
        frame_path = tk.Frame(win)
        frame_path.pack(fill=tk.X, padx=20)
        entry_path = tk.Entry(frame_path)
        entry_path.insert(0, self.config.get("config_ntp", ""))
        entry_path.pack(side=tk.LEFT, fill=tk.X, expand=True)
        
        def browse_file():
            filename = filedialog.askopenfilename(filetypes=[("Config files", "*.conf"), ("All files", "*.*")])
            if filename:
                entry_path.delete(0, tk.END)
                entry_path.insert(0, filename)
        tk.Button(frame_path, text="...", command=browse_file).pack(side=tk.LEFT, padx=5)
        
        tk.Label(win, text="Serveurs NTP (un par ligne):").pack(pady=(10, 0))
        txt_servers = scrolledtext.ScrolledText(win, height=5)
        servers = self.config.get("servers", [])
        txt_servers.insert(tk.END, "\n".join(servers))
        txt_servers.pack(padx=20, pady=(0, 10), fill=tk.X, expand=True)
        
        tk.Label(win, text="Options communes (ex: 'iburst noselect'):").pack(pady=(5, 0))
        entry_options = tk.Entry(win)
        entry_options.insert(0, self.config.get("server_options", "iburst"))
        entry_options.pack(padx=20, fill=tk.X)
        
        tk.Label(win, text="Fudge Time2 (s):").pack(pady=(5, 0))
        entry_fudge = tk.Entry(win)
        entry_fudge.insert(0, self.config.get("time2_value", "0.000"))
        entry_fudge.pack(padx=20, fill=tk.X)
        
        def save_settings():
            self.config["serial_port"] = entry_port.get()
            try:
                self.config["baud_rate"] = int(entry_baud.get())
            except ValueError:
                messagebox.showerror("Erreur", "La vitesse doit être un entier.")
                return
            self.config["config_ntp"] = entry_path.get()
            
            # Lecture et sauvegarde de la liste des serveurs
            server_list_raw = txt_servers.get("1.0", tk.END).strip()
            # Sépare par ligne et filtre les lignes vides
            self.config["servers"] = [line for line in server_list_raw.split('\n') if line.strip()]
            self.config["server_options"] = entry_options.get()
            self.config["time2_value"] = entry_fudge.get()
            
            if self.config_path:
                try:
                    with open(self.config_path, 'w') as f:
                        json.dump(self.config, f, indent=4)
                    
                    # Mise à jour de ntp.conf et redémarrage du service
                    if messagebox.askyesno("Paramètres", "Paramètres enregistrés.\nVoulez-vous mettre à jour ntp.conf et redémarrer le service NTP ?"):
                        new_conf = generate_ntp_conf_content(self.config)
                        target_path = self.config.get("config_ntp", "")
                        
                        if target_path:
                            stop_service("NTP")
                            if write_file_content(target_path, new_conf):
                                start_service("NTP")
                            else:
                                messagebox.showerror("Erreur", "Impossible d'écrire le fichier ntp.conf")

                    win.destroy()
                except Exception as e:
                    messagebox.showerror("Erreur", f"Erreur sauvegarde: {e}")
            else:
                messagebox.showwarning("Attention", "Chemin de configuration non défini.")
                win.destroy()

        frame_btns = tk.Frame(win)
        frame_btns.pack(pady=20)
        tk.Button(frame_btns, text="Enregistrer", command=save_settings, bg="#ddffdd").pack(side=tk.LEFT, padx=10)
        tk.Button(frame_btns, text="Annuler", command=win.destroy, bg="#ffcccc").pack(side=tk.LEFT, padx=10)

    def open_log_viewer(self):
        """Ouvre le visualiseur de logs (Spec 5)."""
        win = tk.Toplevel(self.root)
        win.title("Visualiseur de Logs")
        win.geometry("950x600")
        
        # --- Frame Sélection Fichier ---
        frame_file = tk.Frame(win)
        frame_file.pack(fill=tk.X, padx=10, pady=5)
        
        tk.Label(frame_file, text="Fichier Log :").pack(side=tk.LEFT)
        
        combo_files = ttk.Combobox(frame_file, state="readonly", width=35)
        combo_files.pack(side=tk.LEFT, padx=5)
        
        btn_delete = tk.Button(frame_file, text="Supprimer", bg="#ffcccc")
        btn_delete.pack(side=tk.LEFT, padx=5)
        
        tk.Button(frame_file, text="Rafraîchir Liste", command=lambda: refresh_file_list()).pack(side=tk.LEFT, padx=5)
        
        # --- Frame Filtres ---
        frame_filters = tk.Frame(win)
        frame_filters.pack(fill=tk.X, padx=10, pady=5)
        
        var_info = tk.BooleanVar(value=True)
        var_warn = tk.BooleanVar(value=True)
        var_err = tk.BooleanVar(value=True)
        
        chk_info = tk.Checkbutton(frame_filters, text="INFO", variable=var_info)
        chk_info.pack(side=tk.LEFT)
        chk_warn = tk.Checkbutton(frame_filters, text="WARNING", variable=var_warn)
        chk_warn.pack(side=tk.LEFT)
        chk_err = tk.Checkbutton(frame_filters, text="ERROR", variable=var_err)
        chk_err.pack(side=tk.LEFT)
        
        tk.Label(frame_filters, text="Recherche:").pack(side=tk.LEFT, padx=(20, 5))
        entry_search = tk.Entry(frame_filters)
        entry_search.pack(side=tk.LEFT, fill=tk.X, expand=True)
        
        tree = ttk.Treeview(win, columns=("Time", "Level", "Message"), show="headings")
        tree.heading("Time", text="Heure"); tree.column("Time", width=150)
        tree.heading("Level", text="Niveau"); tree.column("Level", width=80)
        tree.heading("Message", text="Message"); tree.column("Message", width=600)
        
        scrollbar = ttk.Scrollbar(win, orient=tk.VERTICAL, command=tree.yview)
        tree.configure(yscroll=scrollbar.set)
        tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=10, pady=5)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # --- Logique ---
        log_dir = "logs"
        current_date_str = time.strftime('%Y-%m-%d')
        current_log_filename = f"log_{current_date_str}.txt"

        def load_logs(*args):
            for item in tree.get_children(): tree.delete(item)
            
            selected_file = combo_files.get()
            if not selected_file: return
            
            # Gestion état bouton supprimer
            if selected_file == current_log_filename:
                btn_delete.config(state=tk.DISABLED)
            else:
                btn_delete.config(state=tk.NORMAL)

            log_path = os.path.join(log_dir, selected_file)
            if os.path.exists(log_path):
                try:
                    with open(log_path, 'r', encoding='utf-8') as f:
                        for line in f:
                            parts = line.split(" - ", 3)
                            if len(parts) >= 4:
                                ts, _, lvl, msg = parts[0], parts[1], parts[2].strip(), parts[3].strip()
                                if (lvl == "INFO" and not var_info.get()) or \
                                   (lvl == "WARNING" and not var_warn.get()) or \
                                   (lvl == "ERROR" and not var_err.get()): continue
                                
                                search_txt = entry_search.get().lower()
                                if search_txt and search_txt not in msg.lower() and search_txt not in ts: continue
                                
                                tree.insert("", tk.END, values=(ts, lvl, msg), tags=(lvl,))
                except Exception as e:
                    tree.insert("", tk.END, values=("Error", "ERROR", f"Impossible de lire le fichier: {e}"), tags=("ERROR",))

            tree.tag_configure("INFO", foreground="black")
            tree.tag_configure("WARNING", foreground="orange")
            tree.tag_configure("ERROR", foreground="red")

        def refresh_file_list():
            if not os.path.exists(log_dir):
                os.makedirs(log_dir)
            
            files = [f for f in os.listdir(log_dir) if f.startswith("log_") and f.endswith(".txt")]
            files.sort(reverse=True) # Plus récents en premier
            
            combo_files['values'] = files
            
            # Sélection par défaut
            if current_log_filename in files:
                combo_files.set(current_log_filename)
            elif files:
                combo_files.current(0)
            else:
                combo_files.set("")
            
            load_logs()

        def delete_selected_log():
            selected_file = combo_files.get()
            if not selected_file: return
            if selected_file == current_log_filename:
                messagebox.showwarning("Suppression impossible", "Vous ne pouvez pas supprimer le log de la journée en cours.", parent=win)
                return
            
            if messagebox.askyesno("Confirmation", f"Voulez-vous vraiment supprimer le fichier {selected_file} ?", parent=win):
                try:
                    os.remove(os.path.join(log_dir, selected_file))
                    refresh_file_list()
                except Exception as e:
                    messagebox.showerror("Erreur", f"Erreur lors de la suppression : {e}", parent=win)

        # Bindings et Commandes
        chk_info.config(command=load_logs)
        chk_warn.config(command=load_logs)
        chk_err.config(command=load_logs)
        entry_search.bind("<KeyRelease>", load_logs)
        combo_files.bind("<<ComboboxSelected>>", load_logs)
        
        btn_delete.config(command=delete_selected_log)

        # Init
        refresh_file_list()

    def open_ntp_conf_viewer(self):
        """Ouvre le visualiseur de ntp.conf (Spec 8)."""
        win = tk.Toplevel(self.root)
        win.title("Visualisation ntp.conf")
        win.geometry("700x600")
        
        path = self.config.get("config_ntp", "Non défini")
        
        frame_top = tk.Frame(win)
        frame_top.pack(fill=tk.X, padx=10, pady=5)
        
        tk.Label(frame_top, text=f"Chemin : {path}", font=("Arial", 10, "bold")).pack(side=tk.LEFT)
        tk.Button(frame_top, text="Rafraîchir", command=lambda: load_content()).pack(side=tk.RIGHT)
        
        txt_conf = scrolledtext.ScrolledText(win, font=("Consolas", 10))
        txt_conf.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)
        
        def load_content():
            txt_conf.config(state=tk.NORMAL)
            txt_conf.delete(1.0, tk.END)
            if path and os.path.exists(path):
                try:
                    with open(path, 'r', encoding='utf-8', errors='replace') as f:
                        content = f.read()
                    txt_conf.insert(tk.END, content)
                except Exception as e:
                    txt_conf.insert(tk.END, f"Erreur lecture fichier: {e}")
            else:
                txt_conf.insert(tk.END, "Fichier introuvable.")
            txt_conf.config(state=tk.DISABLED)

        load_content()

    def open_clockvar_window(self):
        """Ouvre la fenêtre de visualisation ntpq -c clockvar (Spec 6)."""
        self.logger.info("Ouverture de la fenêtre Clockvar.")
        cv_win = tk.Toplevel(self.root)
        cv_win.title("Détails Driver NMEA (clockvar)")
        cv_win.geometry("900x800")

        # Zone Raw Data
        lbl_raw_title = tk.Label(cv_win, text="Données Brutes (ntpq -c clockvar)", font=("Arial", 10, "bold"))
        lbl_raw_title.pack(pady=(10, 5))
        
        txt_raw = scrolledtext.ScrolledText(cv_win, height=8, state=tk.DISABLED, bg="#f0f0f0", font=("Consolas", 9))
        txt_raw.pack(fill=tk.X, padx=10)

        # Zone Décodée
        lbl_decoded_title = tk.Label(cv_win, text="Informations Décodées", font=("Arial", 10, "bold"))
        lbl_decoded_title.pack(pady=(15, 5))

        frame_decoded = tk.Frame(cv_win)
        frame_decoded.pack(fill=tk.BOTH, expand=True, padx=20)

        # Labels pour les infos
        self.lbl_cv_time = tk.Label(frame_decoded, text="Heure UTC: --:--:--", font=("Arial", 11))
        self.lbl_cv_time.grid(row=0, column=0, sticky="w", pady=2, padx=10)
        
        self.lbl_cv_date = tk.Label(frame_decoded, text="Date: --/--/----", font=("Arial", 11))
        self.lbl_cv_date.grid(row=0, column=1, sticky="w", pady=2, padx=10)

        self.lbl_cv_status = tk.Label(frame_decoded, text="Statut GPS: --", font=("Arial", 11))
        self.lbl_cv_status.grid(row=1, column=0, columnspan=2, sticky="w", pady=2, padx=10)

        self.lbl_cv_pos = tk.Label(frame_decoded, text="Position: --", font=("Arial", 11))
        self.lbl_cv_pos.grid(row=2, column=0, columnspan=2, sticky="w", pady=2, padx=10)

        tk.Frame(frame_decoded, height=10).grid(row=3, column=0) # Spacer

        self.lbl_cv_fudge = tk.Label(frame_decoded, text="Fudge Time 2: -- ms", font=("Arial", 11))
        self.lbl_cv_fudge.grid(row=4, column=0, sticky="w", pady=2, padx=10)

        self.lbl_cv_stratum = tk.Label(frame_decoded, text="Stratum: --", font=("Arial", 11))
        self.lbl_cv_stratum.grid(row=4, column=1, sticky="w", pady=2, padx=10)

        self.lbl_cv_refid = tk.Label(frame_decoded, text="RefID: --", font=("Arial", 11))
        self.lbl_cv_refid.grid(row=5, column=0, sticky="w", pady=2, padx=10)

        self.lbl_cv_health = tk.Label(frame_decoded, text="Santé (Poll/NoReply): --", font=("Arial", 11))
        self.lbl_cv_health.grid(row=5, column=1, sticky="w", pady=2, padx=10)

        tk.Frame(frame_decoded, height=10).grid(row=6, column=0) # Spacer

        lbl_sys_title = tk.Label(frame_decoded, text="Performances Système (NTP)", font=("Arial", 10, "bold"))
        lbl_sys_title.grid(row=7, column=0, columnspan=2, sticky="w", pady=(5, 2))

        self.lbl_sys_offset = tk.Label(frame_decoded, text="Offset: -- ms", font=("Arial", 11))
        self.lbl_sys_offset.grid(row=8, column=0, sticky="w", pady=2, padx=10)

        self.lbl_sys_drift = tk.Label(frame_decoded, text="Drift (Freq): -- ppm", font=("Arial", 11))
        self.lbl_sys_drift.grid(row=8, column=1, sticky="w", pady=2, padx=10)

        self.lbl_sys_jitter = tk.Label(frame_decoded, text="Jitter: -- ms", font=("Arial", 11))
        self.lbl_sys_jitter.grid(row=9, column=0, sticky="w", pady=2, padx=10)

        def update_clockvar():
            if not cv_win.winfo_exists(): return
            
            try:
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                
                # On demande aussi les variables système (offset, drift, jitter)
                cmd = get_ntpq_command()
                process = subprocess.Popen([cmd, "-c", "clockvar", "-c", "rv 0 offset,frequency,sys_jitter"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, startupinfo=startupinfo)
                stdout, stderr = process.communicate()
                
                raw_output = stdout
                
                txt_raw.config(state=tk.NORMAL)
                txt_raw.delete(1.0, tk.END)
                txt_raw.insert(tk.END, raw_output)
                txt_raw.config(state=tk.DISABLED)

                # Parsing
                data = {}
                # Extraction spécifique pour timecode qui est entre guillemets et contient des virgules
                match_tc = re.search(r'timecode="([^"]*)"', raw_output)
                if match_tc:
                    data["timecode"] = match_tc.group(1)

                # Extraction des autres valeurs simples (clé=valeur)
                for match in re.finditer(r'(\w+)=([^,\s"]+)', raw_output):
                    k, v = match.groups()
                    if k != "timecode":
                        data[k] = v

                # Extraction GPRMC
                timecode = data.get("timecode", "")
                if timecode.startswith("$GPRMC"):
                    gprmc_parts = timecode.split(",")
                    if len(gprmc_parts) >= 10:
                        raw_time = gprmc_parts[1]
                        if len(raw_time) >= 6:
                            self.lbl_cv_time.config(text=f"Heure UTC: {raw_time[0:2]}:{raw_time[2:4]}:{raw_time[4:6]}")
                        
                        status = gprmc_parts[2]
                        status_text = "OK (Active)" if status == "A" else "ALERTE (Void)"
                        self.lbl_cv_status.config(text=f"Statut GPS: {status_text}", fg="green" if status == "A" else "red")

                        lat, lat_dir = gprmc_parts[3], gprmc_parts[4]
                        lon, lon_dir = gprmc_parts[5], gprmc_parts[6]
                        
                        def fmt(val, d):
                            if not val: return ""
                            try:
                                dot = val.find('.')
                                if dot == -1: return val + d
                                deg = val[:dot-2]
                                mn = val[dot-2:]
                                return f"{deg}° {mn}' {d}"
                            except: return val + d
                        
                        self.lbl_cv_pos.config(text=f"Position: {fmt(lat, lat_dir)}  {fmt(lon, lon_dir)}")

                        raw_date = gprmc_parts[9]
                        if len(raw_date) == 6:
                            self.lbl_cv_date.config(text=f"Date: {raw_date[0:2]}/{raw_date[2:4]}/20{raw_date[4:6]}")

                self.lbl_cv_fudge.config(text=f"Fudge Time 2: {data.get('fudgetime2', '?')} ms")
                self.lbl_cv_stratum.config(text=f"Stratum: {data.get('stratum', '?')}")
                self.lbl_cv_refid.config(text=f"RefID: {data.get('refid', '?')}")
                self.lbl_cv_health.config(text=f"Santé: Poll={data.get('poll', '?')} / NoReply={data.get('noreply', '?')}")
                
                # Mise à jour des stats système
                offset_str = data.get('offset', '?')
                self.lbl_sys_offset.config(text=f"Offset: {offset_str} ms")
                self.lbl_sys_drift.config(text=f"Drift (Freq): {data.get('frequency', '?')} ppm")
                self.lbl_sys_jitter.config(text=f"Jitter: {data.get('sys_jitter', '?')} ms")
                
                jitter_str = data.get('sys_jitter', '0')

            except Exception as e:
                self.logger.error(f"Erreur clockvar: {e}")
            
            cv_win.after(1000, update_clockvar)

        update_clockvar()

    def open_iqt_window(self):
        """Ouvre la fenêtre de calcul de l'Indice de Qualité Temporelle (Spec 9)."""
        self.logger.info("Ouverture de la fenêtre IQT.")
        
        # Vérification et Arrêt NTP
        if is_service_active("NTP"):
            if messagebox.askyesno("Arrêt Requis", "Le calcul de l'IQT nécessite l'accès exclusif au port série.\nVoulez-vous arrêter le service NTP temporairement ?", parent=self.root):
                if not stop_service("NTP"):
                    messagebox.showerror("Erreur", "Impossible d'arrêter le service NTP.")
                    return
            else:
                return

        iqt_win = tk.Toplevel(self.root)
        iqt_win.title("Indice de Qualité Temporelle")
        iqt_win.geometry("450x600")
        iqt_win.transient(self.root)
        iqt_win.grab_set()
        
        tk.Label(iqt_win, text="Analyse de la stabilité du signal GPS", font=("Arial", 12, "bold")).pack(pady=10)
        
        # Jauge Principale
        gauge_main = IQTGauge(iqt_win, size=220)
        gauge_main.pack(pady=10)
        
        # Frame pour les 3 petits compteurs
        frame_details = tk.Frame(iqt_win)
        frame_details.pack(fill=tk.X, padx=10, pady=10)
        
        def create_small_gauge(parent, title, tooltip):
            frame = tk.Frame(parent)
            frame.pack(side=tk.LEFT, expand=True, padx=2) # On affiche le cadre conteneur
            
            lbl = tk.Label(frame, text=title, font=("Arial", 9, "bold"))
            lbl.pack()
            g = IQTGauge(frame, size=100)
            g.itemconfig(g.text_lbl, text="%") # Remplace "IQT" par "%"
            g.pack()
            ToolTip(g, tooltip)
            ToolTip(lbl, tooltip)
            return g

        g_snr = create_small_gauge(frame_details, "Signal (SNR)", "100 % = Signal excellent (> 40 dB-Hz).\n0 % = Signal très faible (< 20 dB-Hz).\nUn signal fort est moins sujet au bruit.")
        g_hdop = create_small_gauge(frame_details, "Géométrie (HDOP)", "100 % = Satellites bien répartis (HDOP ≤ 1.0).\n0 % = Satellites alignés ou trop proches (HDOP ≥ 4.0).\nUne mauvaise répartition diminue la précision.")
        g_qty = create_small_gauge(frame_details, "Satellites (Qté)", "100 % = Vue dégagée (8 satellites ou plus).\n0 % = Service minimum (3 satellites ou moins).\nPlus de satellites permet de rejeter les erreurs.")
        
        lbl_advice = tk.Label(iqt_win, text="Initialisation...", font=("Arial", 10, "italic"), fg="blue")
        lbl_advice.pack(pady=20)

        # Logique
        calculator = IQTCalculator()
        stop_event = threading.Event()
        
        def iqt_thread_func():
            port = self.config.get("serial_port", "COM1")
            baud = self.config.get("baud_rate", 9600)
            try:
                with serial.Serial(port, baud, timeout=1) as ser:
                    while not stop_event.is_set():
                        if not iqt_win.winfo_exists(): break
                        try:
                            line = ser.readline().decode('ascii', errors='ignore').strip()
                            if line:
                                calculator.process_line(line)
                        except Exception:
                            pass
            except Exception as e:
                self.logger.error(f"Erreur thread IQT: {e}")

        thread = threading.Thread(target=iqt_thread_func, daemon=True)
        thread.start()

        def update_ui():
            if not iqt_win.winfo_exists(): return
            
            iqt, s_snr, s_hdop, s_qty = calculator.get_iqt()
            
            gauge_main.set_value(iqt)
            g_snr.set_value(s_snr)
            g_hdop.set_value(s_hdop)
            g_qty.set_value(s_qty)
            
            if iqt < 50:
                lbl_advice.config(text="RISQUÉ : Signal instable ou insuffisant.", fg="red")
            elif iqt < 80:
                lbl_advice.config(text="ACCEPTABLE : Convient pour usage standard.", fg="orange")
            else:
                lbl_advice.config(text="EXCELLENT : Parfait pour Stratum 0.", fg="green")
            
            iqt_win.after(1000, update_ui)

        update_ui()

        def on_close():
            stop_event.set()
            iqt_win.destroy()
            # Redémarrage NTP automatique si on l'avait arrêté (on suppose oui car on est là)
            self.logger.info("Fermeture IQT -> Redémarrage NTP.")
            start_service("NTP")

        iqt_win.protocol("WM_DELETE_WINDOW", on_close)
