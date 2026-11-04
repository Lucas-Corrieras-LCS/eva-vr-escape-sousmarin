# État du projet — ABYSSES

> Bilan de la session du **2026-11-03** (audit, corrections, ajouts).
> Suivi détaillé tâche par tâche : [`BACKLOG.md`](../BACKLOG.md) · statut des consignes : [`CONSIGNES.md`](../CONSIGNES.md).

## Où on en est

Les **3 étages se jouent de bout en bout**, de la surface jusqu'à la remontée finale, **en solo et à 2 joueurs** dans l'éditeur. Console sans erreur ; seul avertissement : les manettes *simulées* de l'éditeur ne savent pas vibrer (sans conséquence sur le Quest).

## Vérifié (dans l'éditeur, sans casque)

| Quoi | Résultat |
|------|----------|
| **Multijoueur à 2** (Multiplayer Play Mode) | Partie complète surface → fin : un module par joueur, sas, digicodes croisés, vannes en anneau, réacteur à deux couleurs, leviers synchronisés |
| **Passe ambiance de Jeremy en jeu** | Éclairage baké (lightmaps + 336 light probes par étage), caustiques, plancton, sons : OK |
| **Build Android** | APK debug produit sans erreur : 67 Mo, 7 min (`Projet/Builds/Abysses_debug.apk`) |
| **Performance (éditeur, étage 1)** | 154 draw calls, 29 SetPass, 22 000 triangles, 0 ombre dynamique — détail dans [`optimisation.md`](optimisation.md) |

## Bugs trouvés grâce au test à 2 joueurs, corrigés

| Bug | Cause | Correction |
|-----|-------|------------|
| Un fusible posé restait visible chez les autres joueurs | Netcode ne désactive pas chez les clients un objet placé dans la scène qu'on « despawn » | Chaque joueur masque le fusible consommé quand l'objectif passe à « résolu » |
| Le sas renvoyait l'objet dans le sas de départ juste après l'envoi | Seul le transform était téléporté ; le Rigidbody le ramenait au pas de physique suivant | Le Rigidbody est téléporté avec l'objet |
| Le client affichait « Remontée réussie en 0 min 00 s » | Netcode applique les variables dans leur ordre de déclaration : la phase « Finished » arrivait avant l'heure de fin | Horaires déclarés avant la phase |

## Ajouté

