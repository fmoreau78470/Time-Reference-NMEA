Même en travaillant seul, adopter les bonnes pratiques Git est un investissement majeur. Cela vous permet de garder un historique clair, de revenir en arrière facilement en cas d'erreur et de synchroniser vos différents postes (PC/Mac) sans friction.

Voici les meilleures pratiques adaptées à votre situation de développeur solo multi-plateforme :

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

Pour générer un fichier `.exe` autonome (qui fonctionne sur un PC sans avoir besoin d'installer .NET) :

1.  Ouvrez le terminal dans le dossier de l'application :
    ```bash
    cd TimeReference.App
    ```
2.  Lancez la commande de publication :
    ```bash
    dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
    ```
    *   **`-c Release`** : Version optimisée pour la performance.
    *   **`--self-contained`** : Embarque le moteur .NET (l'exe sera plus gros, ~60Mo, mais universel).
    *   **`-p:PublishSingleFile=true`** : Combine tous les fichiers en un seul `.exe`.

3.  **Où est le fichier ?**
    Il est généré dans : `bin\Release\net8.0-windows\win-x64\publish\`

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
OutputBaseFilename=TimeReferenceNMEA_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin

[Files]
; Chemin vers les fichiers publiés (relatif à ce script)
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

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
