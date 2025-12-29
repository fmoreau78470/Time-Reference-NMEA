# Migration vers C# - Guide de Démarrage

Ce fichier sert de fil conducteur et de pense-bête pour la migration du projet Python vers C# (.NET 8 / WPF).

## Étape 0 : Création d'une branche Git

Comme il s'agit d'un changement radical, il est fortement conseillé de travailler sur une branche dédiée pour conserver la version Python intacte sur `main` (ou `master`).

```powershell
git checkout -b migration-csharp
```

## Étape 1 : Installation du "Moteur" (.NET SDK)

Contrairement à Python, C# a besoin du **SDK .NET** pour compiler et exécuter les programmes.

1.  Aller sur la page officielle : [Télécharger .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
2.  Dans la colonne de gauche **"SDK 8.0.xxx"**, sous **Windows**, choisir **x64**.
3.  Télécharger et installer (`dotnet-sdk-8.0...win-x64.exe`).
4.  **Redémarrer l'ordinateur** (ou fermer/rouvrir VS Code) pour que la commande soit prise en compte.
5.  **Vérification** : Ouvrir un terminal et taper `dotnet --version`. Si un numéro s'affiche, c'est bon.

## Étape 2 : Préparer VS Code

VS Code a besoin d'extensions pour gérer le C#.

1.  Ouvrir l'onglet **Extensions** (`Ctrl+Shift+X`).
2.  Chercher et installer **"C# Dev Kit"** (Microsoft).
    *   *Note : Cela installe automatiquement les dépendances nécessaires.*

## Étape 3 : Création de l'arborescence du projet

Commandes à exécuter dans le terminal (PowerShell) à la racine du projet (`Time reference NMEA\`) pour générer la structure propre :

```powershell
# 1. Créer le dossier racine C#
mkdir CSharp_Version
cd CSharp_Version

# 2. Créer le fichier "Solution" (.sln) - Le conteneur global
dotnet new sln -n TimeReferenceNMEA

# 3. Créer les modules (Projets)
dotnet new classlib -n TimeReference.Core -f net8.0    # Logique métier
dotnet new wpf -n TimeReference.App -f net8.0          # Interface Graphique
dotnet new xunit -n TimeReference.Tests -f net8.0      # Tests unitaires

# 4. Relier les projets à la Solution
dotnet sln add TimeReference.Core/TimeReference.Core.csproj
dotnet sln add TimeReference.App/TimeReference.App.csproj
dotnet sln add TimeReference.Tests/TimeReference.Tests.csproj

# 5. Créer les liens entre les projets (Dépendances)
dotnet add TimeReference.App reference TimeReference.Core
dotnet add TimeReference.Tests reference TimeReference.Core
```

## Étape 4 : Vérification (Hello World)

Pour vérifier que l'environnement fonctionne :
1.  Dans le terminal : `cd TimeReference.App`
2.  Lancer l'app : `dotnet run`
3.  **Résultat attendu** : Une fenêtre blanche vide "MainWindow" doit s'ouvrir.

## Étape 5 : Ajout des dépendances (Port Série)

Pour remplacer `pyserial`, nous devons ajouter le package `System.IO.Ports` au projet Core.

```powershell
# Assurez-vous d'être à la racine du dossier CSharp_Version ou naviguez vers le projet :
cd TimeReference.Core
dotnet add package System.IO.Ports
```

## Étape 6 : Organisation du "Core"

Nous allons structurer la logique métier avant de commencer à coder.

1.  Dans le projet `TimeReference.Core`, créer un dossier **`Models`** (pour définir les objets GPS : Latitude, Longitude, Heure...).
2.  Dans le projet `TimeReference.Core`, créer un dossier **`Services`** (pour le parser NMEA et la gestion du Port Série).
3.  Supprimer le fichier `Class1.cs` qui a été généré par défaut (il ne sert à rien).

## Étape 7 : Création du Modèle de Données

Nous avons créé la classe `GpsData.cs` dans `TimeReference.Core/Models`.
Elle servira de structure standard pour stocker les infos GPS (Heure, Position, Qualité) indépendamment du format NMEA brut.

## Étape 8 : Création du Parser NMEA

Nous avons créé la classe `NmeaParser.cs` dans `TimeReference.Core/Services`.
Elle contient la logique pour :
1.  Découper la trame NMEA (Split).
2.  Convertir les coordonnées "Degrés Minutes" (DDMM.MMMM) en "Degrés Décimaux".
3.  Convertir la date et l'heure en objet `DateTime` C#.

## Étape 9 : Création du Lecteur Série (SerialGpsReader)

Nous avons créé la classe `SerialGpsReader.cs` dans `TimeReference.Core/Services`.
C'est le moteur physique qui remplace `pyserial`.
*   Il ouvre le port COM.
*   Il lance un **Thread** (tâche de fond) pour lire les lignes en continu sans bloquer l'interface.
*   Il utilise `NmeaParser` pour traduire les lignes et déclenche l'événement `GpsDataReceived`.

## Étape 10 : Interface Graphique (WPF)

Nous avons modifié `MainWindow.xaml` et `MainWindow.xaml.cs` dans le projet `TimeReference.App`.

1.  **XAML** : Création d'un formulaire simple (TextBox pour le port, Bouton Connecter, Labels pour l'affichage).
2.  **Code-Behind** :
    *   Instanciation de `SerialGpsReader`.
    *   Utilisation de `Dispatcher.Invoke(...)` pour mettre à jour l'interface depuis le thread du port série (point critique en WPF).

## Étape 11 : Gestion de la Configuration (JSON)

Nous avons ajouté la gestion de la persistance (`config.json`) dans le projet Core.
1.  **`Models/AppConfig.cs`** : Définit les paramètres (Port, BaudRate, Time2, etc.).
2.  **`Services/ConfigService.cs`** : Gère le chargement (`Load`) et la sauvegarde (`Save`) en utilisant `System.Text.Json`.
    *   Le fichier est stocké dans le dossier de l'exécutable (`bin/Debug/net8.0-windows/`).

## Étape 12 : Intégration de la Configuration dans l'UI

Nous avons modifié `MainWindow.xaml.cs` pour utiliser `ConfigService`.
1.  Au démarrage, l'application charge la config et pré-remplit le champ Port COM.
2.  À la connexion, l'application sauvegarde le port saisi et utilise le `BaudRate` défini dans la config.

## Étape 13 : Création du Service NTP

Nous avons créé `NtpService.cs` dans `TimeReference.Core/Services`.
*   Il génère le contenu du fichier `ntp.conf` selon le template standard.
*   Il calcule automatiquement le "mode" NTP (ex: 17 pour 9600 bauds).
*   Il extrait le numéro du port COM (ex: "COM3" -> 127.127.20.3).

## Étape 14 : Bouton de Génération NTP

Nous avons ajouté un bouton "Générer NTP" dans `MainWindow.xaml`.
*   Il sauvegarde la configuration actuelle.
*   Il appelle `NtpService.GenerateConfFile`.
*   Il gère les erreurs (notamment les droits d'accès si le fichier est dans `Program Files`).

## Étape 15 : Auto-élévation (Droits Admin)

Pour écrire dans `Program Files`, l'application doit être lancée en tant qu'administrateur.
1.  Nous avons créé le fichier `app.manifest` dans le projet App avec `<requestedExecutionLevel level="requireAdministrator" ... />`.
2.  Nous avons lié ce fichier dans `TimeReference.App.csproj`.

Désormais, au lancement de l'exécutable (ou via `dotnet run`), Windows affichera la fenêtre de confirmation UAC (Oui/Non).

### ⚠️ Note importante (Erreur 740)

Si vous obtenez l'erreur `System.ComponentModel.Win32Exception (740)` avec `dotnet run`, c'est que le terminal VS Code n'a pas les droits suffisants pour lancer une application Admin.
**Solution** : Fermez VS Code et relancez-le via **Clic droit > Exécuter en tant qu'administrateur**.

## Étape 16 : Redémarrage du Service Windows

Une fois le fichier `ntp.conf` généré, il faut redémarrer le service NTP pour qu'il prenne en compte les changements.

1.  **Ajout du package** (dans `TimeReference.Core`) :
    ```powershell
    cd TimeReference.Core
    dotnet add package System.ServiceProcess.ServiceController
    ```

2.  **Création du Helper** :
    Nous avons créé `WindowsServiceHelper.cs` dans `TimeReference.Core/Services` pour gérer l'arrêt et le démarrage du service (généralement nommé "NTP").

3.  **Mise à jour de l'UI** :
    Dans `MainWindow.xaml.cs`, nous appelons `WindowsServiceHelper.RestartService("NTP")` juste après la génération réussie du fichier.

## Étape 17 : Publication (Création de l'exécutable final)

Pour créer un fichier `.exe` autonome (qui n'a pas besoin d'installer .NET sur l'autre machine), nous devons d'abord nous assurer que les fichiers de configuration sont inclus.

1.  **Inclusion des fichiers** : Modification de `TimeReference.App.csproj` pour copier automatiquement `ntp.template` et `config.json` lors de la compilation.

```xml
<ItemGroup>
  <None Include="..\..\ntp.template" Link="ntp.template">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  <None Include="config.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

2.  **Commande de publication** :
```powershell
cd ..\TimeReference.App
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

L'application finale se trouvera dans `TimeReference.App\bin\Release\net8.0-windows\win-x64\publish\`.

## Étape 18 : Contrôle et Monitoring du Service NTP (Spec 5)

Nous avons ajouté la possibilité de démarrer, arrêter et voir l'état du service NTP directement dans l'interface.

1.  **Mise à jour du Helper** : Ajout de `StartService`, `StopService` et `GetStatus` dans `WindowsServiceHelper.cs`.
2.  **Mise à jour du Code-Behind** : Ajout d'un `DispatcherTimer` dans `MainWindow.xaml.cs` pour rafraîchir l'état toutes les 0.5s.

**Action requise : Mise à jour du XAML (`MainWindow.xaml`)**
Ajoutez ce bloc dans votre grille principale (par exemple en dessous du bouton Générer) pour afficher les contrôles :

```xml
<!-- Zone de contrôle NTP -->
<StackPanel Margin="10">
    <TextBlock x:Name="LblNtpStatus" Text="Service NTP : Inconnu" FontWeight="Bold" Margin="0,0,0,5"/>
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
        <Button x:Name="BtnStartNtp" Content="Démarrer" Width="80" Margin="5" Click="BtnStartNtp_Click"/>
        <Button x:Name="BtnStopNtp" Content="Arrêter" Width="80" Margin="5" Click="BtnStopNtp_Click"/>
        <Button x:Name="BtnRestartNtp" Content="Redémarrer" Width="80" Margin="5" Click="BtnRestartNtp_Click"/>
    </StackPanel>
</StackPanel>
```

*Note : Si vous avez une erreur de compilation concernant `ServiceControllerStatus` dans MainWindow, assurez-vous que le projet App référence bien le package `System.ServiceProcess.ServiceController` ou qu'il a accès aux types de Core.*

## Étape 19 : Fenêtre de Paramètres (Spec 5 - Partie 2)

Nous avons créé une fenêtre dédiée pour modifier la configuration (`config.json`) sans éditer le fichier manuellement.

1.  **Création de la Vue** : `SettingsWindow.xaml` contient les champs (Port, BaudRate, Chemin NTP, Fudge).
2.  **Création de la Logique** : `SettingsWindow.xaml.cs` charge la config au démarrage et la sauvegarde au clic sur "Enregistrer".
3.  **Liaison** : Dans `MainWindow.xaml`, nous avons ajouté un bouton "Paramètres" qui ouvre cette fenêtre via `ShowDialog()`.

**Points d'attention :**
*   La fenêtre principale recharge la configuration (`_configService.Load()`) dès que la fenêtre de paramètres est fermée avec succès, afin de prendre en compte immédiatement un changement de port COM par exemple.
*   Le fichier `SettingsWindow.xaml` doit avoir `Build Action: Page` (par défaut dans VS Code/Visual Studio).

## Étape 20 : Système de Logs et Visualiseur (Spec 5 - Partie 3)

Nous avons implémenté un système complet de journalisation.

1.  **Backend (Core)** :
    *   `Logger.cs` : Écrit les logs dans `logs/log_YYYYMMDD.txt`.
    *   `LogReaderService.cs` : Lit et parse les fichiers logs.
2.  **Frontend (App)** :
    *   `LogWindow.xaml` : Affiche les logs dans un tableau avec filtres (Niveau, Recherche) et gestion des fichiers (Rafraîchir, Supprimer).
3.  **Intégration** :
    *   Ajout d'un bouton "Logs" dans `MainWindow`.
    *   Ajout d'appels `Logger.Info()` et `Logger.Error()` aux endroits clés (Démarrage, Connexion, Erreurs, NTP).

## Étape 21 : Commande ntpq -c clockvar (Spec 6)

Nous avons ajouté une fenêtre non-modale pour surveiller les variables internes du pilote NTP (clockvar).

1.  **Backend (Core)** :
    *   `NtpQueryService.cs` : Exécute la commande `ntpq -c clockvar` et parse le résultat.
2.  **Frontend (App)** :
    *   `ClockVarWindow.xaml` : Affiche les données brutes et décodées, rafraîchies toutes les secondes.
3.  **Intégration** :
    *   Ajout d'un bouton "Moniteur NTP" dans `MainWindow` pour ouvrir cette fenêtre.

## Étape 22 : Indice de Qualité Temporelle (Spec 9)

Nous avons implémenté l'algorithme de calcul de qualité (SNR, HDOP, Satellites).

1.  **Backend (Core)** : Création de `IqtService.cs` pour parser les trames `$GPGSV`, `$GPGGA`, `$GPGSA` et calculer le score (0-100).
2.  **Frontend (App)** : Création de `IqtWindow.xaml` avec des jauges visuelles (SNR, HDOP, Satellites).
3.  **Intégration** : Ajout d'un bouton "Qualité Signal" dans `MainWindow`.
    *   *Note : Nécessite l'arrêt temporaire de NTP car l'accès direct au port COM est requis.*

## Étape 23 : Stratégie Multi-sites et Template (Spec 10)

Nous avons mis en place la gestion intelligente de la configuration NTP via un template.

1.  **Fichier** : Création de `ntp.template` à la racine du projet (copié en sortie de build).
2.  **Backend (Core)** : Modification de `NtpService.cs` pour charger ce template et remplacer les balises `{{ SERIAL_PORT }}`, `{{ FUDGE_TIME }}`, etc.
3.  **Config** : Intégration des directives `tinker panic 0` et `tos orphan 5` dans le template pour le fonctionnement autonome.

## Étape 24 : Interface "Horloges & Qualité" (Spec 12)

Nous avons amélioré la fenêtre principale avec une visualisation temps réel (Mode Numérique).

1.  **Frontend (App)** : Modification de `MainWindow.xaml` et `MainWindow.xaml.cs`.
    *   Ajout du bandeau supérieur : Horloge Système vs Horloge GPS.
    *   Implémentation du Switch UTC/Local.
    *   Récupération et affichage des indicateurs (Offset / Jitter) via `ntpq -p`.

## Étape 25 : Assistant de Calibration Expert (Spec 13 & 14)

Unification des méthodes de calibration (GPS seul vs Internet).

1.  **Frontend (App)** : Création d'une fenêtre de choix `CalibrationChoiceWindow`.
2.  **Frontend (App)** : Création de l'assistant `ExpertCalibrationWindow` (Machine à états : Santé -> Mesure -> Calcul -> Validation).
3.  **Backend (Core)** : Logique de comparaison entre GPS et Serveurs Internet (nécessite connexion web et parsing avancé).

## Étape 26 : Finalisation et Tests

Checklist de validation finale incluant les nouvelles fonctionnalités :

1.  **IQT** : Vérifier le calcul du score de qualité signal.
2.  **Template** : Vérifier que le fichier `ntp.conf` généré respecte bien le template.
3.  **Horloges** : Vérifier la cohérence entre l'heure système et l'heure GPS affichée.
4.  **Calibration** : Tester le mode Expert (avec Internet) et le mode Simple (sans Internet).

## Étape 27 : Affichage GPS via NTP (Mode Non-Intrusif)

Pour éviter les conflits d'accès au port COM (verrouillé par le service NTP), nous avons modifié `MainWindow` pour récupérer les données GPS indirectement.

1.  **Problème** : Si le service NTP tourne, il verrouille le port COM. L'application ne peut pas se connecter en "Direct".
2.  **Solution** : Le pilote NTP `NMEA` expose la dernière trame reçue via la variable `timecode` de la commande `ntpq -c clockvar`.
3.  **Implémentation** :
    *   Ajout d'un Timer (1s) dans `MainWindow.xaml.cs`.
    *   Exécution silencieuse de `ntpq -c clockvar`.
    *   Parsing de la chaîne `timecode="$GPRMC,..."` pour extraire l'heure et la position.
    *   Mise à jour de l'interface comme si les données venaient du port série.

## Étape 28 : Synchronisation Visuelle des Horloges (Spec 12)

Pour garantir que l'heure système et l'heure GPS s'affichent exactement au même moment (sans décalage visuel dû à des timers désynchronisés) :

1.  **Suppression** du timer indépendant pour l'horloge système.
2.  **Modification** : La mise à jour de l'heure système est désormais déclenchée **immédiatement après** la réception et le traitement de l'heure GPS (que ce soit via NTP ou Port Série).

## Étape 29 : Moniteur des Pairs NTP Coloré (Spec 7)

Nous avons remplacé l'affichage de la trame brute (moins utile) par un moniteur d'état des serveurs NTP (`ntpq -p`) en temps réel.

1.  **Interface** : Remplacement du `TextBlock` par un `StackPanel` dans `MainWindow.xaml`.
2.  **Logique** :
    *   Exécution asynchrone de `ntpq -p` toutes les secondes.
    *   Parsing ligne par ligne.
    *   **Coloration syntaxique** selon le "Tally Code" (premier caractère) :
        *   `*` (Vert) : System Peer (Source active).
        *   `+` (Bleu) : Candidate (Source de secours).
        *   `x` (Rouge) : False Ticker (Source rejetée).
        *   `-` (Orange) : Outlier.

## Étape 30 : Indicateur de Santé NTP (Spec 30)

Mise en place d'un algorithme "Sentinel" pour surveiller la stabilité de la liaison GPS/NTP.

1.  **Principe** : Analyse des compteurs `noreply` et `badformat` de la commande `ntpq -c clockvar`.
2.  **Algorithme** :
    *   Échantillonnage toutes les 10 secondes.
    *   Calcul des deltas (variations) des erreurs.
    *   **Surveillance d'activité** : Si le `timecode` ne change pas sur 10s (GPS figé), le score tombe à 0% (État MORT).
    *   **Score de Santé (0-100%)** :
        *   Départ à 100%.
        *   -10 pts par `noreply`.
        *   -25 pts par `badformat`.
        *   +5 pts par cycle parfait (guérison).
3.  **Interface** : Ajout d'un indicateur "Santé" dans la barre de statut (Vert > 90%, Orange > 50%, Rouge < 50%).

## Étape 31 : Refonte UI et Correction Coordonnées (Spec 31)

1.  **Interface** :
    *   Masquage de la zone de connexion (Port COM) et des détails GPS redondants.
    *   Regroupement des horloges (Système et GPS) l'une au-dessus de l'autre.
    *   Déplacement de la position (Lat/Lon) en haut à droite.
2.  **Correction Bug** :
    *   Les coordonnées affichées via NTP (`clockvar`) étaient au format brut NMEA (DDMM.MMMM).
    *   Ajout d'une méthode de conversion pour afficher des degrés décimaux (DD.dddd).

## Étape 32 : Amélioration Affichage (Spec 32)

1.  **Horloges** :
    *   Réduction de la taille de police et alignement en haut à gauche.
    *   Utilisation d'une `Grid` pour aligner parfaitement les libellés et les valeurs numériques.
2.  **Position** :
    *   Ajout de l'affichage en degrés sexagésimaux (DMS : Degrés Minutes Secondes) à côté des degrés décimaux.
    *   Implémentation des méthodes de conversion `NmeaToDms` et `DecimalToDms`.

## Étape 33 : Calibration Simple (Spec 3)

Implémentation du mode de calibration sans Internet, basé sur l'horloge locale.

1.  **Interface** :
    *   Activation du bouton "Simple" dans la fenêtre de choix.
    *   Création de `SimpleCalibrationWindow` avec un slider de durée (2 à 20 minutes).
2.  **Logique** :
    *   Localisation automatique du fichier `loopstats` du jour.
    *   Lecture en temps réel des nouvelles lignes ajoutées par NTP.
    *   Calcul de la moyenne des offsets (Peer - Local).
    *   Ajustement du Fudge Time pour aligner le GPS sur l'horloge système.

## Étape 34 : Mise en avant de l'Indicateur de Santé (Spec 34)

Pour améliorer la visibilité de l'état du système, nous déplaçons l'indicateur de santé dans la zone principale.

1.  **Interface (`MainWindow.xaml`)** :
    *   Déplacement du contrôle "Santé" de la barre de statut vers le bandeau supérieur (Grid du haut), entre les horloges et la position.
    *   Augmentation de la taille de la police (ex: `FontSize="20"`).
    *   **Ajout d'une Info-bulle (ToolTip)** : Explication détaillée de l'algorithme (Pénalités pour NoReply/BadFormat, Bonus Stabilité).
2.  **Logique (`MainWindow.xaml.cs`)** :
    *   Mise à jour de la couleur du texte (`Foreground`) en fonction du score :
        *   Vert (> 90%)
        *   Orange (> 50%)
        *   Rouge (< 50%)

## Étape 35 : Gestion de l'Arrêt du Service NTP

Si le service NTP est arrêté ou introuvable, l'interface ne doit pas afficher de données obsolètes.

1.  **Logique** : Création de la méthode `InvalidateNtpData()`.
    *   Force le score de Santé à 0%.
    *   Remplace les valeurs Offset/Jitter par `--`.
    *   Affiche un message "Service arrêté" dans la liste des pairs.
    *   Si aucune connexion série directe n'est active, efface également les données GPS (Position/Heure).
2.  **Déclenchement** : Appel de cette méthode dès que le monitoring détecte le statut `Stopped` ou `Null`.

## Étape 36 : Alignement Précis des Coordonnées

Pour éviter que l'interface ne "saute" lorsque les coordonnées changent, nous avons normalisé l'affichage.

1.  **Formatage Décimal** : Utilisation d'une largeur fixe (padding) pour aligner le point décimal (ex: `{0,11:F5}`).
2.  **Formatage Sexagésimal (DMS)** :
    *   Suppression des espaces superflus.
    *   Ajout de padding sur les degrés (3 chiffres forcés) afin que les chaînes aient toujours la même longueur visuelle.
    *   Alignement à droite dans une police à chasse fixe (Consolas).

## Étape 37 : Enrichissement des Logs (Traçabilité)

Pour faciliter le diagnostic, nous avons renforcé la journalisation.

1.  **Actions Utilisateur** : Chaque ouverture et fermeture de fenêtre (Paramètres, Logs, IQT) et chaque clic sur les boutons de contrôle NTP (Start/Stop) génère un log `INFO`.
2.  **État du Service** : Le monitoring détecte les changements d'état (ex: Running -> Stopped) et indique si la cause est une "Action utilisateur" ou une "Cause EXTÉRIEURE" (crash).
3.  **Santé NTP** : Les dégradations de santé (Timecode figé, erreurs) génèrent des logs `WARNING`.
4.  **Démarrage** : Un marqueur clair `=== OUVERTURE DE L'APPLICATION ===` est inscrit au lancement.

## Étape 38 : Sécurisation des Actions Critiques

Certaines actions nécessitent une validation de l'utilisateur pour éviter les interruptions de service involontaires.

1.  **Analyse Qualité (IQT)** :
    *   Cette fonction nécessite l'accès exclusif au port COM.
    *   **Modification** : Si le service NTP est actif, une boîte de dialogue demande confirmation pour l'arrêter avant de lancer l'analyse.
    *   Si l'utilisateur refuse, l'action est annulée.
    *   **À la fermeture** : Le service NTP est redémarré automatiquement si c'est l'application qui l'avait arrêté.

2.  **Modification des Paramètres** :
    *   Si des paramètres critiques (Port, BaudRate, Serveurs, Fudge) sont modifiés dans la fenêtre de réglages :
    *   **Modification** : À la fermeture de la fenêtre, l'application détecte les changements et demande confirmation pour redémarrer le service NTP.
    *   **Si Oui** : La configuration est appliquée, `ntp.conf` est régénéré, et le service redémarre.
    *   **Si Non** : Les modifications sont annulées (le fichier `config.json` est restauré à son état précédent) et le service continue de tourner sans changement.

## Étape 39 : Démarrage Automatique du Service NTP

Pour garantir que le système est opérationnel dès l'ouverture de l'application :

1.  **Au lancement (`MainWindow`)** : L'application vérifie l'état du service NTP.
2.  **Si Arrêté** : Elle tente de le démarrer automatiquement.
3.  **Gestion d'erreur** : Si le démarrage échoue (ex: manque de droits), une alerte (`MessageBox`) prévient l'utilisateur et l'erreur est loggée.

## Étape 40 : Refonte du Moniteur NTP (ClockVar)

Pour rendre les données techniques du pilote NTP plus lisibles :

1.  **Interface** : Remplacement de l'affichage texte par une `DataGrid` dynamique.
2.  **Décodage** : Parsing avancé de la variable `timecode` pour extraire et afficher clairement :
    *   Heure et Date GPS.
    *   Position, Vitesse et Cap.
    *   Statut, Mode et Déclinaison Magnétique.
3.  **Ergonomie** :
    *   Ajout d'info-bulles (ToolTips) explicatives sur chaque paramètre.
    *   Conservation de la vue brute (`ntpq` raw output) pour le débogage.
    *   Agrandissement de la fenêtre (800px) et ajout d'un bouton "Fermer".
    *   Ajout du bouton "Pause" pour figer le rafraîchissement.
    *   Ajout du bouton "Copier" pour exporter la réponse brute dans le presse-papier.

## Étape 42 : Option d'affichage compact

Pour alléger l'interface lorsque la surveillance détaillée n'est pas nécessaire :

1.  **Interface** : Ajout d'une case à cocher "Afficher les pairs" dans la barre d'outils.
2.  **Comportement** :
    *   Décoché : La zone "État des pairs" est masquée et la fenêtre se redimensionne automatiquement (`SizeToContent`) pour gagner de la place.
    *   Coché : La zone réapparaît et la fenêtre reprend sa taille standard.

## Étape 43 : Simplification de la fenêtre IQT

Remplacement des jauges graphiques par un affichage numérique simple pour plus de sobriété.

1.  **Interface** : Suppression des contrôles graphiques (Aiguilles, ProgressBars) dans `IqtWindow.xaml`.
2.  **Données** : Affichage textuel des valeurs (Score, SNR, HDOP, Satellites) avec des info-bulles explicatives.

## Étape 44 : Robustesse Connexion IQT

Correction de la gestion du port COM et de la configuration.

1.  **Logique** : Rechargement systématique de la configuration (`_configService.Load()`) avant d'ouvrir la fenêtre IQT pour garantir l'utilisation des derniers paramètres sauvegardés.
2.  **NTP** : Gestion robuste de l'arrêt du service (détection de tous les états non-arrêtés).
3.  **Interface** : Ajout d'un bouton "Réessayer" manuel dans `IqtWindow`.

## Étape 45 : Correction Configuration JSON

Alignement strict des types et des noms de propriétés entre C# et JSON.

1.  **Format** : Le fichier `config.json` doit utiliser le format **snake_case** (ex: `serial_port`) pour correspondre aux attributs `[JsonPropertyName]` de `AppConfig.cs`.
2.  **Types** : Les valeurs numériques (ex: `time2_value`) doivent être des nombres JSON (ex: `0.0011`) et non des chaînes (`"0.0011"`), sinon le chargement échoue silencieusement.

## Étape 47 : Icône de l'Application

Ajout de l'identité visuelle.

1.  **Fichier** : Ajout de `Icone-Time-Reference.ico` dans le dossier `Assets`.
2.  **Projet** : Configuration de `<ApplicationIcon>` dans le `.csproj` pour l'exécutable.
3.  **Fenêtre** : Configuration de la propriété `Icon` dans `MainWindow.xaml` pour la barre de titre et la barre des tâches.

## Étape 48 : Splash Screen Informatif

Ajout d'un écran de démarrage affichant l'identité et la version.

1.  **Ressource** : Ajout de `splash.png` dans `Assets` (Action: Resource).
2.  **Fenêtre** : Création de `SplashScreenWindow` (Transparente, sans bordure) affichant l'image et le numéro de version récupéré dynamiquement.
3.  **Métadonnées** : Les informations (Version, Auteur via `<Copyright>`, Compagnie) sont définies dans le `.csproj` et lues dynamiquement.
4.  **Comportement** : Affichage pendant 3 secondes au lancement de l'application.

## Étape 49 : Centrage de la Fenêtre Principale

Pour une meilleure expérience utilisateur au lancement.

1.  **XAML** : Ajout de `WindowStartupLocation="CenterScreen"` dans `MainWindow.xaml`.

## Étape 50 : Refonte du Bandeau Supérieur

Pour améliorer la lisibilité des indicateurs clés.

1.  **Structure** : Division en 3 blocs distincts (Horloges, État/Qualité, Position) avec des bordures et fonds légers.
2.  **Organisation** : Regroupement logique du Statut, de la Santé et des métriques NTP (Offset/Jitter) au centre.
3.  **Style** : Utilisation de polices semi-bold et de couleurs douces pour hiérarchiser l'information.

## Étape 51 : Gestion des Thèmes (Clair, Sombre, Rouge)

Ajout de la personnalisation de l'interface, incluant un mode "Night Vision".

1.  **Ressources** : Création de dictionnaires XAML (`LightTheme`, `DarkTheme`, `RedTheme`) définissant la palette de couleurs.
2.  **Dynamisme** : Remplacement des couleurs statiques par `DynamicResource` dans le XAML et `SetResourceReference` dans le code C#.
3.  **Contrôle** : Ajout d'un sélecteur de thème dans la barre d'outils.

## Étape 52 : Amélioration de la Netteté du Texte

Correction du rendu "flou" des polices WPF (causé par le mode de rendu par défaut et les ombres portées).

1.  **Rendu** : Activation de `TextOptions.TextFormattingMode="Display"` sur la fenêtre principale pour forcer l'alignement sur les pixels (Pixel Snapping).
2.  **Layout** : Activation de `UseLayoutRounding="True"` pour éviter les flous sur les bordures et les images.

## Étape 54 : Modernisation des Contrôles et Persistance du Thème

1.  **Contrôles** : Remplacement des `CheckBox` par des `ToggleButton` stylisés en "switch" pour une apparence plus moderne.
2.  **Thème** : Remplacement du `ComboBox` par un `Slider` vertical à 3 positions pour une sélection plus visuelle.
3.  **Persistance** : Le thème sélectionné est désormais sauvegardé dans `appstate.json` et restauré au prochain lancement de l'application.

## Étape 53 : Verrouillage du Redimensionnement

Pour éviter l'apparition de zones vides lors d'un redimensionnement manuel non souhaité.

1.  **Fenêtre** : Passage de `ResizeMode` à `CanMinimize` dans `MainWindow.xaml`. La taille est désormais gérée uniquement par le bouton "Afficher les pairs".

## Étape 46 : Persistance de l'État de Santé

Pour éviter que l'indicateur de santé ne se réinitialise à 100% lors d'un redémarrage rapide de l'application (alors que le système est encore instable).

1.  **Sauvegarde** : À la fermeture, le score de santé actuel et l'heure sont sauvegardés dans `appstate.json`.
2.  **Restauration** : Au démarrage, si une sauvegarde existe et date de moins de 5 minutes, le score est restauré. Sinon, il repart de 100%.

## Étape 55 : Déplacement du Moniteur NTP

Pour simplifier l'interface principale, le bouton d'accès au moniteur technique (ClockVar) est déplacé dans la fenêtre de paramètres.

1.  **Interface** : Suppression du bouton "Moniteur" dans `MainWindow` et ajout dans `SettingsWindow`.
2.  **Logique** : Déplacement du gestionnaire d'événement `BtnMonitor_Click` vers le code-behind de la fenêtre de paramètres.
