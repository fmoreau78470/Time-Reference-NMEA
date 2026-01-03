# Accueil - Time Reference NMEA

Bienvenue dans la documentation officielle du projet **Time Reference NMEA**.

Ce projet a pour but de transformer un ordinateur standard sous Windows en un **Serveur de Temps de haute précision (Stratum 1)**, capable de discipliner son horloge interne avec une précision de l'ordre de la milliseconde, sans dépendre d'une connexion Internet.

Il a été spécifiquement créé pour répondre aux besoins d'**horodatage précis d'acquisitions** nécessaires à la réalisation de **courbes de lumière** pour l'observation d'**occultations**, particulièrement en situation de mobilité où **aucune connexion Internet n'est disponible**.

## 🎯 Pourquoi ce projet ?

Dans un monde connecté, l'heure est généralement fournie par des serveurs NTP sur Internet (Stratum 2 ou 3). Bien que suffisant pour un usage bureautique, ce système présente des limites :

*   **Latence réseau variable :** Le temps de trajet des paquets sur Internet fluctue (Jitter), dégradant la précision.
*   **Dépendance :** Sans Internet, l'horloge dérive rapidement.
*   **Sécurité :** Dépendance à des tiers.

**Time Reference NMEA** résout ces problèmes en utilisant une source matérielle locale : un récepteur **GPS/GNSS**.

### Les avantages
*   **Précision Stratum 1 :** Votre PC est directement relié à la source atomique des satellites GPS.
*   **Autonomie :** Fonctionne parfaitement en mode "Terrain" (Offline).
*   **Stabilité :** Utilisation du signal PPS (Pulse Per Second) pour une synchronisation ultra-précise.

## 🚀 Fonctionnement global

Le système repose sur la synergie entre trois composants :

1.  **Le Matériel (Hardware) :** Un module GPS (type u-blox) couplé à un microcontrôleur (RP2040) qui convertit les signaux satellites en un flux de données compréhensible par l'ordinateur via USB.
2.  **Le Service NTP (Meinberg) :** Le standard industriel pour la gestion du temps sous Windows. Il discipline l'horloge système en arrière-plan.
3.  **L'Application de Contrôle (Ce logiciel) :** Une interface graphique moderne pour :
    *   Configurer le service NTP sans ligne de commande.
    *   Visualiser la réception GPS et la qualité du signal.
    *   Calibrer automatiquement les délais de transmission (Fudge).
    *   Surveiller la santé de votre serveur de temps.

## 🛠️ Les grandes étapes de mise en œuvre

1.  **Assemblage du Matériel :** Connexion du module GPS au RP2040 et flashage du firmware "Stratum 0".
2.  **Installation Logicielle :** Installation du service NTP Meinberg et de l'application Time Reference NMEA.
3.  **Calibration :** Calcul de la latence (Fudge) pour aligner parfaitement l'heure GPS avec la réalité.
4.  **Mise en Production :** Le système tourne en autonomie et maintient l'heure précise.

## 📚 Organisation de la documentation

*   **Théorie NTP :** Comprendre les concepts de base (Stratum, Jitter, Offset).
*   **Guide Matériel :** Liste des composants et instructions d'assemblage.
*   **Manuel Logiciel :** Installation, configuration et utilisation de l'application.
*   **FAQ & Dépannage :** Solutions aux problèmes courants.
