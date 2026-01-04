# Manuel Logiciel

Ce guide détaille l'installation, la configuration et l'utilisation de l'application **Time Reference NMEA**.

## 1. Installation de l'Application

1.  Rendez-vous dans la section **Releases** du projet GitHub.
2.  Téléchargez le dernier installateur : `TimeReferenceNMEA_Setup_vX.Y.Z.exe`.
3.  Lancez l'installation (des droits administrateur seront demandés pour pouvoir gérer le service NTP).
4.  Une icône est créée sur votre bureau.

## 2. Interface Principale

L'interface est conçue pour surveiller la précision en un coup d'œil. Elle se compose d'une zone d'affichage principale et d'une barre d'outils.

![Capture d'écran de l'interface principale de l'application Time Reference NMEA, montrant des indicateurs de performance en temps réel tels que l'heure système, l'heure GPS, la position géographique, le statut du service NTP, et la santé du système. La fenêtre est organisée avec des graphiques et des boutons d'accès rapide en bas, dans un environnement de bureau. L'image transmet une impression de fonctionnalité et de précision.](PrintScreen/Fenetre_principale.png)
    

### A. Zone d'Affichage
Cette zone regroupe les indicateurs de performance en temps réel :

*   **Horloges :** Compare l'heure système (Windows) et l'heure GPS. Un bouton permet de basculer entre UTC et Heure Locale.
*   **Position :** Latitude et Longitude.
*   **Statut :** Message d'état (ex: "Fix GPS OK", "Recherche de satellites...").
*   **Santé (Score) :** Un indicateur global de fiabilité (0-100%).
    *   🟢 **Vert (> 90%) :** Système stable.
    *   🟠 **Orange (50-90%) :** Perturbations légères.
    *   🔴 **Rouge (< 50%) :** Problème critique.
*   **Métriques NTP :**
    *   **Offset :** L'écart résiduel avec la référence (idéalement < 2ms).
    *   **Jitter :** La stabilité du signal.
*   **Etat du service NTP :** Affiche l'état technique des serveurs NTP.

### B. Zone des Boutons
La barre d'outils située au bas de la fenêtre permet d'accéder aux fonctionnalités :

*   ⚙️ **Paramètres :** Configuration du matériel et du service NTP.
*   📄 **Logs :** Journal des événements.
*   📡 **Qualité Signal (IQT) :** Analyseur de réception satellite (SNR, HDOP).
*   🌐 **Pairs :** Affiche ou masque le détail des sources NTP.
*   🎯 **Etalonnage :** Assistant pour compenser le délai matériel.
*   🔽 **Mode Mini :** Bascule en mode widget transparent "Toujours visible".
*   🎨 **Thème :** Change l'apparence (Clair, Sombre, Rouge/Nuit).

## 3. Premier Démarrage

Au lancement, l'application vérifie la présence du service NTP.

> **⚠️ Prérequis Absolu : Service NTP**
>
> **Ce logiciel ne fonctionne PAS avec le service de temps Windows standard (W32Time).**
>
> Pour atteindre une précision de l'ordre de la milliseconde (Stratum 1), vous **DEVEZ** installer le service NTP officiel maintenu par Meinberg.
>
> 1.  Téléchargez l'installateur **"NTP for Windows"** sur le site officiel : https://www.meinbergglobal.com/english/sw/ntp.htm
> 2.  Durant l'installation, conservez les options par défaut.
> 3.  Une fois installé, le service "Network Time Protocol Daemon" sera actif sur votre machine.
>
> **Pourquoi ?** W32Time est conçu pour la synchronisation de domaine (Kerberos) avec une tolérance de 5 minutes. Meinberg NTP utilise des algorithmes complexes pour discipliner l'horloge avec une précision microseconde et gérer les sources matérielles comme notre GPS.

Si le service est installé mais arrêté, l'application tentera de le démarrer automatiquement.

## 4. Première Configuration

1.  Cliquez sur le bouton **Paramètres** (icône d'engrenage).

    ![Ma photo](PrintScreen/Parametres.png)

    | Champ | Description | Valeur recommandée |
    | :--- | :--- | :--- |
    | **Port Série** | Le port COM virtuel du RP2040 (voir Gestionnaire de Périphériques). | `COMx` |
    | **Vitesse** | La vitesse de communication série du module GPS. | `9600` |
    | **Serveur** | Le pool NTP Internet utilisé pour l'étalonnage et le repli. | `fr.pool.ntp.org` |
    | **Trouver vos serveurs NTP** | Ouvre `ntppool.org` pour trouver les serveurs de votre région. | Copier la liste de serveurs qui correspond à votre pays (sans le mot-clé "server") |
    | **Chemin ntp.conf** | Le chemin complet vers le fichier de configuration du service NTP. | `C:\Program Files (x86)\NTP\etc\ntp.conf` |
    | **Compensation** | Le délai matériel à compenser. Cette valeur est ajustée automatiquement par l'assistant détalonnage. | `0.000` (Initialement) |
    | **Toujours visible** | Maintient la fenêtre au premier plan (Always on Top). | - |
    | **Opacité** | Ajuste la transparence de la fenêtre. | 100% |

3.  Cliquez sur **Enregistrer**.

    *   L'application va générer un fichier `ntp.conf` optimisé pour votre matériel.
    *   Elle va redémarrer le service NTP pour appliquer les changements.


## 4. Etalonnage

La compensation est le délai de transmission matériel (câble USB, traitement série). Il faut le compenser pour être parfaitement à l'heure. Cliquez sur **Etalonnage** pour lancer l'assistant.

> **ℹ️ Quand étalonner ?**
>
> Cette opération doit être effectuée **une seule fois** pour tout nouvel assemblage (PC - Câble USB - GPS). La compensation est sauvegardée.
>
> Il est préférable de faire l'étalonnage sur un réseau stable de type **Fibre** plutôt que sur un réseau mobile (3G/4G/5G).

L'assistant compare votre GPS avec des serveurs de temps Internet (Stratum 1/2) pendant une période donnée.

> Choisissez une durée d'étalonnage (entre 1 min et 60 min). Plus la durée est longue, meilleure sera la précision de l'étalonnage.

Le graphique affiche en gras la médiane des offset (en ms) calculée au fil de la mesure. Les lignes fines correspondent à l'offset mesuré de chaque source.

L'assistant attend que le GPS soit stable (Reach = 377) et qu'au moins une source Internet soit stable (Reach = 377) pour commencer à calculer la médiane.
    ![Ma photo](PrintScreen/Etalonnage.png)

### Algorithme de l'étalonnage

1.  **Isolation (Mode Observation) :**
    *   Le pilote GPS est configuré en mode `noselect` dans NTP.
    *   Il continue d'envoyer des données pour analyse, mais **ne discipline plus** l'horloge locale.
2.  **Référence Absolue :**
    *   Le service NTP est forcé de se synchroniser uniquement sur les serveurs Internet (Stratum 2).
    *   L'horloge système du PC s'aligne donc sur le temps UTC Internet.
3.  **Échantillonnage :**
    *   L'application mesure en continu l'écart (`offset`) rapporté par le pilote GPS par rapport à cette horloge système synchronisée.
    *   Une série de mesures est effectuée pour lisser le "bruit" réseau (Jitter).
4.  **Calcul et Application :**
    *   L'algorithme extrait la **médiane** des écarts pour éliminer les valeurs aberrantes.
    *   Il calcule la correction nécessaire pour aligner le GPS sur Internet et met à jour le paramètre Compensation `time2` (fudge) dans `ntp.conf`.
    *   La valeur `Compensation` est sauvegardée et visible dans les paramètres




## 6. Outils Avancés

*   **Sources NTP (Peers) :** Affiche le détail des serveurs de temps configurés (commande `ntpq -p`).

    Cette fenêtre permet de diagnostiquer pourquoi NTP choisit ou rejette une source.

    La source GPS_NMEA(x) est votre récepteur GPS. x correspond au port COMx utilisé.

       ![Ma photo](PrintScreen/Sources.png)    
       
       > **Note :** Pour fermer cette fenêtre, double-cliquez dessus.
       
       **Légende des symboles:**
    *   `*` (Astérisque) : La source actuelle de synchronisation (System Peer).
    *   `+` (Plus) : Source candidate de bonne qualité, prête à prendre le relais.
    *   `-` (Moins) : Source écartée par l'algorithme de sélection (Outlier).
    *   `x` (Croix) : Source rejetée (Faux ticker, trop d'écart ou inaccessible).

    **Colonnes principales :**
    *   **remote :** Adresse du serveur ou du pilote (ex: `127.127.20.x` pour le GPS).
    *   **refid :** La source de référence de ce serveur (ex: `.GPS.`, `.PPS.`).
    *   **st :** Stratum (Distance par rapport à la source atomique).
    *   **reach :** Registre de disponibilité (377 = 100% de succès sur les 8 derniers essais).
    *   **offset :** L'écart temporel en millisecondes.
    *   **jitter :** La stabilité du signal en millisecondes.

*   **Qualité Signal :** Analyse la puissance (SNR) et la géométrie (HDOP) des satellites.
    *   *Note : Cette fonction nécessite l'arrêt temporaire de NTP pour accéder directement au port série.*

    Cette fenêtre permet de diagnostiquer la qualité de votre installation d'antenne.
    
       ![Ma photo](PrintScreen/QualitéSignalGPS.png)

    **Détail des indicateurs :**

    *   **SCORE:** Indice de Qualité du Signal (0 à 100%).
        C'est une note globale pondérée calculée à partir des trois paramètres ci-dessous.
        *   **100% :** Réception optimale.
        *   **< 50% :** Réception dégradée, risque de perte de synchronisation.

    *   **SNR (Signal Noise Ratio) :** Rapport Signal/Bruit moyen (en dB).
        *   **> 30 dB (Vert) :** Signal fort et clair.
        *   **20 - 30 dB (Orange) :** Signal moyen.
        *   **< 20 dB (Rouge) :** Signal faible, risque de décrochage.

    *   **HDOP (Horizontal Dilution of Precision) :** Précision géométrique.
        *   Indique la dispersion des satellites dans le ciel. Plus la valeur est basse, meilleure est la précision.
        *   **< 2.0 (Vert) :** Idéal.
        *   **2.0 - 5.0 (Orange) :** Acceptable.
        *   **> 5.0 (Rouge) :** Mauvais (Ciel obstrué, canyon urbain).

    *   **SATS :** Nombre de satellites utilisés.
        *   **> 8 (Vert) :** Confortable.
        *   **4 - 8 (Orange) :** Minimum vital.
        *   **< 4 (Rouge) :** Insuffisant pour une triangulation fiable.

*   **Logs :** Historique des événements pour le dépannage.

## 7. Mode Mini (Widget)

Pour surveiller votre serveur de temps sans encombrer l'écran :
1.  Cliquez sur le bouton **Mode Mini** ou double-cliquer sur l'afficheur
2.  L'application devient une petite fenêtre transparente qui reste au premier plan ("Always on top").
3.  Double-cliquez dessus pour revenir au mode normal.

    ![Ma photo](PrintScreen/Fenetre_mini.png)
