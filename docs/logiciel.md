# Manuel Logiciel

Ce guide détaille l'installation, la configuration et l'utilisation de l'application **Time Reference NMEA**.

> **⚠️ Prérequis Absolu**
>
> **Ce logiciel ne fonctionne PAS avec le service de temps Windows standard (W32Time).**
>
> Pour atteindre une précision de l'ordre de la milliseconde (Stratum 1), vous **DEVEZ** installer le service NTP officiel maintenu par Meinberg.
>
> 1.  Téléchargez l'installateur **"NTP for Windows"** sur le site officiel : [https://www.meinbergglobal.com/english/sw/ntp.htm](https://www.meinbergglobal.com/english/sw/ntp.htm)
> 2.  Durant l'installation, conservez les options par défaut.
> 3.  Une fois installé, le service "Network Time Protocol Daemon" sera actif sur votre machine.
>
> **Pourquoi ?** W32Time est conçu pour la synchronisation de domaine (Kerberos) avec une tolérance de 5 minutes. Meinberg NTP utilise des algorithmes complexes pour discipliner l'horloge avec une précision microseconde et gérer les sources matérielles comme notre GPS.

## 1. Installation de l'Application

1.  Rendez-vous dans la section **Releases** du projet GitHub.
2.  Téléchargez le dernier installateur : `TimeReferenceNMEA_Setup_vX.Y.Z.exe`.
3.  Lancez l'installation (des droits administrateur seront demandés pour pouvoir gérer le service NTP).
4.  Une icône est créée sur votre bureau.

## 2. Premier Démarrage & Configuration

Au lancement, l'application vérifie la présence du service NTP. Si le service est arrêté, elle tentera de le démarrer.

### Configuration Initiale
1.  Cliquez sur le bouton **Paramètres** (icône d'engrenage).
2.  **Port Série :** Indiquez le port COM de votre RP2040 (ex: `COM3`). Vous pouvez le trouver dans le Gestionnaire de Périphériques Windows sous "Ports (COM & LPT)".
3.  **Vitesse :** Laissez `9600` (sauf si vous avez modifié le firmware du RP2040).
4.  **Chemin ntp.conf :** Indiquez l'emplacement du fichier de configuration du service NTP (généralement `C:\Program Files (x86)\NTP\etc\ntp.conf`).
5.  **Compensation (Fudge) :** Valeur par défaut `0.000`. C'est le délai matériel (Time2) qui sera affiné automatiquement lors de la calibration, mais que vous pouvez ajuster manuellement ici.
6.  Cliquez sur **Enregistrer**.
    *   L'application va générer un fichier `ntp.conf` optimisé pour votre matériel.
    *   Elle va redémarrer le service NTP pour appliquer les changements.

## 3. Interface Principale

L'interface est conçue pour surveiller la précision en un coup d'œil.

### A. Zone Horloges
*   **Système :** L'heure actuelle de votre PC.
*   **GPS :** L'heure reçue du satellite (via le port série).
*   **Indicateurs :**
    *   **Offset :** L'écart entre votre PC et la référence. Doit être proche de 0 ms.
    *   **Jitter :** La stabilité du signal. Plus c'est bas, mieux c'est.

### B. Indicateur de Santé
Un score de 0 à 100% calculé en temps réel par un algorithme de surveillance :
*   🟢 **Vert (> 90%) :** Système stable, précision optimale.
*   🟠 **Orange (50-90%) :** En cours de stabilisation ou perturbations légères.
*   🔴 **Rouge (< 50%) :** Problème critique (GPS débranché, pas de satellites, service arrêté).

### C. Liste des sources
Affiche les sources de temps utilisées par NTP (commande `ntpq -p`).
*   `*` (Astérisque) : La source actuelle de synchronisation (devrait être votre GPS `127.127.20.x`).
*   `+` (Plus) : Sources candidates (Internet) prêtes à prendre le relais.
*   `x` (Croix) : Sources rejetées (trop d'écart ou instables).

## 4. Calibration

La compensation est le délai de transmission matériel (câble USB, traitement série). Il faut le compenser pour être parfaitement à l'heure. Cliquez sur **Calibration** pour lancer l'assistant.

> **ℹ️ Quand calibrer ?**
>
> Cette opération doit être effectuée **une seule fois** pour tout nouvel assemblage (PC - Câble USB - GPS). La compensation est sauvegardée.
> Il est préférable de faire la calibration sur un réseau stable de type **Fibre** plutôt que sur un réseau mobile (3G/4G/5G).

L'application compare votre GPS avec des serveurs de temps Internet (Stratum 1/2) pendant une période donnée.
1.  L'assistant coupe la priorité du GPS.
2.  Il laisse le PC se caler sur Internet (référence fiable).
3.  Il mesure l'écart moyen du GPS par rapport à cette référence.
4.  Il calcule et applique la correction idéale.

## 5. Outils Avancés

*   **Qualité Signal (IQT) :** Analyse la puissance (SNR) et la géométrie (HDOP) des satellites.
    *   *Note : Cette fonction nécessite l'arrêt temporaire de NTP pour accéder directement au port série.*
*   **Logs :** Historique des événements pour le dépannage.

## 6. Mode Mini (Widget)

Pour surveiller votre serveur de temps sans encombrer l'écran :
1.  Cliquez sur le bouton **Mode Mini** ou double-cliquer sur l'afficheur
2.  L'application devient une petite fenêtre transparente qui reste au premier plan ("Always on top").
3.  Double-cliquez dessus pour revenir au mode normal.