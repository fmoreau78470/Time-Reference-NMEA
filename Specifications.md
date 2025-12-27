# Spécifications du projet "Time Reference NMEA"

## 25/12/2025
### ℹ️ Remarques générales pour tout le projet
✅ L'IA Gemini mettra à jour ce fichier en mettant cochant les spec qui auront été codées avec succès.

✅ Le projet aura une interface graphique
✅ Le projet sera découpé en petits modules .py pour faciliter la maintenance
✅ Chaque class, méthode ou autre objet sera documenté.
✅ Toutes les étapes au cours du fonctionnement seront loggés dans un fichier dans un sous-répertoire logs
✅ Faire fonctionner le programme en admin. Si le programme n'est pas exécuté en tant qu'admin, relance le programme en admin
✅ Les dépendances externes (pyserial, pynmea2) sont listées dans requirements.txt

### ℹ️ Spec 1
✅ Le programme s'appuie sur le produit NTP by Meinberg. Cela sous-entend que NTP soit en fonctionnement.
✅ Une seule instance du programme pourra être lancée
✅ Au démarrage, si le service W32Time de Windows est actif alors cela veut dire que NTP n'a pas été installé ou qu'il n'est pas lancé.
✅ Dans ce cas, avertir l'utilisateur en lui indiquant qu'il est impératif d'utiliser NTP by Meinberg. Lui proposer de l'installer en indiquant le lien vers le site  https://www.meinbergglobal.com/english/sw/ntp.htm
✅ Fermer l'application

### ℹ️ Spec 2
✅ Mise à jour du fichier ntp.conf. 
✅ Cette mise à jour sera à faire si le service NTP existe sur le PC.
✅ La structure de ce fichier est celle décrite dans memoireprojet.md (Fichier ntp.conf de référence  ). 
✅ Le chemin est config_ntp du config.json. 
✅ Dans les adresse 127.127.20.X le X doit être remplacé par le numéro du COM utilisé par le GPS (variable serial_port du config.json).
✅ Mise à jour des adresses xxxx des serveurs web. La valeur entre server et iburst est celle renseignée par la variable servers de config.json
✅ La valeur xxxx de la ligne fudge time2 est celle de time2_value de config.json
✅ Si le nouveau fichier est différent de celui du répertoire config_ntp, proposer une visualisation du fichier à l'utilisateur et copie dans le répertoire qui va bien (config_ntp du config.json) si acceptation de l'utilisateur.
✅ Si le service est actif, l'arrêter et le redémarrer avec le nouveau fichier.
✅ Si le service est inactif, le démarrer avec le nouveau fichier.
✅ Faire une copie du fichier ntp.conf dans le même répertoire que l'application.
✅ A la présentation du nouveau ntp.conf, présenter côte à côte le fichier ancien et le fichier nouveau.

### ℹ️ Spec 3
✅ Ajustement de la valeur de fudge time2
✅ Utilisation du fichier loopstats.<datecourante> ex: loopstats.20251225 pour le 25 décembre 2025
✅ Utilisation du résultat de la commande ntpq -pn
✅ Utilisation du résultat de la commande ntpq -c clockvar (Note: ntpq -pn suffit pour le reach)
✅ Dans une fenetre dédiée, proposer 3 durées de d'échantillonage (Rapide: 2 mn, Standardf: 5mn et Haute précision: 20 mn).
✅ L'ouverture et la fermeture de cette fenetre doit etre loggée
✅ Le bouton Démarrer étalonnage /arrêter étalonnage est un bouton toogle
✅ Le lancement de la calibration doit etre loggé
✅ Stopper le ntp (commande: net stop ntp)
✅ Effacer le fichier loopstats
✅ Démarrer le ntp (commande: net start ntp)
✅ Boucler la commande ntpq -pn tous les 5 s jusqu'à ce que la ligne contenant l'adresse du GPS (127.127.20.X) ait un reach de 377 ((voir spec de la commande dans MémoireProjet.md)). Attention, ne pas essayer de traduire la valeur du reach qui est en octal
✅ Afficher une indication d'avancement pour faire patienter l'utilisateur
✅ Lancer une tempo de la durée d'échantillonnage avec une indication d'avancement
✅ Lire le fichier loopstats et utiliser l'Algorithme de calcul du nouveau fudge décrit dans MémoireProjet.md pour calculer le nouveau fudge
✅ Prendre en compte les bonnes unités telles que décrite dans "Unités des temps dans NTP" dans MémoireProjet.md
✅ Faire valider le résultat par l'utilisateur et si ok. 
✅ modifier time2_value dans config.json
✅ regénérer un ntp.conf que l'on copiera dans le répertoire config_ntp sans oublier la copie locale
✅ redémarrer le ntp (stop puis start)
✅ Ajouter la possibilité d'arrêter la calibration
✅ La fin de la calibration doit etre loggé
✅ Ajouter un bouton Pause pour suspendre l'échantillonnage
✅ Ajouter le changement de valeur du fudge (ancien et nouveau) dans le log
✅ Ajouter un indicateur visuel pour le reach (Barre de LED). La barre de LED d'interpréter le nombre reçu comme de l'octal. Inverser l'ordre des bits afin que les leds vertes s'affichent de la gauche vers la droite
✅ Ajouter un indicateur visuel pour le temps restant (Compte à rebours circulaire)
✅ Empecher la sortie de la fenetre quand la calibration est lancée

