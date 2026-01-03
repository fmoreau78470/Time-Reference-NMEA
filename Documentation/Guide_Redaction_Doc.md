# Guide de Rédaction et Déploiement de la Documentation Utilisateur

Ce guide détaille le processus de création, de rédaction et de publication de la documentation utilisateur pour le projet **Time Reference NMEA**, en utilisant **MkDocs** (générateur de site statique) et **GitHub Pages** (hébergement gratuit).

## 1. Installation de l'environnement

La documentation est écrite en Markdown et transformée en site web par Python.

### Prérequis
*   Avoir Python installé sur votre machine de développement.

### Installation des outils
Ouvrez un terminal à la racine du projet et installez MkDocs, le thème "Material" (standard industriel actuel) et le plugin PDF :

```bash
pip install mkdocs mkdocs-material mkdocs-with-pdf
```

## 2. Initialisation de la structure

À la racine du projet (`Time reference NMEA`), lancez :

```bash
mkdocs new .
```
*Cela va créer un fichier `mkdocs.yml` (configuration) et un dossier `docs/` (contenu).*

## 3. Configuration (`mkdocs.yml`)

Remplacez le contenu du fichier `mkdocs.yml` généré par cette configuration adaptée à votre projet :

```yaml
site_name: Time Reference NMEA
site_url: https://votre-compte.github.io/Time-reference-NMEA/
repo_url: https://github.com/votre-compte/Time-reference-NMEA
edit_uri: edit/main/docs/

theme:
  name: material
  language: fr
  palette: 
    - scheme: default
      primary: indigo
      accent: indigo
      toggle:
        icon: material/brightness-7
        name: Passer au mode sombre
    - scheme: slate
      primary: indigo
      accent: indigo
      toggle:
        icon: material/brightness-4
        name: Passer au mode clair
  features:
    - navigation.tabs
    - navigation.sections
    - toc.integrate

plugins:
  - search
  - with-pdf:
      author: Votre Nom
      copyright: "© 2025 Time Reference NMEA"
      output_path: pdf/manuel-utilisateur.pdf

nav:
  - Accueil: index.md
  - Théorie NTP: theorie.md
  - Guide Matériel: materiel.md
  - Manuel Logiciel: logiciel.md
  - FAQ & Dépannage: faq.md
```

## 4. Rédaction du Contenu (Dossier `docs/`)

Créez les fichiers Markdown correspondants dans le dossier `docs/`.

### A. `index.md` (Accueil)
*   **Contenu :** Présentation du projet ("Transformer un PC Windows en serveur de temps Stratum 1").
*   **Liens :** Boutons de téléchargement vers la dernière Release GitHub.

### B. `theorie.md` (Comprendre NTP)
*   **Vulgarisation :** Expliquez simplement ce qu'est le **Stratum** (distance à l'horloge atomique), le **Jitter** (stabilité du signal) et l'**Offset** (décalage horaire).
*   **Pourquoi le GPS ?** Expliquez l'avantage d'une source matérielle (PPS) par rapport à Internet (latence réseau variable).

### C. `materiel.md` (Le Hardware)
*   **BOM :** Liste des courses (RP2040 Zero, Module GPS u-blox NEO-6M/8M, câbles).
*   **Câblage :** Schéma simple (TX GPS -> RX RP2040, PPS GPS -> GP2 RP2040).
*   **Firmware :** Procédure pour flasher le RP2040 avec votre code Arduino (Stratum 0).

### D. `logiciel.md` (L'Application WPF)
*   **Installation :** Guide d'utilisation de l'installateur.
*   **Calibration :** Tutoriel pas à pas de l'Assistant Expert (Pourquoi couper Internet ? Comment interpréter les courbes ?).
*   **Interface :** Explication des indicateurs (IQT, Santé, Horloges).

## 5. Visualisation et Génération

### Mode Développement (Temps réel)
Pour voir le site pendant que vous écrivez :
```bash
mkdocs serve
```
Ouvrez `http://127.0.0.1:8000` dans votre navigateur.

### Génération du PDF (Pour l'Offline)
Pour créer le PDF à inclure dans l'installateur :
```bash
mkdocs build
```
Le PDF sera dans `site/pdf/manuel-utilisateur.pdf`.

## 6. Déploiement Automatique (GitHub Pages)

Pour que la documentation soit publiée automatiquement à chaque modification, créez le fichier `.github/workflows/docs.yml` :

```yaml
name: Documentation
on:
  push:
    branches: [ main ]
    paths: [ 'docs/**', 'mkdocs.yml' ]
permissions:
  contents: write
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with:
          python-version: 3.x
      - run: pip install mkdocs-material mkdocs-with-pdf
      - run: mkdocs gh-deploy --force
```

Enfin, dans les paramètres de votre dépôt GitHub (**Settings > Pages**), sélectionnez la source **Deploy from a branch** et choisissez la branche `gh-pages` (qui sera créée automatiquement par le workflow).