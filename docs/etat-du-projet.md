# État du projet — ABYSSES

> Bilan des sessions du **2026-11-03** (audit, corrections, ajouts), du **2026-11-05** (passe artistique) et du **2026-11-06** (surface, coopération obligatoire, réseau local).
> Suivi détaillé tâche par tâche : [`BACKLOG.md`](../BACKLOG.md) · statut des consignes : [`CONSIGNES.md`](../CONSIGNES.md).

## Où on en est

Les **3 étages se jouent de bout en bout**, de la surface jusqu'à la remontée finale, **en solo et à 2 joueurs** dans l'éditeur. Console sans erreur ; seul avertissement : les manettes *simulées* de l'éditeur ne savent pas vibrer (sans conséquence sur le Quest).

## Mise à jour du 2026-11-06 (après-midi) — branche `feat/coop-et-correctifs`

Retours du premier test à plusieurs casques :
- certaines énigmes se résolvaient seul, alors que les réponses doivent toujours être chez les autres ;
- l'étage 3 était trop court (partie finie en 13 min) ;
- on pouvait traverser un mur et tomber dans le vide ;
- le réseau local ne marchait pas.

### Corrigé

| Problème | Correctif |
|----------|-----------|
| Réseau local : aucun casque ne trouvait l'hôte | Verrou multicast Wi-Fi Android + permissions `CHANGE_WIFI_MULTICAST_STATE` / `ACCESS_WIFI_STATE` (`AbyssesAndroidManifest`). Le client sonde aussi **chaque adresse de son sous-réseau** en unicast, car les partages de connexion ne relaient souvent pas le broadcast. L'hôte affiche son IP, le client la sienne ; recherche de 20 s. |
| Jeu en ligne | La cause de l'échec s'affiche dans le menu. **À faire côté compte** : lier le projet à Unity Cloud (*Project Settings → Services*) et activer Relay + Lobby. |
| Chute dans le vide | Sol de secours invisible sous toute la piste ; `ZoneGuard` ramène au centre du module (fondu au noir) après une chute ou, au joystick, une sortie de zone de plus de 1,5 m. |
| Menu introuvable | Bouton **MENU** sur la montre (poignet gauche), en plus du bouton Menu de la manette. |
| Joueur soulevé par sa propre main | Le corps (CharacterController) ignore le collider du bouton de la montre. |

### Énigmes croisées (voir [`refonte-enigmes.md`](refonte-enigmes.md))

| Étage | Énigme | L'indice est chez… | L'action est chez… |
|-------|--------|--------------------|--------------------|
| 1 | Digicode | le partenaire (code **privé**, illisible par le hublot) | moi |
| 1 | Disjoncteurs | le partenaire (note sur **son** bureau) | moi |
| 2 | Morse | le partenaire (balise devant **son** hublot, radio chez lui) | moi (cadenas + table) |
| 3 | Sonar (Simon) | le partenaire (**récepteur** qui affiche ma séquence) | moi (touches) |
| 1, 2, 3 | **Relais d'appels** (nouveau) | l'appelant (symbole affiché chez lui seul) | la cible (6 boutons-symboles), rôles tirés à chaque appel |

- Relais : 3 manches à l'étage 1 (surtension), 3 à l'étage 2 (fuites), **4 à l'étage 3** (barres de contrôle). Les manches accélèrent (12 s puis 7 s par appel, ×1,5 en mode facile) ; une erreur ou un retard relance la manche.
- Partenaire absent : copie de la note, balise et bips locaux, séquence affichée sur les touches. Le solo reste jouable.
- `PrivateView` : un indice n'est rendu que pour le joueur du module. Le pupitre du maître du jeu voit tout.

### Vérifié

- Partie solo automatique **63/63**, relais joué par de vrais appuis (erreur comprise).
- Partie à 2 plongeurs + pupitre **41/41** : l'appelant n'est jamais la cible ; Simon croisé diffusé entre les deux machines.
- Ergonomie : 253 objets, 0 à revoir. Console sans erreur.
- Captures : `etage1_relais`, `etage2_relais`, `etage2_radio_balise`, `etage3_relais`, `etage3_recepteur`.

