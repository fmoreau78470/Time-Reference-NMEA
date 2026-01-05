Même en travaillant seul, adopter les bonnes pratiques Git est un investissement majeur. Cela vous permet de garder un historique clair, de revenir en arrière facilement en cas d'erreur et de synchroniser vos différents postes (PC/Mac) sans friction.

Voici les meilleures pratiques adaptées à votre situation de développeur solo multi-plateforme :

## 📑 Table des matières
*   [0. Installation et Prérequis](#0-installation-et-prérequis)
*   [1. Git Commit : La granularité est la clé](#1-git-commit--la-granularité-est-la-clé)
*   [2. Git Branch : Isoler pour ne pas casser](#2-git-branch--isoler-pour-ne-pas-casser)
*   [3. Git Push & Pull : La synchronisation multi-postes](#3-git-push--pull--la-synchronisation-multi-postes)
*   [Résumé du flux de travail idéal](#résumé-du-flux-de-travail-idéal)
*   [4. Création de l'exécutable (Mise en production)](#4-création-de-lexécutable-mise-en-production)
*   [5. Création de l'installateur (Setup)](#5-création-de-linstallateur-setup)
*   [6. Stratégie de Versionning (SemVer)](#6-stratégie-de-versionning-semver)
*   [7. Cycle de Vie : Correctifs et Nouvelles Fonctionnalités](#7-cycle-de-vie--correctifs-et-nouvelles-fonctionnalités)
*   [8. Automatisation du Versionning (Script)](#8-automatisation-du-versionning-script)
*   [9. Processus de Release Complet](#9-processus-de-release-complet)
*   [10. Configuration GitHub Actions (CI/CD)](#10-configuration-github-actions-cicd)
*   [11. Documentation Utilisateur](#11-documentation-utilisateur)
*   [12. Publication manuelle du Firmware (Stratum0.uf2)](#12-publication-manuelle-du-firmware-stratum0uf2)
*   [13. Mise en production rapide (Cheatsheet)](#13-mise-en-production-rapide-cheatsheet)

---

## 0. Installation et Prérequis

Avant de commencer, assurez-vous d'avoir les outils nécessaires installés :

1.  **Git :** Téléchargez et installez la version pour votre système (Windows/Mac) depuis [git-scm.com](https://git-scm.com/).
2.  **Configuration de base :** Ouvrez un terminal et configurez votre identité (pour l'historique) :
    ```bash
    git config --global user.name "Votre Nom"
    git config --global user.email "votre@email.com"
    ```
3.  **Git Graph (Extension VS Code) :** Installez cette extension pour visualiser vos branches et effectuer des actions (merge, checkout) via une interface graphique.

---

## 1. Git Commit : La granularité est la clé

L'erreur classique est de faire un énorme commit en fin de journée. Considérez le commit comme une "sauvegarde logique".

* **Commitez souvent, commitez petit :** Un commit doit idéalement représenter une seule tâche (ex: "Correction bug affichage menu" ou "Ajout fonction calcul TVA"). Cela facilite grandement les retours en arrière.
* **Des messages explicites :** Utilisez l'impératif ou le présent. Au lieu de "Fichiers modifiés", préférez "Ajoute la validation du formulaire de contact".
* **Ne commitez pas de fichiers inutiles :** Utilisez un fichier `.gitignore` pour exclure les dossiers de dépendances (`node_modules/`), les fichiers système (`.DS_Store` sur Mac) ou les fichiers de configuration locale.

---

## 2. Git Branch : Isoler pour ne pas casser

Même seul, travailler directement sur la branche `main` (ou `master`) est risqué.

* **Une branche = Une fonctionnalité :** Créez une branche pour chaque nouvelle idée ou correction (`feature/nom-du-truc`). Si vous devez soudainement corriger un bug urgent, vous pouvez laisser votre branche en cours et revenir sur `main` sans mélanger le code instable.
* **Fusionnez proprement :** Une fois votre fonctionnalité terminée et testée sur sa branche, fusionnez-la (`merge`) dans `main`.

---

## 3. Git Push & Pull : La synchronisation multi-postes

C'est ici que votre configuration PC/Mac devient centrale. Git devient votre "cloud" de code.

* **Push avant de changer de machine :** Avant de quitter votre PC pour passer sur votre Mac, faites systématiquement un `git push`. Cela envoie votre travail sur le serveur distant (GitHub, GitLab, Bitbucket).
* **Pull en arrivant :** Dès que vous ouvrez votre projet sur l'autre machine, le premier réflexe doit être un `git pull`. Cela récupère les dernières modifications pour éviter les conflits de version.
* **N'ayez pas peur de pousser des branches incomplètes :** Si vous n'avez pas fini une tâche mais changez de lieu, poussez votre branche `feature`. Vous la finirez sur l'autre poste.

---

## Résumé du flux de travail idéal

| Action | Quand le faire ? | Commande type |
| --- | --- | --- |
| **Branch** | Avant de commencer un nouveau truc | `git checkout -b ma-feature` |
| **Commit** | Dès qu'une petite étape est fonctionnelle | `git commit -m "Action précise"` |
| **Push** | Dès que vous avez fini votre session de travail | `git push origin ma-feature` |
| **Pull** | Dès que vous changez d'ordinateur | `git pull origin main` |

> **Astuce d'expert :** Si vous avez du travail en cours non terminé (non commité) et que vous devez changer de branche, utilisez `git stash`. Cela met vos modifs de côté temporairement sans créer de commit "sale".

---

## 4. Création de l'exécutable (Mise en production)

### ⚠️ Note importante sur la méthode de publication
Le mode de publication en **fichier unique (`PublishSingleFile`) est activé** dans le fichier `.csproj`.

Cela permet de générer un exécutable unique contenant toutes les dépendances .NET, ce qui simplifie la distribution. Les problèmes liés aux thèmes WPF et aux fichiers de configuration ont été résolus.

La publication génère un dossier `publish` contenant l'exécutable principal et les fichiers de configuration externes (`config.json`, `ntp.template`). Pour distribuer l'application, vous utiliserez **Inno Setup** (voir étape 5) pour créer un installateur à partir de ce dossier.

Pour générer un fichier `.exe` autonome (qui fonctionne sur un PC sans avoir besoin d'installer .NET) :

1.  Ouvrez le terminal dans le dossier de l'application :
    ```bash
    cd TimeReference.App
    ```
2.  Lancez la commande de publication (les paramètres SingleFile sont désormais dans le .csproj) :
    ```bash
    dotnet publish -c Release
    ```

3.  **Où est le fichier ?**
    Il est généré dans : `bin\Release\net8.0-windows\win-x64\publish\`

> **Erreur fréquente :** Si vous obtenez une erreur `Access to the path ... is denied` ou `Échec inattendu de la tâche "GenerateBundle"`, c'est que l'application est encore en cours d'exécution (ou bloquée en arrière-plan). Assurez-vous de bien fermer `TimeReference.App.exe` avant de relancer la commande.

---

## 5. Création de l'installateur (Setup)

Pour distribuer votre application proprement, nous allons utiliser **Inno Setup** pour empaqueter les fichiers générés à l'étape 4.

### Prérequis
*   Téléchargez et installez **Inno Setup** (gratuit) : jrsoftware.org.

### Procédure

1.  Assurez-vous d'avoir exécuté la commande `dotnet publish` (étape 4).
2.  Créez un fichier texte nommé `setup.iss` dans le dossier `TimeReference.App`.
3.  Collez-y le contenu suivant :

```iss
; Script Inno Setup pour Time Reference NMEA

[Setup]
AppName=Time Reference NMEA
AppVersion=1.0
AppPublisher=Votre Nom
DefaultDirName={autopf}\Time Reference NMEA
DefaultGroupName=Time Reference NMEA
OutputDir=Installer
; Le nom de base est mis à jour automatiquement par le script Set-Version.ps1 pour inclure la version (ex: _v1.2.0)
OutputBaseFilename=TimeReferenceNMEA_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin

[Files]
; Chemin vers les fichiers publiés (relatif à ce script)
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Documentation locale (générée par mkdocs build dans le dossier site à la racine)
Source: "..\site\*"; DestDir: "{app}\Documentation"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Time Reference NMEA"; Filename: "{app}\TimeReference.App.exe"
Name: "{autodesktop}\Time Reference NMEA"; Filename: "{app}\TimeReference.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le Bureau"; GroupDescription: "Icônes supplémentaires:"; Flags: unchecked

[Run]
Filename: "{app}\TimeReference.App.exe"; Description: "Lancer l'application"; Flags: nowait postinstall skipifsilent
```

4.  Double-cliquez sur `setup.iss` pour l'ouvrir dans Inno Setup.
5.  Cliquez sur le bouton **Compile** (ou appuyez sur `Ctrl+F9`).
6.  Votre installateur `TimeReferenceNMEA_Setup.exe` sera généré dans le dossier `TimeReference.App\Installer`.

---

## 6. Stratégie de Versionning (SemVer)

Pour s'y retrouver dans le temps, adoptez la norme **Semantic Versioning** (X.Y.Z) :

*   **MAJOR (X.0.0)** : Changements majeurs, refonte totale, incompatibilité (ex: Passage de Python à C#).
*   **MINOR (1.Y.0)** : Nouvelles fonctionnalités rétro-compatibles (ex: Ajout du mode Expert, nouvelle fenêtre).
*   **PATCH (1.1.Z)** : Corrections de bugs uniquement (ex: Fix crash au démarrage, faute de frappe).

---

## 7. Cycle de Vie : Correctifs et Nouvelles Fonctionnalités

L'objectif est de pouvoir développer le futur (v1.2) tout en étant capable de corriger le présent (v1.1) si un bug est découvert.

### Cas A : Nouvelle Fonctionnalité (Feature)
C'est le flux standard.
1.  Partir de `main` : `git checkout main`
2.  Créer une branche : `git checkout -b feature/ma-super-idee`
3.  Développer, tester, commiter.
4.  Fusionner dans `main` : `git checkout main` puis `git merge feature/ma-super-idee`
5.  Incrémenter **MINOR** (ex: 1.1.0 -> 1.2.0).

### Cas B : Correctif Urgent (Hotfix)
Un bug critique est trouvé en production sur la v1.2.0.
1.  Retrouver l'état exact de la prod (grâce au tag) : `git checkout v1.2.0`
2.  Créer une branche de secours : `git checkout -b hotfix/correction-urgente`
3.  Corriger et tester.
4.  Incrémenter **PATCH** (ex: 1.2.0 -> 1.2.1).
5.  Fusionner dans `main` (pour que le futur l'ait aussi) : `git checkout main` puis `git merge hotfix/correction-urgente`
6.  Créer le tag correctif : `git tag v1.2.1`

### Comment "garder" les anciennes versions ?
C'est le rôle des **Tags**. Un tag est une étiquette indélébile posée sur un commit précis.

*   **Créer un tag :** `git tag v1.0.0`
*   **Envoyer les tags sur le serveur :** `git push --tags`
*   **Revenir voir une vieille version :** `git checkout v1.0.0` (Vous serez en mode "détaché", parfait pour consulter ou recompiler une vieille version).
*   **Revenir au présent :** `git checkout main`

---

## 8. Automatisation du Versionning (Script)

Modifier manuellement les numéros de version dans plusieurs fichiers est une source d'erreurs. On peut automatiser cette tâche avec un simple script PowerShell.

1.  Créez un fichier `Set-Version.ps1` à la racine de votre projet.
2.  Copiez-y le code suivant :

```powershell
# Script pour définir la version du projet Time Reference NMEA
# Utilisation : 
#   .\Set-Version.ps1 -Version 1.2.0  (Force une version)
#   .\Set-Version.ps1                 (Incrémente le Patch automatiquement)

param(
    [Parameter(Mandatory=$false)]
    [string]$Version
)

# --- Chemins des fichiers ---
$csprojPath = ".\TimeReference.App\TimeReference.App.csproj"
$issPath = ".\TimeReference.App\setup.iss"

# --- Logique d'auto-incrémentation ---
if ([string]::IsNullOrWhiteSpace($Version)) {
    $xml = xml
    $currentVersion = $xml.Project.PropertyGroup.Version
    if ($currentVersion -match '^(\d+)\.(\d+)\.(\d+)$') {
        $newPatch = [int]$matches[3] + 1
        $Version = "$($matches[1]).$($matches[2]).$newPatch"
        Write-Host "Auto-incrémentation : $currentVersion -> $Version" -ForegroundColor Yellow
    } else {
        Write-Error "Impossible de lire la version actuelle pour l'incrémenter."
        return
    }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Le format de la version doit être X.Y.Z (ex: 1.2.0). Fourni : $Version"
    return
}

Write-Host "Mise à jour du projet vers la version $Version..." -ForegroundColor Cyan

# --- 1. Mise à jour du fichier .csproj ---
Write-Host "Modification de $csprojPath..."
$xml = xml
$xml.Project.PropertyGroup.Version = $Version
$xml.Project.PropertyGroup.FileVersion = "$Version.0"
$xml.Project.PropertyGroup.AssemblyVersion = "$Version.0"
$xml.Save($csprojPath)
Write-Host ".csproj mis à jour." -ForegroundColor Green

# --- 2. Mise à jour du script Inno Setup ---
Write-Host "Modification de $issPath..."
$issContent = (Get-Content $issPath -Raw) -replace '(?m)^(AppVersion=).*', "AppVersion=$Version"
$issContent = $issContent -replace '(?m)^(OutputBaseFilename=).*', "OutputBaseFilename=TimeReferenceNMEA_Setup_v$Version"
Set-Content -Path $issPath -Value $issContent
Write-Host "setup.iss mis à jour." -ForegroundColor Green

Write-Host "Versionning terminé pour v$Version." -ForegroundColor Cyan
```

### Utilisation
Avant de créer une nouvelle release, ouvrez un terminal PowerShell et lancez :
```powershell
.\Set-Version.ps1 -Version 1.2.1
```
Le script mettra à jour automatiquement les fichiers `.csproj` et `.iss`.

---

## 9. Processus de Release Complet

Voici le workflow complet pour publier une nouvelle version (ex: 1.2.1) :

1.  **Mettre à jour les numéros de version :**
    ```powershell
    .\Set-Version.ps1 -Version 1.2.1
    ```

2.  **Valider les changements avec Git :**
    ```bash
    git add .
    git commit -m "Bump version to 1.2.1"
    ```

3.  **Créer le tag Git et le pousser :**
    ```bash
    git tag v1.2.1
    git push origin main --tags
    ```

4.  **Laisser GitHub travailler :**
    Grâce au fichier `.github/workflows/release.yml`, GitHub va automatiquement :
    *   Détecter le nouveau tag.
    *   Lancer une machine virtuelle Windows.
    *   Compiler le projet .NET.
    *   Générer l'installateur avec Inno Setup.
    *   Créer une "Release" dans l'onglet **Releases** de votre dépôt GitHub.
    *   Y attacher l'exécutable et l'installateur.

---

## 10. Configuration GitHub Actions (CI/CD)

Pour que l'étape 4 ci-dessus fonctionne, un fichier de workflow a été ajouté au projet dans `.github/workflows/release.yml`.

**Ce qu'il fait :**
1.  **Trigger :** Se déclenche uniquement sur les tags (`v*`).
2.  **Build :** Utilise `dotnet publish` pour créer l'exe autonome.
3.  **Setup :** Utilise une action tierce pour compiler le script `setup.iss` d'Inno Setup.
4.  **Release :** Utilise `softprops/action-gh-release` pour publier les fichiers générés.

**Note :** Vous n'avez rien à faire de plus que de pousser vos tags (`git push --tags`). Vous pouvez suivre l'avancement dans l'onglet **Actions** de votre dépôt GitHub.

---

## 11. Documentation Utilisateur

La documentation fait partie intégrante du produit. Voici les standards adoptés pour ce projet :

### Structure
1.  **Théorie NTP :** Vulgarisation des concepts (Jitter, Offset, Stratum) et justification du GPS.
2.  **Matériel :** Guide d'assemblage du module GPS (BOM, câblage, configuration u-blox).
3.  **Logiciel :** Mode d'emploi de l'application WPF (Installation, Calibration Expert, Interprétation des graphiques).

### Outils & Workflow
*   **Format :** Markdown (dans le dossier `/docs`).
*   **Moteur :** **MkDocs** (Thème Material).
*   **Publication :** GitHub Pages (automatisé via GitHub Actions).
*   **Offline :** Site HTML statique généré localement et inclus dans l'installateur (Inno Setup).

### Bonnes Pratiques
*   **Tooltips :** L'aide de premier niveau est intégrée directement dans l'UI (infobulles).
*   **Bouton Aide :** Redirige vers le site de documentation en ligne.
*   **Versionning :** La documentation évolue dans le même dépôt que le code.

### Procédure de mise à jour (Commandes)

Voici les commandes à exécuter lorsque vous modifiez la documentation (fichiers `.md` dans le dossier `docs/`).

#### 1. Prévisualisation (Optionnel)
Pour vérifier le rendu en temps réel pendant que vous rédigez :
```bash
mkdocs serve
```
Ouvrez `http://127.0.0.1:8000` dans votre navigateur.

#### 2. Génération pour l'installateur (Offline)
Pour que la documentation soit incluse dans le prochain installateur (`setup.exe`), vous devez régénérer le site statique localement avant de compiler le setup :
```bash
mkdocs build
```
*Cette commande met à jour le dossier `site/` qui est embarqué par Inno Setup.*

#### 3. Publication sur le Web (GitHub Pages)
Pour mettre à jour le site en ligne, il suffit de pousser vos modifications sur GitHub. Le workflow automatique s'occupe du reste.
```bash
git add .
git commit -m "Mise à jour documentation"
git push
```
*Après quelques minutes, le site web sera à jour.*

## 12. Publication manuelle du Firmware (Stratum0.uf2)

Pour rendre le fichier `Stratum0.uf2` disponible au téléchargement dans la section **Releases** de votre dépôt GitHub, vous devez créer une "Release" manuellement et y attacher le fichier compilé.

Voici la procédure étape par étape à réaliser sur le site web de GitHub :

1.  **Accéder à la section Releases :**
    *   Allez sur la page d'accueil de votre dépôt GitHub.
    *   Dans la colonne de droite, cliquez sur le lien **Releases** (ou "Create a new release" s'il n'y en a aucune).

2.  **Créer une nouvelle version :**
    *   Cliquez sur le bouton **Draft a new release**.

3.  **Remplir les informations de version :**
    *   **Choose a tag :** Cliquez sur ce bouton et tapez un numéro de version (par exemple `v1.0.0`), puis cliquez sur "Create new tag".
    *   **Release title :** Donnez un titre à votre version (ex: "Firmware Initial").
    *   **Describe this release :** Vous pouvez ajouter une description des changements.

4.  **Ajouter le fichier (Important) :**
    *   En bas de la page, repérez la zone encadrée avec le texte **Attach binaries by dropping them here or selecting them**.
    *   Glissez et déposez votre fichier `Stratum0.uf2` dans cette zone (ou cliquez pour le sélectionner sur votre disque).
    *   Attendez que la barre de chargement soit terminée.

5.  **Publier :**
    *   Cliquez sur le bouton vert **Publish release**.

Une fois publié, le fichier `Stratum0.uf2` apparaîtra dans la section "Assets" de cette release, et les utilisateurs pourront le télécharger comme indiqué dans votre documentation.

---

## 13. Mise en production rapide (Cheatsheet) - Processus Local

Un script batch `deploy.bat` a été créé à la racine du projet pour automatiser l'ensemble du processus de build et de déploiement sur Git.

> **Note :** La commande ci-dessous doit être exécutée depuis la **racine du projet** dans un terminal (cmd ou PowerShell).

### 1. Lancement du déploiement local

Pour compiler et déployer une nouvelle version (exemple : `1.2.3`), exécutez simplement :

```cmd
.\deploy.bat 1.2.3
```

Le script se chargera de :
1.  Mettre à jour les numéros de version dans les fichiers du projet.
2.  Générer la documentation locale (`mkdocs build`).
3.  Compiler l'application en mode `Release`.
4.  Créer un commit et un tag Git pour la version.
5.  Pousser les changements et le tag sur GitHub pour déclencher la release.

**Résultat :**
*   Le workflow GitHub Actions se déclenche automatiquement.
*   Il compile le code et l'installateur.
*   Il crée une **Release** sur GitHub et y dépose (upload) automatiquement les fichiers binaires (`setup.exe`).
*   **Délai :** Comptez environ **2 à 5 minutes** pour que le processus se termine et que les fichiers soient disponibles.