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

Pour créer un fichier `.exe` autonome (qui n'a pas besoin d'installer .NET sur l'autre machine) :

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

Amélioration de la fenêtre principale avec visualisation temps réel et style "Vintage".

1.  **Frontend (App)** : Modification de `MainWindow.xaml` pour ajouter le bandeau supérieur.
    *   Affichage double : Horloge Système vs Horloge GPS.
    *   Switch UTC/Local.
    *   Vu-mètres (Offset / Jitter) basés sur les données NTP.
2.  **Backend (Core)** : Ajout d'un parser dans `NtpQueryService` pour extraire Offset/Jitter de la commande `ntpq -p`.

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
