# Stratégie d'Internationalisation (i18n)

Ce document centralise la stratégie et les choix techniques pour la gestion du multilingue (Français / Anglais) dans le projet **Time Reference NMEA**.

## 1. Documentation (Site Web)

La documentation est générée par **MkDocs** avec le thème **Material**.

### Stratégie : Structure par Dossiers
Nous utilisons le plugin `mkdocs-static-i18n` qui permet de gérer les traductions via des sous-dossiers distincts. Cela évite de dupliquer la configuration dans `mkdocs.yml`.

**Structure des fichiers :**
```text
docs/
├── fr/             # Langue par défaut (Français)
│   ├── index.md
│   ├── faq.md
│   └── ...
├── en/             # Traduction (Anglais)
│   ├── index.md
│   ├── faq.md
│   └── ...
└── assets/         # Images communes (ne pas dupliquer)
```

### Configuration (`mkdocs.yml`)
Le plugin gère automatiquement le sélecteur de langue et la navigation.

```yaml
plugins:
  - i18n:
      docs_structure: folder
      languages:
        - locale: fr
          name: Français
          default: true
        - locale: en
          name: English
      nav_translations:
        en:
          "Accueil": "Home"
          "Théorie NTP": "NTP Theory"
          "Guide Matériel": "Hardware Guide"
          "Manuel Logiciel": "Software Manual"
          "FAQ & Dépannage": "FAQ & Troubleshooting"
```

**Commandes utiles :**
*   Installation du plugin : `pip install mkdocs-static-i18n`
*   Prévisualisation : `mkdocs serve` (Le site démarre en FR, le sélecteur permet de passer en EN).

---

## 2. Application (WPF / C#)

L'application doit pouvoir changer de langue dynamiquement ou au redémarrage.

### Stratégie : Fichiers de Ressources JSON
Plutôt que les fichiers `.resx` compilés (complexes à éditer pour des contributeurs), nous utilisons des fichiers JSON externes chargés au démarrage.

**Emplacement :** Dossier `lang/` à la racine de l'application.

**Format (`fr.json`) :**
```json
{
  "MAIN_WINDOW_TITLE": "Time Reference NMEA",
  "BTN_CONNECT": "Connecter",
  "STATUS_GPS_OK": "Signal GPS acquis",
  "ERR_PORT_CLOSED": "Le port série est fermé."
}
```

### Implémentation Technique (C#)
1.  **Classe `TranslationManager` (Singleton) :**
    *   Charge le fichier JSON correspondant à la culture (`CultureInfo`).
    *   Stocke les paires Clé/Valeur dans un `Dictionary<string, string>`.
    *   Fournit une méthode `Get(string key)` qui retourne le texte ou la clé si introuvable.
2.  **Extension XAML (MarkupExtension) :**
    *   Permet d'utiliser les traductions directement dans le XAML : `Text="{Lang MAIN_WINDOW_TITLE}"`.
3.  **Persistance :**
    *   Le choix de la langue est sauvegardé dans `config.json` (`language`: "fr" ou "en").

---

## 3. Installeur (Inno Setup)

L'installeur doit utiliser les fichiers de langue standard d'Inno Setup (`French.isl`, `English.isl`) et proposer le choix au démarrage.