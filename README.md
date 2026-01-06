# Time Reference NMEA

<div align="center">

[![en](https://img.shields.io/badge/lang-en-red.svg)](https://github.com/fmoreau78470/Time-Reference-NMEA/blob/main/README.en.md)
[![fr](https://img.shields.io/badge/lang-fr-blue.svg)](https://github.com/fmoreau78470/Time-Reference-NMEA/blob/main/README.md)

**Transformez votre PC Windows en serveur de temps Stratum 1 de haute précision.**

[![Soutenir sur Ko-fi](https://img.shields.io/badge/Ko--fi-Soutenir%20le%20projet-blue?style=for-the-badge&logo=kofi)](https://ko-fi.com/francismoreau)
[![Documentation](https://img.shields.io/badge/docs-online-blue?style=for-the-badge&logo=read-the-docs)](https://fmoreau78470.github.io/Time-Reference-NMEA/)
[![License](https://img.shields.io/badge/license-MIT-green?style=for-the-badge)](LICENSE)
[![Release](https://img.shields.io/badge/release-latest-orange?style=for-the-badge)](https://github.com/fmoreau78470/Time-reference-NMEA/releases)

</div>

## 🔭 Pourquoi ce projet ?

Ce projet a été développé pour répondre à un besoin critique en **astronomie** : l'horodatage précis des acquisitions pour les courbes de lumière lors d'**occultations stellaires**. Ces observations se font souvent sur le terrain, en situation de mobilité, là où aucune connexion Internet fiable n'est disponible.

Les solutions classiques (NTP via Internet) souffrent de latence variable (Jitter) et nécessitent une connexion. **Time Reference NMEA** utilise une source matérielle locale (GPS + Signal PPS) pour discipliner l'horloge Windows avec une précision de l'ordre de la **milliseconde**, en toute autonomie.

## ✨ Fonctionnalités Clés

*   **Précision Stratum 1 :** Synchronisation directe sur l'horloge atomique des satellites GNSS.
*   **Mode Hors-ligne (Offline) :** Fonctionne parfaitement sans Internet.
*   **Technologie PPS :** Utilisation du signal *Pulse Per Second* pour éliminer le jitter de transmission série.
*   **Application de Contrôle (WPF) :**
    *   Interface intuitive type "Raquette de commande".
    *   **Calibration automatique** du délai matériel (Fudge).
    *   Surveillance en temps réel : Offset, Jitter, Santé du système.
    *   Analyseur de qualité du signal GPS (IQT : SNR, HDOP, Satellites).
    *   Mode Widget (Mini) "Always on top".

## 🛠️ Matériel Requis

Le système repose sur un matériel accessible et peu coûteux (< 20€) :

1.  **Microcontrôleur :** Waveshare RP2040-Zero (Interface USB & Traitement).
2.  **Module GPS :** u-blox NEO-6M ou NEO-8M.
3.  **Liaison :** Câble USB-C Data.

*Le firmware "Stratum 0" pour le RP2040 est disponible dans les Releases.*

## 💻 Installation & Prérequis

### ⚠️ Prérequis Absolu
Ce logiciel pilote le service **NTP officiel de Meinberg**.
Le service de temps Windows standard (W32Time) n'est **PAS** supporté car insuffisant pour la précision visée.

1.  Téléchargez et installez [NTP for Windows (Meinberg)](https://www.meinbergglobal.com/english/sw/ntp.htm).
2.  Téléchargez l'installateur `TimeReferenceNMEA_Setup.exe` depuis les Releases GitHub.

### Utilisation Rapide
1.  Branchez votre module RP2040/GPS.
2.  Lancez **Time Reference NMEA**.
3.  Dans les paramètres, sélectionnez le port COM détecté.
4.  L'application configure automatiquement le service NTP local.
5.  Lancez la **Calibration** pour compenser les délais USB.

## 📚 Documentation

Une documentation complète est disponible pour vous guider pas à pas :
*   Théorie NTP
*   Guide d'assemblage Matériel
*   Manuel Logiciel

👉 **Accéder à la documentation complète**

## 🏗️ Architecture Technique

*   **Firmware (RP2040) :** C++ / Arduino. Algorithme de "Time Adder" pour aligner la trame NMEA sur le signal PPS.
*   **Logiciel PC :** C# .NET 6/8 (WPF). Interface avec `ntpq` et gestion du service Windows.
*   **Documentation :** MkDocs avec le thème Material.

## 📄 Licence

Ce projet est distribué sous licence MIT.
