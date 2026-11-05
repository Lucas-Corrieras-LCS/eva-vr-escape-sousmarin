# État du projet — ABYSSES

> Bilan des sessions du **2026-11-03** (audit, corrections, ajouts) et du **2026-11-05** (passe artistique).
> Suivi détaillé tâche par tâche : [`BACKLOG.md`](../BACKLOG.md) · statut des consignes : [`CONSIGNES.md`](../CONSIGNES.md).

## Où on en est

Les **3 étages se jouent de bout en bout**, de la surface jusqu'à la remontée finale, **en solo et à 2 joueurs** dans l'éditeur. Console sans erreur ; seul avertissement : les manettes *simulées* de l'éditeur ne savent pas vibrer (sans conséquence sur le Quest).

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
- **Perf (éditeur)** : 266 batches, 40 SetPass, 41 000 triangles, toujours 0 ombre dynamique — détail dans [`optimisation.md`](optimisation.md).

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
2. **Mesurer la perf sur le casque** après la passe artistique (266 batches dans l'éditeur) ; si besoin : occlusion culling, puis fusion des pièces de décor.
3. **Activer Relay** sur cloud.unity.com (projet *eva-vr-escape-sousmarin* → Multiplayer → Relay) pour jouer en ligne en plus du réseau local.
4. **Playtests** avec de vraies personnes : durée, compréhension des énigmes.