### ℹ️ Spec 4
✅ Interface pour le test du bon fonctionnement du GPS
✅ L'ouverture du port com pour le test du gps entre en concurrence avec ntp qui utilise déjà le port com
✅ Ouverture d'une fenetre dédiée
✅ Bouton toggle Lancer/arrêter le test
✅ Quand le test est lancé, arrêter ntp en demandant l'autorisation à l'utilisateur
✅ Dans un zone défilante, afficher ce qui est reçu du port com 
✅ pendant la durée du test, si on veut voir une zone de la liste en remontant l'ascenceur, il ne faut pas que les nouveles entrées déplacent l'ascenceur.
✅ Ne pas pouvoir fermer la fenetre si le test est en cours
✅ A la fermeture de la fenetre, relancer le ntp s'il a été arrêté

### ℹ️ Spec 5
✅ Ajouter 3 boutons sur l'interface principale: Démarrer le service NTP, Arrêter le service NTP, Redémarrer le service NTP
    ✅ Un label sur l'état du service NTP sera affiché, "Service démarré en vert", "Service arrêté" en rouge
    ✅ Si le service est arrêté et qu'on clique sur le bouton arrêter, ne pas générer de message d'erreur
    ✅ idem pour le bouton démarrer le service
✅ Ajouter un accès à une fenetre de paramétrage
    ✅ La fenetre de paramétrage permettra de modifier le port com et le débit
    ✅ Indiquer le chemin d'accès à ntp.conf
    ✅ Ces valeurs seront pré-renseignées avec les valeurs du fichier config.json
    ✅ Le fichier json sera bien sur mis à jour à la sortie de cette fenetre
    ✅ Ajouter un champ pour modifier manuellement le "Fudge Time2" (time2_value)
    ✅ Ajouter un bouton Annuler pour fermer sans sauvegarder
✅ Ajouter un accès au contenu du fichier log
    ✅ Le log sera présenté sous forme de tableau
    ✅ Possibilité de filtrer suivant le type de message (info, erreur, autres)
    ✅ Ajouter une recherche sur un texte
    ✅ Ajouter un bouton refresh
    ✅ Une liste permettra de choisir parmi les logs existants.
    ✅ Il sera possible d'effacer (avec confirmation) un log qui n'est pas de la date courante

### ℹ️ Améliorations suite au débogage de Spec 5
✅ Suppression des MessageBox de succès (Calibration, Service, Paramètres) pour ne garder que les erreurs.
✅ Rafraîchissement de l'état du service NTP plus rapide (0.5s).
✅ Visualiseur de logs : Filtrage et recherche dynamiques (sans validation par Entrée).
✅ Visualiseur de logs : Support de l'encodage UTF-8 pour les accents.

### ℹ️ Spec 6
✅ Afficher le résultat de la commande "ntpq -c clockvar"
    ✅ Ouvrir une fenetre pour cet usage. Ce n'est pas une fenetre modale.
    ✅ Boucler sur la commande "ntpq -c clockvar" toutes les secondes
    ✅ Afficher le résultat décodé (voir spec de décodage dans memoireprojet.md)
    ✅ La trame $GPRMC affichée par cette commande sera décodée (spec dans memoireprojet.md)

### ℹ️ Spec 7
✅ Dans la fenetre principale, ajoute une zone de visualisation du résultat de la commande ntpq -p
✅ Cette commande sera lancée tous les 5s

### ℹ️ Améliorations suite au débogage de Spec 7
✅ Exécution asynchrone de "ntpq -p" pour éviter le gel de l'interface (Spec 7).
✅ Sécurisation des appels Tkinter depuis les threads secondaires (fix crash fermeture).

