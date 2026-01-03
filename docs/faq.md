# FAQ & Dépannage

## Mon GPS ne fixe pas (Pas de LED clignotante)
*   Assurez-vous d'être à l'extérieur ou près d'une fenêtre.
*   Vérifiez l'alimentation du module (la LED Power doit être allumée).

## L'application indique "Port COM introuvable"
*   Vérifiez que le RP2040 est bien branché.
*   Vérifiez dans le Gestionnaire de Périphériques qu'il n'y a pas d'erreur de pilote.

## Pourquoi faut-il les droits administrateur ?
L'application doit arrêter et redémarrer le service Windows Time (W32Time) ou NTPD pour appliquer les corrections de précision.