# Partie de test automatique — 2 plongeurs + pupitre

> 2026-11-06 16:39 · Unity 6000.3.23f1 · *Abysses → Tests*. 
> Énigmes coopératives résolues par le maître du jeu ; énigmes de module (disjoncteurs, morse, sonar) résolues par de vrais appuis. On vérifie l'enchaînement et les systèmes, pas les gestes au casque.

- ✅ Hôte réseau local lancé (plongeur ORANGE)
- ✅ Client Mac lancé (2e plongeur)
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
- ✅ Balise morse : code à 3 chiffres tiré (562)
- ✅ Cadenas : un mauvais code est refusé
- ✅ Cadenas : le code de la balise ouvre la vanne d'urgence
- ✅ Étage 2 : « passer l'étage » (pupitre) → étage résolu
- ✅ Étage 2 quitté (phase : FloorSolved, étage 2)
- ✅ Descente vers l'étage 3 (salle commune)
- ✅ Montre : -4 000 m
- ✅ Étage 3 : 4 consoles sonar
- ✅ Console sonar : la première note est jouée
- ✅ Console sonar : une erreur relance la séquence
- ✅ Console sonar : séquence de 6 couleurs reproduite → calibrée
- ✅ Étage 3 : relais d'appels en place
- ✅ Relais : DÉMARRER lance le premier appel
- ✅ Relais à plusieurs : le symbole s'affiche chez un autre que la cible
- ✅ Relais : un mauvais symbole est refusé, la manche recommence
- ✅ Relais : 4 manches réussies → énigme résolue
- ✅ Séquence du réacteur + remontée → fin de partie
- ✅ Chrono de fin cohérent
- ✅ Temps enregistré au classement : 1.  1:12  ·  1 plongeur

**Console : aucune erreur.**

**Journal client_plongeur.log** : 
  - `[Abysses] Connexion : Connecting — Connexion à 127.0.0.1…`
  - `[Abysses] Connexion : Online — Équipe rejointe`
  - `[Abysses] Cabine Q1 : plongeur à bord (Lobby)`
  - `[Abysses] Connexion : Failed — Connexion perdue — reconnexion…`
  - `[Abysses] Cabine Q1 : plongeur à bord (FloorSolved)`

**Journal client_pupitre.log** : 
  - `[Abysses] Connexion : Connecting — Connexion à 127.0.0.1…`
  - `[Abysses] Connexion : Online — Pupitre connecté`

**Résultat : OK**
