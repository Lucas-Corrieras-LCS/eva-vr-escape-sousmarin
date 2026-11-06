# Jouer à plusieurs : guide réseau

## Le cas normal : réseau local (un casque héberge, les autres rejoignent)

1. Tous les casques sur **le même Wi-Fi** : Wi-Fi d'EVA, ou partage de connexion d'un téléphone.
2. Sur **un seul** casque : **Héberger la partie**. Le menu affiche « Partie créée », avec son adresse IP.
3. Sur les autres : **Rejoindre la partie**. Le casque trouve l'hôte tout seul, en 1 à 2 s, 15 s au maximum.
4. Chacun entre dans sa cabine. Quand tout le monde est à bord, n'importe qui lance la plongée.

Le pupitre du maître du jeu (PC) peut aussi héberger ou rejoindre. Bouton *Rejoindre l'IP* : il suffit de taper l'adresse affichée par l'hôte.

### Conseils

- **L'hôte garde son casque sur la tête.** Un Quest posé se met en veille : au-delà de 30 s, la partie s'arrête pour tout le monde. Sinon, faire héberger le PC du pupitre.
- **Un joueur qui décroche** (Wi-Fi, casque retiré quelques secondes) se reconnecte seul pendant 30 s. Il retrouve son module.
- **Wi-Fi d'école ou d'entreprise** : ces réseaux isolent souvent les appareils entre eux. Dans ce cas, aucune découverte ni connexion n'est possible : prendre un partage de connexion.

## En ligne (Internet)

Il faut Internet sur chaque casque. Le premier qui appuie crée la session ; les suivants la rejoignent par le relais Unity.

- Une seule équipe à la fois : toutes les parties en ligne partagent le même nom de session.
- Le jeu tente 3 fois d'obtenir la session. Sans réponse de l'hôte en 25 s, il abandonne et propose le réseau local.
- Diagnostic des services : *Abysses → Tests → Diagnostic du mode en ligne* (résultat dans `docs/diagnostic-en-ligne.md`).

## Ce qui a été corrigé le 2026-11-06

| Symptôme | Cause | Correction |
|----------|-------|------------|
| « Aucune partie trouvée » au hasard, surtout sur un partage de connexion | Le Wi-Fi du Quest (Android) jette les broadcasts reçus si l'application ne tient pas de verrou multicast | Verrou multicast pris pendant la recherche et l'annonce ; permissions `CHANGE_WIFI_MULTICAST_STATE`, `ACCESS_WIFI_STATE`, `ACCESS_NETWORK_STATE` et `INTERNET` ajoutées au manifeste |
| Découverte bloquée sur certains points d'accès | Annonce envoyée seulement à 255.255.255.255 | Annonce aussi sur l'adresse de broadcast de chaque réseau (ex. 192.168.43.255, 172.20.10.15), et second canal : le casque qui cherche envoie une demande, l'hôte lui répond directement |
| En ligne : session pleine ou bloquée, « hors ligne » après un échec | Une session ratée n'était jamais quittée (membre fantôme), pas de nouvel essai, aucun délai maximal | Session toujours quittée en cas d'échec ou de coupure ; 3 essais ; abandon après 25 s sans réponse de l'hôte, avec un message clair |
| Décrochage trop vite définitif | 5 essais de reconnexion (15 s) | 10 essais (30 s) ; l'hôte garde le module libre 30 s |
| Impossible de rejoindre à la main | Adresse de l'hôte inconnue | L'hôte affiche son IP dans le menu |

Tests : partie à 2 + pupitre (le 2e plongeur trouve l'hôte par la découverte réseau local, coupure et reconnexion comprises) et partie solo, toutes deux réussies ; services en ligne vérifiés (authentification, sessions, Relay).
