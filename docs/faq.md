# FAQ & Dépannage

## Mon GPS ne fixe pas (Pas de LED clignotante)
*   Assurez-vous d'être à l'extérieur ou près d'une fenêtre.
*   Vérifiez l'alimentation du module (la LED Power doit être allumée).

## L'application indique "Port COM introuvable"
*   Vérifiez que le RP2040 est bien branché.
*   Vérifiez dans le Gestionnaire de Périphériques qu'il n'y a pas d'erreur de pilote.

## Pourquoi faut-il les droits administrateur ?
L'application doit arrêter et redémarrer le service Windows Time (W32Time) ou NTPD pour appliquer les corrections de précision.

## Peut-on utiliser NTP sans le GPS ?
Oui. Le service NTP (Meinberg) est conçu pour gérer plusieurs sources.
*   Si le GPS est déconnecté, le service bascule automatiquement sur les serveurs Internet (Pool NTP) configurés en repli.
*   Vous perdez la précision Stratum 1 (microseconde) pour revenir à une précision Stratum 2 (milliseconde), mais votre horloge reste disciplinée et plus précise que via le service Windows standard.
*   L'application affichera simplement que la source GPS est absente, mais le service NTP continuera de tourner en arrière-plan.