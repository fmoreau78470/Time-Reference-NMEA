# Mémoire du projet GPS NTP

## 📑 Table des matières
*   [1. Présentation du Projet](#1-présentation-du-projet)
*   [2. Environnement de Développement](#2-environnement-de-développement)
*   [3. Configuration NTP (Meinberg)](#3-configuration-ntp-meinberg)
*   [4. Monitoring & Logs](#4-monitoring--logs)
*   [5. Matériel & Protocoles](#5-matériel--protocoles)
*   [6. Dépannage](#6-dépannage)
*   [7. Firmware RP2040 (Stratum 0)](#7-firmware-rp2040-stratum-0)
*   [8. Indice de Qualité Temporelle](#8-indice-de-qualité-temporelle)
*   [9. Stratégie Multi-sites](#9-stratégie-multi-sites)
*   [10. Algorithme de Calibration](#10-algorithme-de-calibration)
*   [11. Algorithme de Santé NTP](#11-algorithme-de-santé-ntp)

---

## Session du 25/12/2025

### ❓ Question
Je veux créer un programme en python qui fera l'interface entre un GPS NMEA (sur un port série) et le programme NTP de Meinberg. Ce programme devra pouvoir modifier un fichier qui nécessite des droits administrateurs. Un certain nombre d'informations seront stockées dans un fichier de config json.

### ✅ Réponse
C'est un excellent projet. Interfacer un GPS matériel avec le service NTP de Meinberg sous Windows demande de la rigueur, notamment pour la gestion des privilèges et la précision temporelle.

---

## 1. Présentation du Projet

### Objectif
Créer une interface Python entre un GPS NMEA (port série) et le service NTP de Meinberg sous Windows.

Le programme doit :
*   Modifier des fichiers nécessitant des droits administrateur (`ntp.conf`).
*   Stocker sa configuration dans un fichier JSON.
*   Gérer le service NTP (Arrêt/Démarrage).
*   Fournir une interface graphique pour la calibration et le monitoring.

### Solution Technique
*   **Langage** : Python (puis C#).
*   **OS** : Windows.
*   **Fonctionnalités Clés** :
    *   Auto-élévation de privilèges (Admin).
    *   Lecture et visualisation du flux NMEA.
    *   Mise à jour intelligente de `ntp.conf` (comparaison avant écriture).
    *   Calibration automatique du "Fudge time" via analyse statistique.

---

## 2. Environnement de Développement

### Création de l'environnement virtuel (venv)
Pour isoler les dépendances :
1.  Création : `python -m venv venv`
2.  Activation : `.\venv\Scripts\activate`
3.  Installation des paquets : `pip install -r requirements.txt`

### Configuration VS Code
Pour utiliser automatiquement le `venv`, configurer `.vscode/settings.json` :
```json
{
    "python.defaultInterpreterPath": "${workspaceFolder}\\venv\\Scripts\\python.exe"
}
```
### ✅ Réponse
Pour forcer VS Code à utiliser le `venv` du projet :

### Gestion des Droits Administrateur
> **Configuration (.vscode/settings.json) :**
> ```json
> {
>     "python.defaultInterpreterPath": "${workspaceFolder}\\venv\\Scripts\\python.exe"
> }
> ```

#### Dans le code Python
Le programme ne peut pas "devenir" admin en cours de route. Il doit se relancer :
```python
params = " ".join([f'"{arg}"' for arg in sys.argv])
ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, params, None, 1)
```
### ❓ Question
Si le programme n'est pas exécuté en tant qu'admin, peut-on basculer en admin par simple programmation ?

#### Dans VS Code (Débogage)
Pour déboguer avec les droits admin sans fenêtres externes :
1.  Fermer VS Code.
2.  Clic droit sur l'icône VS Code > **"Exécuter en tant qu'administrateur"**.
3.  Le terminal intégré héritera des droits.
### ✅ Réponse
**Oui et Non.** La seule méthode (imposée par la sécurité Windows) est que le programme détecte qu'il n'est pas administrateur, puis se relance lui-même (crée une nouvelle instance) en demandant explicitement les droits.

*Note : Si VS Code s'ouvre vide, utiliser "Fichier > Ouvrir Récent" pour retrouver le projet.*
> **Code Python (avec gestion des espaces dans les chemins) :**
> ```python
> params = " ".join([f'"{arg}"' for arg in sys.argv])
> ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, params, None, 1)
> ```

---
### ❓ Question
Comment exécuter le code en mode administrateur directement depuis VS Code (Terminal ou Menu Exécuter) ?

## 3. Configuration NTP (Meinberg)
### ✅ Réponse
Le script possède une auto-élévation, mais cela ouvre une nouvelle fenêtre externe. Pour tout garder intégré dans l'interface de VS Code :

### Fichier de référence `ntp.conf`
```conf
**La solution : Lancer VS Code en Admin**
1. Fermer VS Code.
2. Clic droit sur l'icône VS Code > **"Exécuter en tant qu'administrateur"**.
3. Le terminal intégré aura alors automatiquement les droits.

> **Configuration Menu Exécuter (F5) :**
> J'ai créé le fichier `.vscode/launch.json` pour permettre le lancement via F5.
> Si VS Code n'est pas admin, le script se relancera dans une fenêtre externe et le débogueur se déconnectera.

### ❓ Question
Pourquoi j'ai le message "A code sample in this response was truncated..." ?

### ✅ Réponse
Ce n'est pas une erreur de votre code, mais une **limite de l'interface de discussion**.
Cela arrive quand la réponse de l'IA est trop longue (trop de lignes de code d'un coup).

> **Conseil :**
> Si cela arrive, demandez-moi de ne générer qu'une partie du code ou un seul fichier à la fois.

### ❓ Question
Si je redémarre VS Code en mode administrateur, est-ce que mes fichiers ouverts seront conservés ?

### ✅ Réponse
**Généralement oui**, mais pas garanti à 100% lors du changement de contexte (User vers Admin).

> **Procédure de sécurité :**
> 1. Sauvegardez tout avant de fermer.
> 2. Si VS Code s'ouvre vide en Admin, faites **Fichier > Ouvrir Récent** et choisissez le projet. L'état des fenêtres sera alors restauré.

### ℹ️ Infos complémentaires
Fichier ntp.conf de référence   

```conf
# ==========================================================
# CONFIGURATION NTP - MODE TERRAIN (PILOTE 20 NMEA)
# ==========================================================

# Emplacement du fichier de dérive (drift)
driftfile "C:\Program Files (x86)\NTP\etc\ntp.drift"

# --- 1. CONFIGURATION DES LOGS & STATISTIQUES ---
# Nécessaire pour NTP Time Server Monitor
statsdir "C:\Program Files (x86)\NTP\etc\"
statistics loopstats peerstats clockstats
filegen loopstats file loopstats type day enable
filegen peerstats file peerstats type day enable
filegen clockstats file clockstats type day enable

# --- 2. SERVEURS WEB (Sources secondaires) ---
# On les garde pour la comparaison, mais le GPS sera prioritaire
server xxxx iburst noselect
server xxxx iburst noselect
server xxxx iburst noselect
server xxxx iburst noselect

# Serveurs Web (Comparaison)
server 0.fr.pool.ntp.org iburst noselect
# ...
# --- 3. SOURCE GNSS (Pilote 20 NMEA) ---
# Adresse : 127.127.20.X (X = numéro du port COM, ici COM6)
# Mode 17 : 9600 baud + trame $GPRMC
server 127.127.20.x mode 17 minpoll 4 maxpoll 4 prefer

# Source GNSS (Pilote 20 NMEA)
# 127.127.20.X (X = Port COM)
server 127.127.20.6 mode 17 minpoll 4 maxpoll 4 prefer
# --- 4. PARAMÈTRES FUDGE ---
# stratum 1 : Indique que c'est une source de référence directe
# refid GPS  : Nom affiché dans ntpq
# time1      : Offset pour le signal PPS (si présent sur DCD)
# time2      : Offset pour la trame série NMEA (Latence USB/Série)
```

# Paramètres Fudge
# time1 : Offset PPS (si DCD actif)
# time2 : Offset NMEA (Latence Série)
fudge 127.127.20.6 stratum 1 refid GPS
fudge 127.127.20.6 time1 0.000
fudge 127.127.20.6 time2 0.1347
```
fudge 127.127.20.x stratum 1 refid GPS
fudge 127.127.20.x time1 0.000
fudge 127.127.20.x time2 xxxx  # Valeur par défaut à affiner selon l'écart observé

### Unités de temps (⚠️ Important)
Il est crucial de ne pas mélanger les unités selon le contexte :
### ℹ️ Format du fichier loopstats
Le fichier `loopstats` est généré par le démon NTP pour suivre la performance de l'algorithme de discipline de l'horloge système. Chaque ligne représente une mise à jour de l'horloge.

| Emplacement | Unité | Exemple | Équivalent |
| :--- | :--- | :--- | :--- |

Voici le décodage colonne par colonne pour votre extrait :

### 1. Date (MJD) : `61034`

* C'est la date au format **Modified Julian Day**.
* `61034` correspond au **25 décembre 2025**.

### 2. Secondes depuis minuit : `35543.021`

* Le temps écoulé depuis le début de la journée UTC.
* `35543` secondes correspondent environ à **09:52:23 UTC**.

### 3. Offset de l'horloge (s) : `0.002807808`

* C'est l'info la plus cruciale : l'écart mesuré entre votre PC et la source GPS.
* Ici, `0.0028` signifie que votre PC a un **retard de 2,8 millisecondes** sur le GPS.

### 4. Dérive de fréquence (ppm) : `3.168`

* Aussi appelée "Drift". C'est la correction appliquée pour compenser l'imprécision naturelle du quartz de votre carte mère.
* Votre quartz avance naturellement de **3,168 parties par million** (ppm). NTP ralentit logiciellement l'horloge de cette valeur pour rester à l'heure.

### 5. Constante de temps RMS (s) : `0.001226628`

* C'est l'erreur moyenne quadratique (RMS) de l'offset.
* Elle indique la stabilité de la mesure. Ici, l'erreur résiduelle est de **1,2 ms**.

### 6. Gigue (Jitter) : `0.019661`

* La variation de l'offset entre les mesures successives.
* Une valeur de **0.019** (soit 19 ms) est assez élevée pour un GPS, reflétant probablement les latences de traitement du port série sous Windows.

### 7. Constante de temps de la boucle : `4`

* C'est l'intervalle de mise à jour exprimé en puissance de 2.
* . Cela confirme que votre NTP interroge le GPS toutes les **16 secondes** (correspondant au `minpoll 4` de votre config).

### ℹ️ Décodage détaillé de votre commande `ntpq -c clockvar`
Voici le décodage détaillé de votre commande `ntpq -c clockvar`. Cette commande est essentielle car elle interroge directement les entrailles du **pilote 20 (NMEA)** pour voir comment il traite les données avant même qu'elles n'atteignent l'algorithme principal de NTP.

### 1. La trame reçue (`timecode`)

* **`$GPRMC,093703.00,A,...`** : C'est la dernière trame NMEA "Recommended Minimum" reçue par NTP.
* **Signification** : Il est **09h 37min 03s UTC**. Le `A` (Active) indique que le GPS a un "fix" valide. Si vous voyez un `V` (Void) à cet endroit, NTP rejettera la source.

### 2. Les réglages de compensation (`fudgetime`)

* **`fudgetime2=135.000`** : C'est la valeur que vous avez saisie dans votre `ntp.conf` (135 ms).
* **Observation importante** : Notez que NTP l'affiche ici en **millisecondes**, alors que dans le fichier `ntp.conf`, on l'écrit en **secondes** (`0.135`). Cela confirme que votre fichier de configuration est bien lu.
* **`fudgetime1` absent** : Puisque vous ne l'avez pas défini ou mis à 0, il n'apparaît pas ici (ou vaut 0 par défaut). Cela confirme que vous n'utilisez pas d'offset sur le signal PPS.

### 3. Santé de la liaison (`poll`, `noreply`, `badformat`)

* **`poll=15`** : C'est le compteur de secondes depuis la dernière interrogation réussie.
* **`noreply=1`** : C'est un indicateur. S'il augmente sans cesse, cela signifie que NTP n'arrive plus à lire le port COM.
* **`badformat=0` & `baddata=0**` : C'est parfait. Cela signifie que les trames envoyées par votre RP2040 sont parfaitement conformes au standard NMEA attendu par NTP.

### 4. Hiérarchie (`stratum`, `refid`)

* **`stratum=1`** : Votre pilote se déclare comme une horloge de référence de premier niveau (directement reliée à une source atomique/GNSS).
* **`refid=GPS`** : L'identifiant réseau de votre source.

### ℹ️ Format type de la commande ntpq -pn
     remote           refid      st t when poll reach   delay   offset  jitter
==============================================================================
*127.127.20.6    .GPS.            1 l    7   16  377    0.000   -0.513   3.816
 129.250.35.251  129.250.35.222   2 u   50   64  377    6.272   -7.536   0.729
 162.159.200.123 10.219.8.4       3 u   56   64  377    7.975   -8.985   1.648
 2a02:2178:1000: 195.176.26.215   2 u   52   64  377   18.000   -6.345   0.884
 31.58.102.164   195.145.119.188  2 u   46   64  377    7.305   -9.002   1.230

 ### ℹ️Algorithme de calcul du nouveau fudge
 Pour calculer votre nouveau **fudge** à partir du fichier `loopstats`, vous devez traiter l'**offset résiduel** (la 3ème colonne du fichier) et l'ajouter algébriquement à votre valeur actuelle.

Voici la procédure rigoureuse en trois étapes :

### 1. Extraire la tendance centrale de l'Offset

Pour calculer votre nouveau fudge à partir du fichier loopstats, vous devez traiter l'offset résiduel (la 3ème colonne du fichier) et l'ajouter algébriquement à votre valeur actuelle.

Voici la procédure rigoureuse en trois étapes :
1. Extraire la tendance centrale de l'Offset

Il ne faut pas prendre une valeur isolée, car l'offset fluctue à cause du jitter (gigue). Utilisez la Médiane ou la Moyenne Tronquée des offsets présents dans la colonne 3 (valeurs en secondes).

    Si la médiane est positive (ex: +0.000500) : Votre PC est en retard, il faut augmenter la compensation.

    Si la médiane est négative (ex: -0.001200) : Votre PC est en avance, il faut diminuer la compensation.

2. La Formule de Calcul

La formule mathématique pour mettre à jour votre time2 (ou time1) est la suivante :
Nouveau Fudge = Fudge Actuel + Offset Médian

Exemple concret basé sur vos données :

    Fudge actuel dans ntp.conf : 0.1357.

    Offset Médian calculé dans loopstats : +0.000494.

    Calcul : 0.1357+0.000494=0.136194.

3. Application dans ntp.conf

Une fois la valeur calculée, vous devez l'arrondir (généralement à 4 ou 5 décimales)

### ℹ️Unités des temps dans NTP
Il est très fréquent de s'y perdre, car selon l'endroit où l'on regarde (le fichier de configuration, les logs ou la console), NTP change d'unité. Voici la règle d'or à retenir pour ne plus faire d'erreur de calcul :

### 1. Dans le fichier `ntp.conf` (Le Fudge)

L'unité est la **seconde**.

* Quand vous écrivez `fudge ... time2 0.1362`, vous indiquez une compensation de **136,2 millisecondes**.
* C'est une valeur décimale de la seconde.

### 2. Dans le fichier `loopstats` (L'Offset)

L'unité est la **seconde** (en notation scientifique ou décimale).

* La troisième colonne (l'offset) est exprimée en secondes.
* Une valeur comme `0.000494` représente **0,494 milliseconde**.
* Une valeur comme `-0.002078` représente **-2,078 millisecondes**.

### 3. Dans la commande `ntpq -p` (Le Monitoring)

L'unité est la **milliseconde**.

* C'est le piège principal ! Si vous lisez `offset 10.040` dans votre terminal, cela signifie **10,040 millisecondes** (soit `0.010040` seconde).

---

### Tableau Récapitulatif

| Emplacement | Unité | Exemple de valeur | Équivalent réel |
| --- | --- | --- | --- |
| **`ntp.conf`** | **Seconde** | `0.135` | 135 ms |
| **`loopstats`** | **Seconde** | `0.0015` | 1,5 ms |
| **`ntpq -p`** | **Milliseconde** | `1.500` | 1,5 ms |
| **`clockvar`** | **Milliseconde** | `135.000` | 135 ms |

### Calcul du Fudge (Calibration)
Pour calibrer le `time2` à partir des logs `loopstats` :
### Comment faire votre calcul sans erreur ?

1.  **Extraire la tendance** : Calculer la Médiane des offsets (3ème colonne de `loopstats`).
2.  **Formule** : `Nouveau Fudge = Fudge Actuel + Offset Médian`
    *   *Exemple* : `0.1357 (Actuel) + 0.000494 (Offset) = 0.136194`
3.  **Application** : Mettre à jour `ntp.conf` avec la nouvelle valeur (en secondes).
Comme le `loopstats` et le `ntp.conf` utilisent tous les deux la **seconde**, vous pouvez faire votre addition directement sans conversion :

---
**Astuce :** Si vous travaillez avec des millisecondes pour plus de confort mental, n'oubliez pas de diviser par **1000** avant de réinjecter le résultat dans votre fichier `ntp.conf`.

## 4. Monitoring & Logs
Voulez-vous que je reprenne votre dernier calcul de médiane pour confirmer la valeur exacte à inscrire dans votre `ntp.conf` ?

### Fichier `loopstats`
Format : `MJD | Secondes | Offset | Drift | RMS | Jitter | Poll`
### ℹ️Décodage de la trame $GPRMC
La trame **$GPRMC** (Recommended Minimum Specific GNSS Data) est la plus importante pour le protocole NTP car elle contient toutes les données de temps et de position nécessaires à la synchronisation.

Exemple décodé (`61034 35543.021 0.002807808 3.168 0.001226628 0.019661 4`) :
*   **Date** : 25/12/2025 (MJD 61034).
*   **Heure** : ~09:52:23 UTC.
*   **Offset** : +2.8 ms (Retard PC sur GPS).
*   **Drift** : 3.168 ppm (Correction quartz).
*   **Jitter** : 19 ms (Gigue élevée, typique NMEA sans PPS).
Voici le décodage détaillé de votre trame spécifique :
`$GPRMC,095137.00,A,4842.08248,N,00203.99425,E,0.272,,251225,,,A*73`

### Commande `ntpq -p` (Peers)
Affiche l'état des sources de temps.
*   **remote** : Adresse IP ou Driver (127.127.20.x).
*   **refid** : `.GPS.` (Source locale).
*   **st** : Stratum (1 pour GPS).
*   **reach** : Registre octal (377 = 100% de succès sur les 8 derniers essais).
*   **offset** : Décalage en **millisecondes**.
1. **`095137.00` (Heure UTC)** : Il est **09:51:37** précise. Le `.00` indique que la mesure est synchronisée sur le début de la seconde.
2. **`A` (Statut)** : État de la réception. **A = OK** (Active), **V = Alerte** (Void). Comme vous avez un `A`, le pilote NTP accepte la donnée.
3. **`4842.08248,N` (Latitude)** : 48° 42.08248 minutes Nord.
4. **`00203.99425,E` (Longitude)** : 002° 03.99425 minutes Est.
5. **`0.272` (Vitesse)** : Vitesse au sol en nœuds (ici très faible, environ 0.5 km/h, ce qui est normal pour un récepteur fixe à cause du "bruit" GNSS).
6. **`251225` (Date)** : Nous sommes le **25 décembre 2025**.
7. **`A` (Mode Indicateur)** : `A` pour Autonome. Indique que le récepteur calcule sa position seul sans correction différentielle externe.
8. **`*73` (Checksum)** : Somme de contrôle pour vérifier qu'aucun caractère n'a été corrompu durant le transfert série.

### Commande `ntpq -c clockvar`
Affiche les détails internes du driver NMEA.
*   **timecode** : La dernière trame NMEA reçue brute.
*   **fudgetime2** : Valeur du fudge appliquée (en **ms**).
*   **poll/noreply** : Compteurs de santé de la communication série.
*   **badformat** : Compteur d'erreurs de syntaxe NMEA.
### Informations système associées (clockvar) :

---
Dans votre retour `ntpq`, on note également des informations cruciales pour votre serveur de temps :

## 5. Matériel & Protocoles
* **`fudgetime2=135.700`** : Vous avez appliqué la correction de **135.7 ms** que nous avons calculée précédemment pour compenser la latence du port série.
* **`stratum=1`** : Votre serveur est bien reconnu comme une source de référence primaire (Strate 1) grâce à cette trame.
* **`refid=GPS`** : C'est l'étiquette d'identification que vous avez définie dans le `fudge` de votre fichier `ntp.conf`.

### Décodage NMEA (`$GPRMC`)
Trame type : `$GPRMC,095137.00,A,4842.08248,N,00203.99425,E,0.272,,251225,,,A*73`
**Analyse de performance** : Avec un `poll=11`, le système interroge le GPS toutes les 16 secondes environ (2^4). Votre configuration est saine car `badformat` et `baddata` sont à **0**, prouvant que la communication avec votre RP2040 est parfaite.

*   **Heure** : 09:51:37 UTC.
*   **Status** : A (Active/OK). Si V (Void), le GPS n'est pas fix.
*   **Position** : Latitude/Longitude.
*   **Date** : 25/12/2025.
### ❓ Question
Comment charger le code .ino (C++) dans le RP2040 ?

### RP2040 & Synchronisation
Le RP2040 est utilisé pour interfacer le module GPS.
*   **Problème** : MicroPython manque de déterminisme (Garbage Collector) pour une synchronisation précise.
*   **Solution** : Utiliser un firmware C++ (Arduino) pour aligner l'envoi de la trame NMEA sur le signal PPS.
### ✅ Réponse
Pour charger le firmware C++, il faut quitter l'environnement MicroPython (Thonny) et utiliser l'**IDE Arduino**.

#### Procédure de Flash (C++)
1.  **IDE** : Installer Arduino IDE v2.x.
2.  **Core** : Installer "Raspberry Pi Pico/RP2040 by Earle F. Philhower".
3.  **Lib** : Installer "Adafruit TinyUSB".
4.  **Config** :
    *   Board : Raspberry Pi Pico.
    *   **USB Stack : Adafruit TinyUSB**.
    *   CPU : 133 MHz.
5.  **Flash** : Brancher le RP2040 avec BOOTSEL enfoncé, puis téléverser le fichier `.ino`.
**1. Prérequis Logiciels**
*   Installer l'**IDE Arduino** (v2.x).
*   Ajouter le dépôt de cartes **Earle Philhower** dans *Fichier > Préférences* (champ situé tout en bas de la fenêtre) :
    `https://github.com/earlephilhower/arduino-pico/releases/download/global/package_rp2040_index.json`
*   Installer le core **"Raspberry Pi Pico/RP2040"** via le *Gestionnaire de cartes*.
*   ⚠️ **Ne PAS installer** la bibliothèque "Adafruit TinyUSB" via le gestionnaire (elle est déjà incluse dans le core). Si vous l'avez installée, supprimez-la.

**2. Configuration de l'IDE**
*   **Carte** : **Waveshare RP2040-Zero** (Correspond à votre modèle Amazon).
*   **USB Stack** : **"Adafruit TinyUSB"**.
*   **CPU Speed** : 133 MHz.

**3. Procédure de Flash**
1.  Débranchez le RP2040.
2.  Maintenez le bouton **BOOTSEL** enfoncé et branchez l'USB (le disque `RPI-RP2` apparaît).
3.  Dans l'IDE Arduino, copiez le code `.ino`.
4.  Cliquez sur **Téléverser** (Flèche droite).

*Note : Cette opération écrase MicroPython et tous les fichiers présents sur le RP2040.*

---

## 6. Dépannage

### Double Port COM
Le firmware utilise un objet `Adafruit_USBD_CDC` dédié.
Cela fait apparaître **deux ports COM** sur Windows :
*   L'un est le port de debug standard du RP2040.
*   L'autre est le port **GPS/PPS** (celui qui nous intéresse). Il faut identifier le nouveau numéro apparu après le flash.

### Erreur `Fatal Python error` (initfsencoding)
**Symptôme :** `Fatal Python error: initfsencoding` ou `ModuleNotFoundError: No module named 'encodings'` lors de la compilation.
**Cause :** Conflit de variables d'environnement. Le Python interne d'Arduino est perturbé par une variable `PYTHONHOME` ou `PYTHONPATH` définie dans Windows.
**Solution :**
1.  Dans Windows, chercher "Modifier les variables d'environnement système".
2.  Supprimer les variables **`PYTHONHOME`** et **`PYTHONPATH`** (si elles existent).
3.  Supprimer le dossier `%LOCALAPPDATA%\Arduino15\packages\rp2040`.
4.  Relancer l'IDE et réinstaller le cœur RP2040.

## 7. Firmware RP2040 (Stratum 0)

### Description
Ce firmware transforme le RP2040 en une horloge de référence (Stratum 0).
Il lit le flux NMEA du GPS et synchronise l'envoi de la trame `$GPRMC` avec le signal PPS (Pulse Per Second).

**Problème résolu (Le piège du "Next PPS") :**
Le module GPS envoie sa trame NMEA (ex: "Il est 12:00:00") environ 400ms après le top PPS. Si le firmware attend le *prochain* PPS pour l'envoyer (pour avoir un jitter nul), ce prochain PPS correspondra physiquement à 12:00:01. Le PC recevra donc "Il est 12:00:00" à l'instant T=12:00:01, soit un retard d'une seconde.

**Solution (Time Adder) :**
Le firmware analyse la trame reçue, ajoute mathématiquement **1 seconde** à l'heure (et gère le changement de date si nécessaire), recalcule le checksum, et stocke cette trame "futuriste". Au top PPS suivant, il envoie cette trame qui correspond désormais exactement à la réalité.

### Environnement de Compilation
*   **IDE** : Arduino IDE v2.x
*   **URL de gestionnaire de cartes** : `https://github.com/earlephilhower/arduino-pico/releases/download/global/package_rp2040_index.json`
*   **Cœur (Core)** : "Raspberry Pi Pico/RP2040" par Earle F. Philhower.
*   **Bibliothèque** : `Adafruit TinyUSB` (Incluse dans le cœur).

### Configuration de la Carte (IDE Arduino)
*   **Board** : Waveshare RP2040-Zero (ou Generic RP2040).
*   **USB Stack** : **"Adafruit TinyUSB"** (Indispensable).
*   **CPU Speed** : 133 MHz.

### Câblage
*   **GPS TX** &rarr; **RP2040 RX** (GP1 / Pin 2)
*   **GPS RX** &rarr; **RP2040 TX** (GP0 / Pin 1)
*   **GPS PPS** &rarr; **RP2040 GP2** (Pin 3)
*   **GND** &rarr; **GND**

### Code Source (stratum0.ino)
```cpp
/*
* Ce firmware transforme le RP2040 en une horloge de référence (Stratum 0).
* Il lit le flux NMEA du GPS, ajoute 1 seconde pour compenser l'attente, et synchronise l'envoi avec le signal PPS suivant.
 */

#include <Arduino.h>
#include <Adafruit_TinyUSB.h>

// --- CONFIGURATION ---
const int PPS_PIN = 2;       // Le fil PPS du GPS va sur GP2 (Pin 3)

// Objet USB CDC standard
Adafruit_USBD_CDC GpsUSB;

// --- VARIABLES GLOBALES ---
volatile bool pps_detected = false;
String inputBuffer = "";
String lastRmc = "";

// --- INTERRUPTIONS ---
void on_pps_signal() {
    pps_detected = true;
}

// --- LOGIQUE TIME ADDER (+1s) ---
String adjustRmc(String rmc) {
    // Trouve les virgules pour extraire Heure (1) et Date (9)
    // $GPRMC,HHMMSS.ss,A,lat,N,lon,E,spd,cog,DDMMYY,...
    int commas[10];
    int p = 0;
    for(int i=0; i<10; i++) {
        p = rmc.indexOf(',', p);
        if(p == -1) return rmc; 
        commas[i] = p;
        p++;
    }
    
    String sTime = rmc.substring(commas[0]+1, commas[1]);
    String sDate = rmc.substring(commas[8]+1, commas[9]);
    if (sTime.length() < 6 || sDate.length() < 6) return rmc;

    long t = sTime.substring(0, 6).toInt();
    long d = sDate.toInt();
    int h=t/10000, m=(t%10000)/100, s=t%100;
    int D=d/10000, M=(d%10000)/100, Y=d%100;

    // Ajout 1 seconde
    if (++s >= 60) { s=0; m++; }
    if (m >= 60) { m=0; h++; }
    if (h >= 24) { 
        h=0; D++;
        int dim = 31;
        if(M==4||M==6||M==9||M==11) dim=30;
        if(M==2) dim = (Y%4==0) ? 29 : 28;
        if(D > dim) { D=1; M++; }
        if(M > 12) { M=1; Y++; }
    }

    char buf[10];
    sprintf(buf, "%02d%02d%02d", h, m, s);
    String newTime = String(buf) + sTime.substring(6); // Garde .ss
    sprintf(buf, "%02d%02d%02d", D, M, Y);
    String newDate = String(buf);

    // Reconstruction corps (entre $ et *)
    String body = "GPRMC," + newTime + rmc.substring(commas[1], commas[8]+1) + newDate + rmc.substring(commas[9], rmc.indexOf('*'));
    
    // Checksum
    int sum = 0;
    for(unsigned int i=0; i<body.length(); i++) sum ^= body[i];
    String hex = String(sum, HEX);
    hex.toUpperCase();
    if (hex.length() < 2) hex = "0" + hex;
    
    return "$" + body + "*" + hex + "\r\n";
}

void setup() {
    // Initialisation du port USB
    GpsUSB.begin(115200);
    
    // Initialisation du GPS sur le port série matériel (UART0)
    Serial1.begin(9600);
    
    // Init PPS
    pinMode(PPS_PIN, INPUT);
    attachInterrupt(digitalPinToInterrupt(PPS_PIN), on_pps_signal, RISING);

    // Message de démarrage (comme dans main.py)
    // Attente optionnelle du port série
    if (GpsUSB) GpsUSB.println("RP2040 Stratum 0 : Mode PPS Aligned (+1s fix)");
}

void loop() {
    // 1. Lecture et Buffering du GPS
    while (Serial1.available()) {
        char c = Serial1.read();
        inputBuffer += c;
        if (c == '\n') {
            // Fin de ligne détectée
            if (inputBuffer.startsWith("$GPRMC")) {
                // On stocke la trame RMC pour la synchroniser avec le PPS
                // On ajoute 1s pour compenser le fait qu'elle sera envoyée au PROCHAIN PPS
                lastRmc = adjustRmc(inputBuffer);
            } else {
                // Les autres trames passent tout de suite
                GpsUSB.print(inputBuffer);
            }
            inputBuffer = "";
        }
    }

    // 2. Gestion du PPS (Synchronisation)
    if (pps_detected) {
        pps_detected = false;
        
        // Envoi de la trame RMC stockée (si disponible)
        if (lastRmc.length() > 0) {
            GpsUSB.print(lastRmc);
            lastRmc = "";
        }
    }
}
```

## 8. Indice de Qualité Temporelle

### Description
Pour transformer un simple récepteur GPS en une source de temps fiable (type serveur NTP/PTP), il ne faut pas se contenter de savoir "si ça capte", mais "si le signal est assez stable pour ne pas dériver".
Voici une proposition d'algorithme pour calculer un **Indice de Qualité Temporelle (IQT)**, noté de 0 à 100.

### 1. Les critères de pondération
Un bon indice doit combiner la force brute du signal (SNR/CN0) et la précision géométrique (HDOP).

| Critère | Source NMEA | Seuil Idéal | Poids dans l'indice |
| --- | --- | --- | --- |
| **Moyenne des CN0** | `$GPGSV` |  dB-Hz | 50% |
| **Nombre de Satellites** | `$GPGGA` |  | 20% |
| **Précision (HDOP)** | `$GPGSA` |  | 30% |

### 2. L'algorithme (Pseudo-code)
Cet algorithme traite les trames au fur et à mesure et calcule un score.

```python
# Initialisation des variables
sat_count = 0
hdop = 99.9
snr_list = []

FONCTION Calculer_IQT(trames_nmea):
    POUR CHAQUE trame DANS trames_nmea:
        
        SI trame commence par "$GPGGA":
            sat_count = Valeur du champ 7 (Nb satellites)
            
        SI trame commence par "$GPGSA":
            hdop = Valeur du champ 16 (Précision horizontale)
            
        SI trame commence par "$GPGSV":
            # On extrait tous les SNR (4ème valeur de chaque bloc de 4)
            snr_list.ajouter(Extraire_SNR_Valides(trame))

    # --- CALCUL DES SCORES ÉLÉMENTAIRES ---
    
    # 1. Score SNR (Moyenne des 4 meilleurs signaux)
    # On prend les 4 meilleurs car le GPS utilise souvent les plus forts pour le temps
    top_snr = trier_decroissant(snr_list)[0:4]
    moyenne_snr = moyenne(top_snr)
    # On normalise : 40 dB-Hz = 100%, 20 dB-Hz = 0%
    score_snr = Clamper((moyenne_snr - 20) * 5, 0, 100)

    # 2. Score Géométrie (HDOP)
    # 1.0 ou moins = 100%, 4.0 ou plus = 0%
    score_hdop = Clamper((4.0 - hdop) * 33.3, 0, 100)

    # 3. Score Quantité
    # 8 satellites ou plus = 100%, 3 ou moins = 0%
    score_qty = Clamper((sat_count - 3) * 20, 0, 100)

    # --- INDICE FINAL PONDÉRÉ ---
    IQT = (score_snr * 0.5) + (score_hdop * 0.3) + (score_qty * 0.2)
    
    RETOURNER IQT

## 3. Interprétation du résultat pour votre Horloge

* **IQT > 80 : Excellente.** L'horloge est stable à quelques dizaines de nanosecondes. Le PPS est parfaitement fiable.
* **IQT 50 - 80 : Acceptable.** Convient pour de la synchronisation informatique classique (NTP).
* **IQT < 50 : Risqué.** Le "jitter" (gigotage) du signal PPS va augmenter. L'horloge risque de dériver si le récepteur perd le fix quelques secondes.

### Conseil pour l'implémentation :

Pour éviter que l'indice ne saute sans arrêt (car le SNR fluctue d'une seconde à l'autre), je vous conseille d'appliquer une **moyenne glissante** sur les 10 dernières secondes de calcul. Cela lissera l'indice et évitera de considérer l'horloge comme "mauvaise" à cause d'un simple nuage ou d'un oiseau passant devant l'antenne.


## 9. Stratégie Multi-sites
Pour qu'un fichier `ntp.conf` soit efficace à la fois **sur le terrain (GPS seul)** et **à la maison (Internet disponible)**, il faut jouer sur les priorités et les stratégies de repli (fallback) de NTP.

D'après le fichier que vous avez fourni, vous utilisez déjà plusieurs sources, mais nous pouvons optimiser la configuration pour rendre la transition totalement transparente.

Voici comment adapter votre `ntp.conf` pour les deux situations :

### 1. Utiliser le mot-clé `prefer` pour le GPS

NTP doit savoir que le GPS est la référence absolue lorsqu'il est présent.
Dans votre fichier, modifiez la ligne du pilote NMEA ainsi :

```bash
server 127.127.20.6 mode 16 minpoll 4 maxpoll 4 prefer

```

* **Pourquoi ?** Le mot-clé `prefer` indique à NTP que si cette source est disponible et valide, elle doit être choisie en priorité, même si des serveurs Internet (Strate 2) semblent avoir un jitter plus faible.

### 2. Ajouter des serveurs de pool Internet

Gardez vos serveurs distants (ou ajoutez le pool générique). Si le GPS est débranché, NTP marquera la source `127.127.20.6` comme injoignable après quelques minutes et basculera automatiquement sur les serveurs Internet.

```bash
pool pool.ntp.org iburst

```

### 3. La stratégie "Orphan Mode" (Mode Orphelin)

C'est l'élément clé pour votre usage terrain. Si vous n'avez **ni GPS, ni Internet**, vous voulez que votre PC continue de servir du temps aux autres équipements (si vous en avez) ou qu'il ne s'arrête pas de discipliner son horloge interne.
Ajoutez cette ligne :

```bash
tos orphan 5

```

Cela permet au serveur de se considérer comme une source valide de strate 5 même s'il n'a plus aucune source externe.

### 4. Gérer l'absence du GPS (Le "Panic Threshold")

Sous Windows, si vous débranchez le GPS, le port COM disparaît ou ne répond plus. NTP est robuste : il va simplement "déclarer" le GPS comme `REJECT` ou `INSANE` dans le `ntpq -p`. Dès que vous rebranchez le GPS à la maison ou sur le terrain, NTP le détectera à nouveau (cela peut prendre quelques cycles de poll, soit environ 1 à 2 minutes).

### Résumé de la configuration hybride idéale :

Voici à quoi devrait ressembler la section des sources de votre `ntp.conf` :

```bash
# --- SOURCE GPS (Terrain) ---
# On ajoute 'prefer' pour qu'il soit la priorité absolue
server 127.127.20.6 mode 16 minpoll 4 maxpoll 4 prefer
fudge 127.127.20.6 time2 0.1362 refid GPS

# --- SOURCES INTERNET (Maison) ---
# Elles ne seront utilisées que si le GPS est débranché ou invalide
pool pool.ntp.org iburst

# --- SÉCURITÉ / STABILITÉ ---
# Permet de garder une strate cohérente si tout est déconnecté
tos orphan 5
# Autorise NTP à corriger l'heure même si l'écart est grand au démarrage
tinker panic 0

```

### Comment NTP va se comporter :

1. **Sur le terrain (GPS branché, pas d'Internet) :** NTP voit le GPS, il est marqué `prefer`. Il se synchronise dessus. Les serveurs `pool` échouent (pas de réseau), ce n'est pas grave.
2. **À la maison (GPS branché + Internet) :** NTP voit tout le monde. Grâce à `prefer`, il reste calé sur le GPS. Internet sert de "témoin" pour vérifier que le GPS ne dit pas n'importe quoi.
3. **À la maison (GPS débranché + Internet) :** NTP voit que le port COM ne répond plus. Il bascule sur les serveurs du `pool`. Votre PC devient un serveur de Strate 2 ou 3.
4. **Partout (Rien de branché) :** Grâce à `tos orphan 5` et au fichier `ntp.drift`, le PC continue de faire défiler le temps en utilisant la dernière dérive de quartz connue, restant ainsi beaucoup plus précis qu'une horloge Windows standard.

**Conseil Windows :** Assurez-vous que le port COM assigné à votre RP2040 est toujours le même (ex: COM6). Si vous le branchez sur un autre port USB et qu'il devient COM7, NTP ne le trouvera plus car l'adresse `127.127.20.6` pointe spécifiquement vers le **COM6**.

## 10. Algorithme de Calibration d'une Source de Temps (GPS/NMEA)
Voici une proposition d'algorithme logique pour résoudre un écart important entre une source locale (GPS) et des références réseau (Internet), sans entrer dans le codage, mais en suivant la méthodologie de "discipline d'horloge" propre à NTP.

---

### Algorithme de Calibration d'une Source de Temps (GPS/NMEA)

#### 1. Phase de Stabilisation (Pré-requis)

* **Désactiver** tout service tiers (W32Time) pour éviter les corrections erratiques.
* **Activer** les serveurs de référence Internet en mode observation (pour avoir une "vérité" de comparaison).
* Laisser tourner le système **10 minutes** pour que le "jitter" (variation) se stabilise.

#### 2. Mesure du Décalage Brut (Offset)

* Relever la valeur de l'**Offset** du GPS par rapport au système (via `ntpq -p`).
* Relever la valeur de l'**Offset moyen** des serveurs Internet.
* **Calcul de l'écart réel ()** :
* 
* *Note : Si Internet est à +2ms et le GPS à -865ms, l'écart à compenser est d'environ 867ms.*



#### 3. Analyse de la Cohérence

* **SI** le Jitter du GPS est très faible (ex: < 5ms) **ET** l'Offset est constant :
* L'écart est purement **matériel** (latence USB/Série). On passe à la correction.


* **SI** le Jitter est énorme ou l'Offset change de sens sans arrêt :
* La liaison série est instable ou le PC est surchargé. **STOP** : Régler le problème matériel avant de calibrer.



#### 4. Correction par Approximation Successive (Boucle de réglage)

* **Étape A (Correction de masse)** :
* Modifier la valeur de compensation (`time2` / Fudge) en ajoutant la valeur de l'écart  calculé à l'étape 2.
* Redémarrer le service pour réinitialiser les filtres.


* **Étape B (Observation)** :
* Attendre que le "Reach" (disponibilité) atteigne son maximum (377).
* Vérifier le nouveau symbole de la source :
* Si `*` ou `+` : La source est acceptée.
* Si `x` (false ticker) : L'écart est encore trop grand pour l'algorithme de Marzullo.




* **Étape C (Affinement)** :
* Si l'offset résiduel est positif (ex: +10ms) : Diminuer légèrement le `time2`.
* Si l'offset résiduel est négatif (ex: -10ms) : Augmenter légèrement le `time2`.



#### 5. Validation Finale

* L'algorithme est terminé quand :
1. Le GPS est sélectionné comme source principale (`*`).
2. L'Offset du GPS est inférieur à la précision de la liaison (généralement < 10ms pour du NMEA seul).
3. Les serveurs Internet sont marqués comme "Candidats" (`+`), prouvant qu'ils sont d'accord avec le GPS.



#### 6. Sauvegarde et Archivage

* Inscrire la valeur finale de `time2` dans la configuration permanente.
* Noter la valeur de la dérive (`drift`) après 24h pour garantir la précision en mode "terrain" (hors ligne).

---

### Résumé de la logique de calcul (La "Règle d'or") :

> **Nouveau Fudge = Fudge Actuel + Offset constaté**
> *(En faisant attention aux signes : ajouter un offset négatif revient à diminuer le fudge).*



### Nouvel algoithme de calcul de l'indice de santé
L'approche actuelle repose sur une logique de "punition/récompense" incrémentale qui crée naturellement une inertie (hystérésis). Pour obtenir un algorithme plus réactif sans sacrifier la stabilité, il faut passer d'un système de score cumulatif à un système d'**analyse d'état pondéré**.

Voici une proposition pour optimiser votre surveillance NTP avec Meinberg.

---

## 1. Critères supplémentaires essentiels

Pour que l'algorithme soit fiable, les compteurs d'erreurs ne suffisent pas. Vous devez interroger les variables de synchronisation réelle :

* **Stratum** : Si le stratum passe à 16, le serveur n'est plus synchronisé.
* **Root Dispersion** : Indique l'erreur maximale accumulée. Si elle dépasse 1s, la qualité est médiocre.
* **Offset** : L'écart réel en millisecondes entre votre horloge et le GPS.
* **Precision** : Pour détecter une dégradation matérielle.
* **RefID** : Indique si la source est bien `.GPS.` ou `.PPS.`. S'il passe en `LOCAL` ou `STEP`, la santé chute.

---

## 2. Nouvel Algorithme : Le "Score de Confiance Instantané"

Au lieu de faire varier un score de 5 en 5, nous allons classer l'état selon une **matrice de criticité**. Le score est recalculé à chaque échantillon (10s) mais lissé par une moyenne mobile courte pour éviter les micro-coupures.

### Logique de calcul (Score )

Le score part de 100 et on applique le **malus le plus sévère** détecté lors de l'échantillonnage, plutôt que de les additionner indéfiniment.

| Paramètre | Condition de Dégradation | Malus de Santé | Criticité |
| --- | --- | --- | --- |
| **Timecode** | Statique (identique au précédent) | -100 (Score = 0) | Critique (Mort) |
| **Stratum** |  (en config GPS normale) | -60 | Majeur |
| **Offset** |  ms | -40 | Modéré |
| **Badformat** | Delta  | -30 | Technique |
| **Noreply** | Delta  | -20 | Réseau/Antenne |
| **Reach** |  (octal) | -10 par bit manquant | Stabilité |

### Algorithme de "Guérison Rapide"

Pour résoudre votre problème de lenteur à revenir à 100% :

1. **Vérification de conformité** : Si (Delta `noreply` == 0) ET (Delta `badformat` == 0) ET (Stratum < 3).
2. **Saut de confiance** : Si ces conditions sont vraies pendant 2 cycles consécutifs (20s), le score remonte immédiatement à **100%**, au lieu de remonter péniblement de 5 en 5.

---

## 3. Implémentation suggérée (Logique Pseudo-code)

```python
# À chaque intervalle de 10s
def calculate_health(current_data, previous_data):
    if current_data.timecode == previous_data.timecode:
        return 0, "ROUGE" # GPS Figé
    
    # Initialisation des malus pour ce cycle
    malus = 0
    
    # 1. Analyse des erreurs protocolaires (Réactivité immédiate)
    if current_data.badformat > previous_data.badformat: malus += 30
    if current_data.noreply > previous_data.noreply: malus += 20
    
    # 2. Analyse de la qualité de synchro
    if current_data.stratum >= 16: malus += 80
    if abs(current_data.offset) > 128: malus += 40
    
    current_score = max(0, 100 - malus)
    
    # 3. Système de rétablissement rapide (Fast recovery)
    if malus == 0 and current_data.stratum < 3:
        # Si tout est vert, on force le retour au top
        return 100, "VERT"
        
    return current_score, get_color(current_score)

```

---

## 4. Pourquoi cette approche est meilleure ?

1. **Réactivité** : Le passage de "Orange" à "Vert" est quasi instantané (20s max) si les indicateurs techniques sont bons.
2. **Fiabilité** : En surveillant l'**Offset** et le **Stratum**, vous ne surveillez plus seulement si le service "parle" (`noreply`), mais s'il dit la vérité (précision horaire).
3. **Transparence** : L'utilisation du `reach` (registre à décalage de 8 bits dans NTP) permet de voir si les 8 derniers échanges ont réussi. C'est l'indicateur natif le plus puissant de NTP.


Algorithme sous forme de mermaid
graph TD
    A[Début du cycle : échantillonnage 10s] --> B{Timecode inchangé?}
    
    B -- Oui --> C[Score: 0% <br/>ÉTAT: ROUGE - MORT]
    B -- Non --> D{RefID == .INIT. ou <br/>Reach < 377 & Offset == 0?}

    D -- Oui --> E[Score: 20% <br/>ÉTAT: BLEU - Initialisation]
    D -- Non --> F{Source Active?}

    F -- Internet --> G{GPS Reach == 377?}
    G -- Oui --> H[Score: 15% <br/>ÉTAT: ACTION - FUDGE REQUIS]
    G -- Non --> I[Score: 30% <br/>ÉTAT: ORANGE - Secours Internet]

    F -- GPS --> J{Offset_Abs >= Seuil_User?}
    
    J -- Oui --> K[Score: 5% <br/>ÉTAT: ROUGE - Hors Tolérance]
    J -- Non --> L[Calcul Malus]

    L --> M{Offset > Seuil * 0.7?}
    M -- Oui --> N[Malus + 40]
    M -- Non --> O[Malus + 0]

    O --> P{Jitter > Seuil / 4?}
    P -- Oui --> Q[Malus + 20]
    P -- Non --> R[Malus + 0]

    R --> S{Reach != 377?}
    S -- Oui --> T[Malus + 20]
    S -- Non --> U[Malus + 0]

    U --> V[Score Final = 100 - Somme Malus]
    
    V --> W{Score > 90 & <br/>Historique >= 100?}
    W -- Oui --> X{Abs_Moyenne_Offset > Seuil * 0.25?}
    X -- Oui --> Y[Score: 95% <br/>ÉTAT: VERT - SUGGESTION CALIBRATION]
    X -- Non --> Z[Score: 100% <br/>ÉTAT: VERT - NOMINAL]
    W -- Non --> Z

    %% Style des couleurs
    style C fill:#ff4d4d,stroke:#333,stroke-width:2px
    style E fill:#4da6ff,stroke:#333,stroke-width:2px
    style H fill:#ff9933,stroke:#333,stroke-width:4px
    style K fill:#ff4d4d,stroke:#333,stroke-width:2px
    style Y fill:#99ff99,stroke:#333,stroke-width:2px,stroke-dasharray: 5 5
    style Z fill:#00cc44,stroke:#333,stroke-width:2px




    https://www.ntppool.org/fr/