### Reste à faire

- **Au casque** : réseau local sur un partage de connexion, durée de partie (objectif 20 à 25 min), lisibilité du relais.
- Rendre les 6 symboles du relais plus difficiles à nommer (variantes, rotations) si c'est trop facile.
- Passe visuelle des objets d'énigme (liseré d'appel, animations de réussite) ; relier le projet à Unity Cloud.

## Mise à jour du 2026-11-06 — branche `feat/surface-et-plongee`

### Audit des branches

| Branche | État |
|---------|------|
| `main` | Passe artistique de Lucas (e4598ab). Ne contient pas encore les améliorations (pas de PR fusionnée). |
| `feat/ameliorations-jeu` | `main` + améliorations + fusion de la passe artistique (b8f153a, local). |
| `feat/test-casque` (Lucas) | Premier test Quest 2 : 57 FPS au lieu de 72, GPU à 98 % → rendu fovéal, résolution 0,9, rotation au stick, « Recommencer ». Fusionnée ici. |
| `feat/surface-et-plongee` | Tout ce qui précède + cette session. **C'est la branche à jour.** |

### Ajouté

| Domaine | Ajout |
|---------|-------|
| Surface (C8) | Plateforme offshore au large d'une île : pont percé d'un puits autour de chaque cabine, garde-corps le long de chaque zone de jeu, potence + câble au-dessus de chaque cabine, mer, 3 îles low poly avec palmiers, phare à faisceau tournant, bateau qui tangue, nuages, mouettes, soleil, ressac |
| Plongée | Première plongée de 22 s vue par les hublots (agrandis à 76 cm) : treuil qui lâche (secousse, vibration), la mer monte et passe les hublots (éclaboussure), l'eau s'assombrit, les piliers défilent ; un puits d'eau masque le chargement de l'étage ; remontée inverse en fin de partie (portes fermées jusqu'à l'émersion). La caméra n'est jamais bougée (confort). |
| Difficulté | Une énigme **par module et par étage**, jamais résolue automatiquement : en solo il y a vraiment de quoi chercher. Disjoncteurs (étage 1, note à lire, court-circuit si on se trompe), morse (étage 2, balise derrière le hublot, table au mur, cadenas), console sonar (étage 3, mémoire 6 notes). Tirages aléatoires à chaque partie. |
| Lisibilité | Vrai porte-fusible (socle céramique, pinces, voyant) et fusible à cartouche ; boutons physiques qui s'enfoncent avec un clic ; textes contrastés ; aide-mémoire des boutons collé aux manettes (surface + 2 premières minutes) |
| Tests sans casque | Déplacement à la manette de jeu (debug, coupé dans l'APK EVA) |

### Vérifié

- Partie solo automatique : 58/58, aucune erreur (les nouvelles énigmes sont résolues par de vrais appuis, erreurs comprises).
- Ergonomie et partie à 2 + pupitre : voir `docs/ergonomie.md` et `docs/playtest-multi.md`.
- Captures : `docs/captures/surface_*.png`, `etage1_disjoncteurs`, `etage1_fusible`, `etage2_morse`, `etage2_balise`, `etage3_sonar`.

### Reste à faire

- **Tester au casque** : FPS avec la surface (une lumière temps réel, maillages fusionnés), confort de la plongée, lisibilité de la note et de la table morse.
- Faire valider par Lucas les ajouts hors consignes (WORKFLOW), puis PR vers `main`.

## Passe artistique (2026-11-05)

Le décor n'est plus en blockout. Tout reste généré par *Abysses → Tout construire + baker l'éclairage* (nouveau fichier `AbyssesArt.cs`), sans aucun asset externe. Captures : [`docs/captures/`](captures/).

- **Matières** : textures PBR générées (couleur + relief) — coque à plaques rivetées, sol en tôle larmée, plafond ondulé, casiers, bandes de danger, écrans animés. Les textures suivent les coordonnées du monde, donc pas d'étirement quelle que soit la taille du mur.
- **Modules** : plinthes, bandeau lumineux à la couleur du joueur, membrures, poutres, conduites avec colliers, appliques murales, luminaires encastrés, cadres arrondis sur les hublots et les sas.
- **Mobilier propre à chaque étage** :
  - habitat : casiers, bureau, caisses, fûts ;
  - labo : paillasse avec fioles, cuves à échantillons lumineuses, baies serveurs ;
  - cœur : bobines en laiton, colonnes de conduites.
- **Cabine bathyscaphe** redessinée : hublots ronds cerclés de laiton, dôme, coins arrondis lumineux, console et main courante.
- **Appareils** (digicode, fusible, manomètre) montés sur des platines cerclées de bandes de danger, avec gaine vers le plafond. Le réacteur est habillé (socle, anneaux, haubans).
- **Lumière** : étage 2 moins monochrome (halos verts dans les cuves, contre le rouge de secours) ; vitres corrigées (elles devenaient blanches sous un angle rasant).
- **Étage 2 (labo)** : coque peinte claire et faïence blanche au lieu de l'acier sombre (le rouge de secours éclaire enfin les murs), hottes avec réglette froide au-dessus des paillasses, armoire à échantillons, fuite de vapeur et sol mouillé.
- **Étage 3 (cœur)** : armoires électriques, cage en laiton qui tourne autour du réacteur, liseré et lumière du réacteur, **balisage au sol à la couleur du joueur** de sa cabine jusqu'à sa console, lumière remontée au niveau de l'étage 1 (un écran LCD de Quest rend mal les scènes très sombres).
- **Corrigé** : bureaux, paillasses et baies serveurs étaient tournés vers le mur.
- **Perf (éditeur)** : 187 à 303 batches selon l'étage, 34 à 56 SetPass, 34 000 à 50 000 triangles, toujours 0 ombre dynamique — détail dans [`optimisation.md`](optimisation.md).

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
| C7 | DA claire et travaillée | 🟡 passe artistique faite, à juger au casque |
| C8 | Bonus « étendre l'univers » | ✅ bathyscaphe, 3 étages |
| C9 | Coopération + avatars visibles | ✅ testé à 2 joueurs |
| C10 | Marge de dérive 50 cm | ✅ |

## Ce qui reste, par priorité

1. **Tester sur un vrai Quest 2** — rien n'a encore tourné sur un casque : performances, confort, volumes sonores, joystick réellement coupé. L'APK est à régénérer pour inclure la passe artistique (*Abysses → Build → APK debug*), puis, casque branché en USB, mode développeur activé :
   ```bash
   /Applications/Unity/Hub/Editor/6000.3.23f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb install -r /Users/Eleve/Desktop/IUTS5/UNITY/TP_Unity/Projet/Builds/Abysses_debug.apk
   ```
2. **Mesurer la perf sur le casque** après la passe artistique (jusqu'à 303 batches à l'étage 2 dans l'éditeur) ; si besoin : occlusion culling, puis fusion des pièces de décor.
3. **Activer Relay** sur cloud.unity.com (projet *eva-vr-escape-sousmarin* → Multiplayer → Relay) pour jouer en ligne en plus du réseau local.
4. **Playtests** avec de vraies personnes : durée, compréhension des énigmes.

## Mise à jour du 2026-11-04 — branche `feat/ameliorations-jeu`

### Ajouté

| Domaine | Ajout |
|---------|-------|
| Lumière (C6) | Étage 1 en éclairage de secours ; à la remise sous tension, les néons se rallument un par un et l'éclairage complet revient (2 bakes échangés, aucune lumière temps réel en plus) |
| DA (C7) | Passe artistique de `main` (textures PBR, habillage, mobilier) ; hublots sur l'océan (neige marine, poissons, bioluminescence, silhouette géante) |
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