- **Calibration guidée** : le menu dit où se placer ; la plongée ne démarre que quand chaque joueur est dans sa cabine ; la porte se referme quand le joueur est à bord.
- **Menu en cours de partie** : bouton Menu de la manette gauche (touche **M** dans l'éditeur) pour recalibrer ou quitter ; le menu s'affiche toujours devant le joueur.
- **« Nouvelle plongée »** après la remontée, sans relancer la session (les groupes s'enchaînent chez EVA).
- **Arrivée refusée en cours de partie**, avec le motif affiché au joueur (« Plongée déjà commencée »).
- **Vibrations des manettes** : énigme réussie, mauvais code, leviers ratés, réacteur, descente, sortie de zone.
- **Menus Unity** :
  - *Abysses → Build → APK debug / APK EVA* — la version EVA coupe le joystick (consigne C3) ;
  - *Abysses → Test multijoueur* — un second éditeur s'ouvre et rejoint tout seul ;
  - *Abysses → Reconstruire la Station seulement* — sans recalculer l'éclairage des étages.

## Nettoyé

- Plus de faux changements git à chaque ouverture d'Unity ou génération des scènes (matériaux).
- 12 Mo de lightmaps identiques évités dans Git LFS (quota gratuit : 1 Go).
- Documentation à jour : [`CONSIGNES.md`](../CONSIGNES.md) (statut + preuve de chaque consigne), [`optimisation.md`](optimisation.md) (relevé « avant »), [`README.md`](../README.md) (comment tester, menus).

## Statut des consignes (résumé)

| # | Consigne | Statut |
|---|----------|--------|
| C1 | Application VR adaptée à EVA (pas un FPS) | ✅ |
| C2 | 15 × 8 m par joueur, échelle 1:1 | ✅ |
| C3 | Joystick désactivable, coupé en prod | ✅ (à valider sur casque) |
| C4 | Empêcher de sortir de sa zone (idée originale) | ✅ |
| C5 | Qualité finale | 🟡 playtests casque à faire |
| C6 | Lumière | 🟡 à juger au casque |
| C7 | DA claire et travaillée | 🟡 géométrie encore en blockout |
| C8 | Bonus « étendre l'univers » | ✅ bathyscaphe, 3 étages |
| C9 | Coopération + avatars visibles | ✅ testé à 2 joueurs |
| C10 | Marge de dérive 50 cm | ✅ |

## Ce qui reste, par priorité

1. **Tester sur un vrai Quest 2** — rien n'a encore tourné sur un casque : performances, confort, volumes sonores, joystick réellement coupé. L'APK est prêt ; casque branché en USB, mode développeur activé :
   ```bash
   /Applications/Unity/Hub/Editor/6000.3.23f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb install -r /Users/Eleve/Desktop/IUTS5/UNITY/TP_Unity/Projet/Builds/Abysses_debug.apk
   ```
2. **Passe artistique** — la géométrie est encore faite de formes simples : c'est le plus gros chantier pour les consignes C6 et C7 (lumière et direction artistique), à répartir dans le binôme.
3. **Activer Relay** sur cloud.unity.com (projet *eva-vr-escape-sousmarin* → Multiplayer → Relay) pour jouer en ligne en plus du réseau local.
4. **Playtests** avec de vraies personnes : durée, compréhension des énigmes.

## Mise à jour du 2026-11-04 — branche `feat/ameliorations-jeu`

### Ajouté

| Domaine | Ajout |
|---------|-------|
| Lumière (C6) | Étage 1 en éclairage de secours ; à la remise sous tension, les néons se rallument un par un et l'éclairage complet revient (2 bakes échangés, aucune lumière temps réel en plus) |
| DA (C7) | Coque en tôles rivetées avec rouille et plancher en tôle larmée (textures générées, UV monde), nervures, poutres, conduites ; hublots sur l'océan (neige marine, poissons, bioluminescence, silhouette géante) |
| Ambiance | Réverbération métallique, coque qui grince, gouttes, vapeur qui siffle, néon défaillant, eau qui monte (étage 2), oreilles qui se bouchent pendant la descente |
| Ressenti | Vibrations sur chaque interaction, surbrillance des objets à portée de main, avatars avec buste et bras |
| Prise en main | Tutoriel dans la cabine, montre de plongée (profondeur, O2, chrono), indices progressifs de l'IA de la station, mode facile, FR / EN |
| Exploitation EVA | Pupitre du maître du jeu sur PC (suivi, indice, message, résoudre, passer l'étage, relancer, caméras de surveillance, mode vitrine), classement des équipes, reconnexion automatique d'un casque |
| Récit / coop | Journaux de bord de l'équipage (FR / EN), plaques de coque à frapper (on s'entend à travers la cloison) |
| Fiabilité | Objets perdus qui reviennent ; étages déchargés quand on quitte une partie (bug existant corrigé : un étage restait affiché) |

### Vérifié automatiquement (*Abysses → Tests*, rapports `docs/playtest-*.md`)

- **Partie solo complète** ([`playtest-solo.md`](playtest-solo.md)) : 36 vérifications, 0 erreur de console — panne puis courant rétabli, indice, objet perdu, 3 étages, fin, classement, nouvelle plongée, relance par le pupitre, sortie propre.
- **Partie à 2 + pupitre** ([`playtest-multi.md`](playtest-multi.md)) : l'éditeur héberge, un client Mac compilé joue le 2e plongeur, un autre le pupitre spectateur. 28 vérifications, 0 erreur : pupitre sans avatar avec les droits du maître du jeu, indices envoyés par le pupitre, coupure du 2e plongeur en pleine partie puis **reconnexion automatique dans son module**, partie jusqu'à la fin.

### Bugs trouvés par la relecture et les tests, corrigés

| Bug | Correction |
|-----|------------|
| *(existant)* Après avoir quitté une partie, l'étage restait chargé (brouillard compris) ; la partie suivante en chargeait un second par-dessus | Les étages sont déchargés dès que la session s'arrête |
| Un casque reconnecté aurait été recalibré au centre de son module (décor décalé) | Pas de recentrage quand on retrouve le même quadrant |
| Reconnexion refusée après le délai de grâce | Acceptée tant que le quadrant est libre |
| Reconnexion jamais tentée : Netcode donne un motif même pour une simple coupure | Seuls nos refus d'admission (« Partie complète », « Plongée déjà commencée ») arrêtent les essais |
| Son et vibration du fusible rejoués à l'arrivée d'un joueur en cours de partie | Uniquement quand le fusible est posé |
| La montre gardait l'ancienne profondeur jusqu'à 1 s après l'arrivée | Rafraîchie à chaque changement de phase |

Tests relançables : *Abysses → Tests → Partie solo automatique*, *Compiler le client de test Mac*, puis *Partie à 2 + pupitre*.
- **Ergonomie** : 101 objets interactifs entre 0,8 et 1,6 m, aucun n'oblige à entrer dans la marge de 50 cm (`docs/ergonomie.md`).

### Reste à faire

- Tester au casque : confort, volumes, lisibilité de la montre, vibrations, surbrillance.
- Hors de portée pour l'instant : spatialiseur Meta XR Audio, vrais bruitages, énigmes morse / sonar / grue, Application SpaceWarp, modèles 3D importés.
- Les scènes d'étage pèsent 4 à 7 Mo (maillages de coque enregistrés dans la scène).
