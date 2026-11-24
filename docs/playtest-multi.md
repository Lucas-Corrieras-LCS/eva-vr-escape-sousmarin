# Partie de test automatique — 2 plongeurs + pupitre

> 2026-11-24 14:20 · Unity 6000.3.23f1 · *Abysses → Tests*. 
> Énigmes coopératives résolues par le maître du jeu ; énigmes de module (disjoncteurs, morse, sonar) résolues par de vrais appuis. On vérifie l'enchaînement et les systèmes, pas les gestes au casque.

- ✅ Hôte réseau local lancé (plongeur ORANGE)
- ✅ Client Mac lancé (2e plongeur, trouve l'hôte par la découverte réseau local)
- ✅ 2e plongeur connecté, quadrant CYAN
- ✅ Les 2 plongeurs détectés dans leur cabine
- ✅ Pupitre spectateur lancé
- ✅ Pupitre connecté
- ✅ Le pupitre n'a ni avatar ni quadrant
- ✅ Le pupitre a les droits du maître du jeu
- ✅ Plongée à 2 → étage 1
- ✅ Modules ORANGE et CYAN actifs
- ✅ Indice demandé par le pupitre, reçu par le plongeur ORANGE
- ✅ 2e plongeur déconnecté (coupure Wi-Fi simulée)
- ✅ Reconnexion automatique : retour dans le module CYAN
- ✅ Après le délai de grâce, les énigmes du module CYAN restent à faire
- ✅ Étage 1 : « passer l'étage » (pupitre) → étage résolu
- ✅ Étage 1 quitté (phase : FloorSolved, étage 1)
- ✅ Retour en cabine → descente automatique vers l'étage 2
- ✅ Montre : -1 500 m
- ✅ Étage 2 : 4 fuites de vapeur
- ✅ Étage 2 : eau qui monte dans les 4 modules
- ✅ Étage 2 : néons défaillants
- ✅ Étage 2 : 4 cadenas morse
- ✅ Balise morse : code à 3 chiffres tiré (702)
- ✅ Cadenas : un mauvais code est refusé
- ✅ Cadenas : le code de la balise ouvre la vanne d'urgence
- ✅ Étage 2 : « passer l'étage » (pupitre) → étage résolu
- ✅ Étage 2 quitté (phase : FloorSolved, étage 2)
- ✅ Descente vers l'étage 3 (ballasts)
- ✅ Montre : -2 800 m
- ✅ Étage 3 : l'eau monte dans les 4 modules
- ✅ Étage 3 : 4 collecteurs de vannes
- ✅ Collecteur : combinaison tirée au hasard
- ✅ Schéma hydraulique : une ligne par vanne de couleur
- ✅ Collecteur : amorcer avec les vannes fermées → coup de bélier
- ✅ Collecteur : vannes ouvertes une à une (appuis sur les poignées)
- ✅ Collecteur : bonnes vannes ouvertes → la pompe démarre
- ✅ Étage 3 : « passer l'étage » (pupitre) → étage résolu
- ✅ Étage 3 quitté (phase : FloorSolved, étage 3)
- ✅ Descente vers l'étage 4 (salle commune)
- ✅ Montre : -4 000 m
- ✅ Étage 4 : 4 consoles sonar
- ✅ Console sonar : la première note est jouée
- ✅ Console sonar : une erreur relance la séquence
- ✅ Console sonar : séquence de 6 couleurs reproduite → calibrée
- ✅ Étage 4 : relais d'appels en place
- ✅ Relais : DÉMARRER lance le premier appel
- ✅ Relais à plusieurs : le symbole s'affiche chez un autre que la cible
- ✅ Relais : un mauvais symbole est refusé, la manche recommence
- ✅ Relais : 4 manches réussies → énigme résolue
- ✅ Séquence du réacteur + remontée → fin de partie
- ✅ Chrono de fin cohérent
- ✅ Temps enregistré au classement : 1.  1:12  ·  1 plongeur

**Console : aucune erreur.**

**Journal client_plongeur.log** : 
  - `[Abysses] Connexion : Connecting — Recherche d'une partie locale…`
  - `[Abysses] Recherche LAN depuis 192.0.0.2 : broadcast + 1021 adresses sondées une à une.`
  - `[Abysses] Connexion : Connecting — Partie trouvée, connexion…`
  - `[Abysses] Connexion : Online — Équipe rejointe`
  - `[Abysses] Cabine Q1 : plongeur à bord (Lobby)`
  - `[Abysses] Connexion : Failed — Connexion perdue — reconnexion…`
  - `[Abysses] Connexion : Connecting — Connexion à 192.0.0.2…`
  - `[Abysses] Cabine Q1 : plongeur à bord (FloorSolved)`

**Journal client_pupitre.log** : 
  - `[Abysses] Connexion : Connecting — Connexion à 127.0.0.1…`
  - `[Abysses] Connexion : Online — Pupitre connecté`

**Résultat : OK**
