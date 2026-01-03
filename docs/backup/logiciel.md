# Manuel du Logiciel

L'application **Time Reference NMEA** est le cœur du système sous Windows. Elle configure le service de temps Windows et assure la liaison avec le matériel.

!!! "⚠️ Prérequis Absolu : NTP by Meinberg"
    **Ce logiciel ne fonctionne PAS avec le service de temps Windows standard (W32Time).**
    
    Pour atteindre une précision de l'ordre de la milliseconde (Stratum 1), vous **DEVEZ** installer le service NTP officiel maintenu par Meinberg.
    
    1.  Téléchargez l'installateur **"NTP for Windows"** sur le site officiel : [https://www.meinbergglobal.com/english/sw/ntp.htm](https://www.meinbergglobal.com/english/sw/ntp.htm)
    2.  Durant l'installation, conservez les options par défaut.
    3.  Une fois installé, le service "Network Time Protocol Daemon" sera actif sur votre machine.
    
    **Pourquoi ?** W32Time est conçu pour la synchronisation de domaine (Kerberos) avec une tolérance de 5 minutes. Meinberg NTP utilise des algorithmes complexes pour discipliner l'horloge avec une précision microseconde et gérer les sources matérielles comme notre GPS.

## 1. Installation de l'Application
## Calibration
Lors du premier lancement, il est crucial de calibrer le délai de transmission (latence USB).

1.  Ouvrez le menu **Outils > Calibration**.
2.  Choisissez le **Mode Expert** (nécessite Internet temporairement).
3.  Laissez l'assistant mesurer l'écart entre le GPS et les serveurs de temps Internet.
4.  Appliquez le "Fudge" calculé.