### ℹ️ Spec 8
✅ Ajouter une visualisation du fichier ntp.conf
    ✅ Ajouter un bouton Rafraîchir pour recharger le contenu du fichier

### ℹ️ Spec 9
✅ Ajoute une fenetre modale pour calculer l'Indice de Qualité Temporelle (voir l'algorithme dans MemoireProjet.ms)
    ✅ Puisque cela lit le COM, il faut arrêter NTP
    ✅ Afficher le résultat global sous forme d'un grand compteur rotatif.
    ✅ Afficher les 3 sous-indicateurs (SNR, HDOP, Qté) sous forme de petits compteurs rotatifs sous le principal.
    ✅ Conserver les bulles d'aide explicatives pour chaque indicateur.
    ✅ Adopter le style Vintage (Fond noir, cadran beige) pour harmoniser avec le Vu-mètre d'offset.

### ℹ️ Spec 10
✅ Mise en place de la stratégie Multi-sites (Hybride Terrain/Maison).
✅ Création et utilisation d'un fichier `ntp.template` externe au lieu de modifier une copie de sauvegarde ou d'avoir le code en dur.
✅ Le template intègre les directives `tinker panic 0` et `tos orphan 5` pour le fonctionnement sans Internet/GPS.
✅ Le template configure le pool NTP internet en mode fallback (sans `noselect` mais avec `prefer` sur le GPS).
✅ Le programme Python doit charger ce template pour générer le fichier `ntp.conf` actif.
✅ Utilisation de marqueurs explicites `{{ VARIABLE }}` dans le template. Le bloc `{{ SERVERS_BLOCK }}` est généré en appliquant une variable globale `server_options` (ex: "iburst") à chaque serveur de la liste.

### ℹ️ Spec 12
✅ Ajouter un cadre "Horloges & Qualité" en haut de la fenêtre principale
    ✅ Afficher l'heure système du PC et l'heure GPS.
    ✅ Synchroniser l'affichage des deux horloges pour éviter le décalage visuel.
    ✅ Les deux heures sont affichées l'une au-dessus de l'autre
    ✅ Ajouter un commutateur "Mode UTC" / "Heure Locale"
    ✅ L'état du commutateur est sauvegardé dans config.json (paramètre utc_mode)
    ✅ Intégrer les Vu-mètres (Offset et Jitter) à droite des horloges.
        ✅ Style Vintage (Boîtier noir, cadran beige, aiguille rouge).
        ✅ Echelle logarithmique pour une meilleure précision au centre.
        ✅ 
### ℹ️ Spec 13
✅ Ajouter un "Assistant de Calibration (Expert)" basé sur l'algorithme du mémoire
    ✅ Implémenter une machine à états (Initialisation -> Santé -> Mesure -> Application -> Validation)
    ✅ Étape 1 : Passage en mode observation (noselect) pour les serveurs Internet

### ℹ️ Spec 14
✅ Unifier le lancement des calibrations.
    ✅ Au clic sur "Lancer une Calibration", proposer un dialogue de choix.
    ✅ Option 1: "GPS Seul (via Loopstats)" lance la calibration simple (ancienne Spec 3).
    ✅ Option 2: "GPS vs Serveurs Net (Assistant Expert)" lance l'assistant complet (Spec 13).
    ✅ Étape 2 : Vérification de la santé du GPS (Reach=377, Jitter < 100ms)
    ✅ Étape 3 : Mesure comparative des offsets (GPS vs Internet) sur 30s
    ✅ Étape 4 : Calcul du delta et application du nouveau Fudge
    ✅ Étape 5 : Validation et sauvegarde
    ✅ Affichage des logs détaillés dans la fenêtre
    ✅ Bouton Pause pour suspendre le processus sans arrêter NTP

### ℹ️ Documentation du fichier de configuration (config.json)
Le fichier `config.json` stocke les paramètres persistants de l'application :
*   `serial_port` : Port COM utilisé par le GPS (ex: "COM8").
*   `baud_rate` : Vitesse de communication du port série (ex: 9600).
*   `timeout` : Délai d'attente pour la lecture série (en secondes).
*   `config_ntp` : Chemin absolu vers le fichier `ntp.conf` du service NTP.
*   `servers` : Liste des serveurs NTP distants (stratum 1/2) utilisés comme référence ou fallback.
*   `server_options` : Options NTP appliquées aux serveurs distants (ex: "iburst", "noselect").
*   `time2_value` : Valeur de compensation (Fudge) pour le délai GPS (en secondes).
*   `utc_mode` : Préférence d'affichage de l'heure (True=UTC, False=Locale).