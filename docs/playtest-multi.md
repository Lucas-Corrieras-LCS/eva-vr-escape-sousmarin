# Partie de test automatique — 2 plongeurs + pupitre

> 2026-11-26 08:42 · Unity 6000.3.23f1 · *Abysses → Tests*. 
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
- ✅ Balise morse : code à 3 chiffres tiré (070)
- ✅ Cadenas : un mauvais code est refusé
- ✅ Cadenas : le code de la balise ouvre la vanne d'urgence
- ✅ Étage 2 : 4 mélangeurs d'antidote
- ✅ Antidote : recette et réactifs tirés
- ✅ Antidote : tableau des réactifs (encre UV) rempli
- ✅ Antidote : fiche de recette affichée
- ✅ Antidote : un mauvais dosage mousse et se vide
- ✅ Antidote : recette respectée → antidote prêt
- ✅ Étage 2 : relais d'appels en place
- ✅ Relais : DÉMARRER lance le premier appel
- ✅ Relais à plusieurs : le symbole s'affiche chez un autre que la cible
- ✅ Relais : un mauvais symbole est refusé, la manche recommence
- ✅ Relais : 3 manches réussies → énigme résolue
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
- ✅ Étage 3 : 4 cales électrifiées
- ✅ Écran de surveillance : la carte de la cale est affichée
- ✅ Cale : la dalle DÉPART arme le parcours
- ✅ Cale : une dalle sous tension → décharge, retour au départ
- ✅ Cale : chemin sûr suivi (7 dalles) → vanne de purge atteinte
- ✅ Étage 3 : fuite et salle des pompes tirées
- ✅ Cloisons : le plan n'est affiché que chez un plongeur (module 1)
- ✅ Cloisons : aucune porte ne se ferme avant l'alerte
- ✅ Cloisons : alerte déclenchée, le chrono de l'eau tourne
- ✅ Cloisons : portes autour de l'eau fermées → fuite isolée, pompe au sec
- ✅ Étage 3 : plateaux de lest en place
- ✅ Contrepoids : charges cibles tirées au début de l'étage
- ✅ Contrepoids : registre de lest affiché
- ✅ Contrepoids : le plateau pèse les lests posés dessus (5 kg)
- ✅ Contrepoids : charge fausse, pas d'équilibre
- ✅ Contrepoids : combinaison de lests trouvée pour 9 kg
- ✅ Contrepoids : combinaison de lests trouvée pour 11 kg
- ✅ Contrepoids : chaque plateau à sa charge (dont 9 kg ici) → ballasts équilibrés
- ✅ Étage 3 : verrou de la cabine en place
- ✅ Pupitre : 2 énigme(s) des autres plongeurs résolue(s) par le maître du jeu
- ✅ Étage 3 : tous les fragments du code révélés
- ✅ Verrou : le fragment confié à ce module s'affiche
- ✅ Verrou : un mauvais code est refusé
- ✅ Verrou : le bon code ouvre la cabine
- ✅ Étage 3 : code de la cabine → étage résolu
- ✅ Descente vers l'étage 4 (salle commune)
- ✅ Montre : -4 000 m
- ✅ Étage 4 : consoles sonar et lance-leurre en place
- ✅ Léviathan : position tirée au début de l'étage
- ✅ Sonar : la distance du module s'affiche
- ✅ Léviathan : un tir à côté est raté
- ✅ Léviathan : leurre tiré en F3 → touché
- ✅ Étage 4 : standard des circuits en place
- ✅ Standard : le tableau est chez l'opérateur (module 1)
- ✅ Standard : un câblage faux est refusé, tout se débranche
- ✅ Standard : 4 bonnes liaisons → circuits rétablis
- ✅ Séquence du réacteur + remontée → fin de partie
- ✅ Chrono de fin cohérent
- ✅ Temps enregistré au classement : 1.  1:35  ·  2 plongeurs

**Console : aucune erreur.**

**Journal client_plongeur.log** : 
  - `[Abysses] Connexion : Connecting — Recherche d'une partie locale…`
  - `[Abysses] Recherche LAN depuis 10.136.10.25 : broadcast + 2042 adresses sondées une à une.`
  - `[Abysses] Connexion : Connecting — Partie trouvée, connexion…`
  - `[Abysses] Connexion : Online — Équipe rejointe`
  - `[Abysses] Cabine Q1 : plongeur à bord (Lobby)`
  - `[Abysses] Connexion : Failed — Connexion perdue — reconnexion…`
  - `[Abysses] Connexion : Connecting — Connexion à 10.136.10.25…`
  - `[Abysses] Cabine Q1 : plongeur à bord (FloorSolved)`

**Journal client_pupitre.log** : 
  - `[Abysses] Connexion : Connecting — Connexion à 127.0.0.1…`
  - `[Abysses] Connexion : Online — Pupitre connecté`

**Résultat : OK**
