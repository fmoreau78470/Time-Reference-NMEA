# Théorie NTP & Synchronisation

Comprendre les mécanismes qui permettent à votre ordinateur d'être à l'heure précise est essentiel pour tirer le meilleur parti de ce projet. Cette page vulgarise les concepts clés du protocole NTP et explique pourquoi l'ajout d'un GPS change la donne.

## 1. Qu'est-ce que NTP ?

**NTP (Network Time Protocol)** est l'un des plus vieux protocoles d'Internet. Son rôle est de synchroniser l'horloge des ordinateurs à travers un réseau à latence variable.

Il fonctionne sur un modèle hiérarchique organisé en **Strates (Stratum)** :

*   **Stratum 0 :** La source de temps physique (Horloge Atomique, GPS). Elle ne se connecte pas au réseau.
*   **Stratum 1 :** Un ordinateur directement relié à une source Stratum 0 (par câble série/USB). C'est le "Serveur de Temps Primaire". **C'est le rôle de votre PC avec ce projet.**
*   **Stratum 2 :** Un ordinateur qui demande l'heure à un serveur Stratum 1 via Internet.
*   **Stratum 3 :** Un ordinateur qui demande l'heure à un Stratum 2, et ainsi de suite...

Plus le numéro de strate est élevé, plus la précision se dégrade à cause des délais de transmission.

## 2. Pourquoi le GPS est-il supérieur à Internet ?

La plupart des PC se synchronisent sur Internet (Stratum 2 ou 3). Bien que suffisant pour la vie quotidienne, ce système souffre de défauts majeurs pour l'astronomie ou la science :

### A. La Latence Variable (Jitter)
Sur Internet, les paquets de données voyagent par des chemins différents et traversent des routeurs encombrés. Le temps de trajet "aller" n'est jamais exactement le même que le temps "retour". Cette variation imprévisible s'appelle le **Jitter** (Gigue).
*   *Internet :* Jitter typique de 10 à 100 ms.
*   *GPS Local :* Jitter quasi-nul (< 0.005 ms) car la liaison est directe (câble USB/Série).

### B. L'Asymétrie
NTP suppose théoriquement que le temps de trajet Aller est égal au temps de trajet Retour. Or, sur une connexion ADSL, 4G ou Fibre grand public, les débits descendants et montants sont différents, créant une erreur de calcul systématique (Offset) impossible à corriger logiciellement.

### C. La Disponibilité
Sans Internet, l'horloge de votre PC dérive rapidement (plusieurs secondes par jour). Le GPS fonctionne partout sur Terre, sans abonnement et sans réseau, garantissant une autonomie totale (mode "Terrain").

## 3. Pourquoi utiliser Meinberg NTP ?

Windows intègre par défaut un service de temps appelé **W32Time**. Pourquoi le remplacer ?

*   **W32Time** a été conçu pour l'authentification réseau (Kerberos), qui tolère jusqu'à 5 minutes d'erreur. Il n'est pas conçu pour la haute précision scientifique. Il corrige souvent l'heure par "sauts" brutaux.
*   **Meinberg NTP** est le portage sous Windows du démon NTP officiel (celui utilisé par les serveurs Linux de la NASA ou du CERN).
    *   Il discipline l'horloge en douceur (accélère/ralentit la fréquence) sans sauts temporels.
    *   Il atteint une précision de l'ordre de la microseconde.
    *   Il offre des outils de diagnostic avancés (`ntpq`) que ce logiciel utilise.

## 4. Les Algorithmes de NTP

NTP n'est pas une simple "mise à l'heure". C'est un système complexe d'asservissement.

### L'Algorithme d'Intersection (Marzullo)
Si vous configurez plusieurs sources (ex: GPS + 3 serveurs Internet), NTP doit deviner qui dit la vérité. Il compare les intervalles de temps de chaque source et rejette les "menteurs" (False Tickers) qui s'éloignent trop du consensus. C'est ce qui permet d'ignorer un serveur Internet défaillant.

### La Boucle de Discipline (PLL/FLL)
Une fois la meilleure source choisie, NTP ne se contente pas de "reset" l'heure. Il calcule la **dérive naturelle** de votre quartz (Drift).
*   Si votre PC retarde de 10ms, NTP va augmenter légèrement la fréquence de l'horloge pour rattraper le retard progressivement.
*   Cela garantit une échelle de temps continue et monotone (pas de retour en arrière possible), indispensable pour l'enregistrement de données (Logs, Bases de données, Courbes de lumière).

## 5. Glossaire des Termes Usuels

Voici les termes techniques que vous rencontrerez dans l'application :

| Terme | Définition | Unité | Bonnes valeurs |
| :--- | :--- | :--- | :--- |
| **Offset** | L'écart temporel entre votre PC et la référence absolue. C'est l'erreur à corriger. | ms | Proche de 0 (ex: +/- 2ms) |
| **Jitter** | La stabilité du signal (variance de la latence). Une valeur faible indique une connexion fiable. | ms | < 5 ms (GPS) |
| **Drift** | La dérive naturelle de l'horloge matérielle de votre PC (imprécision du quartz). | ppm | Stable (ex: 15.000 ppm) |
| **Reach** | Registre (octal) indiquant le succès des 8 dernières tentatives de connexion. C'est l'historique de santé. | - | **377** (100% de succès) |
| **Poll** | Intervalle entre deux interrogations de la source (puissance de 2). | s | 16s (Poll 4) à 1024s (Poll 10) |
| **PPS** | *Pulse Per Second*. Signal électrique envoyé par le GPS à chaque début de seconde précise. C'est le "top" ultime de synchronisation. | - | - |