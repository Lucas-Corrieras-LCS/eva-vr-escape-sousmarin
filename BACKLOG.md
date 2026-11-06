# BACKLOG — Projet VR EVA : Escape game sous-marin MULTIJOUEUR

> Suivi de progression. À mettre à jour **à chaque tâche terminée**.
> Statuts : `[ ]` à faire · `[~]` en cours · `[x]` fait · `[!]` bloqué
> Exécutant : **CC** = Claude Code (MCP Unity) · **OC** = OpenCode · **AG** = Antigravity · **B** = binôme

## Où on en est

> Bilan complet de la dernière session : [`docs/etat-du-projet.md`](docs/etat-du-projet.md)

- **Session 2026-11-06** (branche `feat/surface-et-plongee`) : départ en surface (plateforme offshore, île, phare), vraie plongée animée (secousse, mer qui monte, puits d'eau), 3 énigmes de module jouables seul (disjoncteurs, morse, sonar), vrai porte-fusible, boutons lisibles, aide-mémoire des manettes, manette de jeu (debug) ; fusion de `feat/test-casque`
- **Session précédente** : 2026-11-05 — **passe artistique** : textures PBR procédurales (coque rivetée, tôle larmée, plafond ondulé, faïence), habillage des modules, mobilier par étage, cabine bathyscaphe redessinée, supports d'appareils ; étages 2 et 3 finis (identité labo, réacteur animé, balisage au sol)
- **Prochaine action** : régénérer l'APK et premier test sur Quest 2 (perf réelle : 187 à 303 batches selon l'étage dans l'éditeur) ; playtests
- **Bloquants** : pas de casque pour tester pour l'instant (tout est validé dans l'éditeur + simulateur XR) ; date/format de rendu inconnus ; Relay à activer dans le dashboard Unity

## Décisions

| Date | Décision | Raison |
|------|----------|--------|
| 2026-10-16 | ~~Escape game mono-joueur, piste entière~~ → remplacé | — |
| 2026-10-16 | **Escape game MULTIJOUEUR** (★★★★★), **15 × 8 m par joueur**, 4 max, même monde virtuel. Repli autorisé : mono-joueur en 15 × 8 m | Choix du binôme, barème technique |
| 2026-10-16 | Projet en **binôme** | — |
| 2026-10-16 | DA **sous-marine / fonds marins** | Ambiance forte, lumière centrale |
| 2026-10-16 | Extension du monde : **ascenseur / bathyscaphe** entre niveaux de profondeur | Bonus C8 + gradient de lumière par profondeur |
| 2026-10-16 | **2 à 4 joueurs** (jouable dès 2, extensible à 4) | Testable avec les casques de l'IUT |
| 2026-10-16 | Réseau : **Netcode for GameObjects + Multiplayer Services (Sessions)**. **Relay** (code de session, via Internet) par défaut + **LAN direct** en secours | Wi-Fi école/EVA : isolation client possible OU pas d'Internet → les deux couverts |
| 2026-10-16 | Pas de chat vocal : les joueurs sont dans la même salle physique | Économie perf/complexité |
| 2026-10-16 | Disposition **mixte évolutive** : chacun dans son compartiment (vision par hublots, échanges par sas), puis **réunion finale** via l'ascenseur dans une salle commune | Coop C9 + effet « waouh » final |
| 2026-10-16 | Monde virtuel calqué sur la piste : chaque joueur a son quadrant 15 × 8 m ; la salle commune juxtapose les 4 quadrants séparés par des barrières infranchissables (vitre, gouffre…) avec marge ≥ 50 cm | Aucune collision physique possible (C10) |
| 2026-10-16 | Unity **6000.3.23f1** (6.3 LTS), URP, OpenXR + XRI 3.3 | Installé + module Android + déjà validé sur Quest |

## Phase 0 — Mise en place

- [x] Dossier + fichiers de suivi (CC)
- [x] Lecture énoncé → `CONSIGNES.md` (CC)
- [x] Créer le projet Unity via Hub → `Projet/` (utilisateur)
- [x] Serveur MCP Unity connecté — transport **Stdio** (port 6400), instance `Projet` (CC)
- [x] Packages + Player Settings + OpenXR + URP Quest 2 selon `docs/setup-unity.md` (CC)
- [x] Dépôt Git (LFS + Smart Merge) → https://github.com/Lucas-Corrieras-LCS/eva-vr-escape-sousmarin (public)
- [x] Binôme (Jeremy) collaborateur du dépôt
- [~] Build Quest 2 : APK debug produit sans erreur (`Projet/Builds/Abysses_debug.apk`) — **reste à l'installer sur un casque**

## Phase 1 — Cadrage & conception

- [x] Scénario de l'escape game coop → `docs/game-design.md` (première version, à relire par le binôme)
- [x] Plan au sol : 4 quadrants 15 × 8 m, murs en retrait de 30 cm, alarme de pression si la tête sort de la zone sûre ; salle commune vitrée à l'étage 3 (C2, C4, C10)
- [x] Palette lumière par profondeur (C6, C7) : cyan habitat → rouge de secours + halos verts labo → bleu nuit cœur + violet ; bandeau à la couleur du joueur
- [ ] Répartition des tâches dans le binôme

## Phase 2 — Fondations techniques

- [x] Packages réseau : Netcode for GameObjects 2.13.3, Multiplayer Services 2.4.0, Multiplayer Play Mode 2.0.2 (CC)
- [~] Projet lié à Unity Cloud (`eva-vr-escape-sousmarin`, org lucascorrieras06) — reste à activer Relay dans le dashboard (utilisateur)
- [x] Menu de connexion VR : En ligne (Relay, sans saisie de code) / Héberger LAN / Rejoindre LAN (découverte UDP auto) / Solo (CC)
- [x] Attribution d'un quadrant (0–3) par joueur + calibration de l'origine (CC) — vérifié en Play Mode
- [x] Avatars réseau légers (casque + gants, couleur par quadrant, NetworkTransform propriétaire) (CC)
- [x] Objets/leviers synchronisés (ownership au grab, sas à objets, leviers synchronisés) (CC)
- [x] Test multi dans l'éditeur à 2 joueurs (Multiplayer Play Mode, *Abysses → Test multijoueur*) : partie complète surface → fin ; 3 bugs corrigés (fusible posé visible chez les autres, sas qui renvoyait l'objet au départ, chrono de fin à 0 chez le client) ; arrivée en cours de partie refusée (CC)
- [ ] Test sur 2 casques (CC + B)
- [x] Repli mono-joueur 15 × 8 m fonctionnel (même build, session solo, auto-résolution + écran de surveillance + fusible rapatrié) (CC) — vérifié en Play Mode
- [x] XR Origin + interactions (grab, poke, levier, bouton) + simulateur XR dans l'éditeur (EditorOnly)
- [x] Locomotion joystick derrière un flag, **off en build prod** via define `ABYSSES_PROD` ; téléportation/rotation au stick toujours coupées (C3)
- [x] Calibration guidée : consigne dans le menu, départ impossible tant qu'un joueur n'est pas dans sa cabine, porte qui se ferme quand le joueur est à bord, « Recalibrer » accessible en partie (bouton Menu manette gauche) (CC)
- [x] Système d'ascenseur : cabines fixes, étages en scènes additives synchronisées, portes/bulles/profondeur (C8) — vérifié en Play Mode
- [x] Gestionnaire d'énigmes / états de progression (GameStateManager + PuzzleObjective, événements)
- [x] Blockout à l'échelle 1:1 généré par `Abysses → Tout construire` : 3 étages complets

## Phase 3 — Contenu & ambiance

- [x] Énigmes : étage 1 (digicode croisé, fusible par le sas, leviers en dernier) ; étage 2 (vannes croisées en anneau, lampe UV unique + sas verticaux) ; étage 3 (salle commune vitrée, séquence du réacteur, leviers de remontée, final lumineux) — tous testés en Play Mode solo
- [x] Lighting baked par niveau + light/reflection probes (C6) : lumières des modules en Baked (ombres douces, AO, 2 rebonds), 336 light probes + 4 reflection probes box-projetées par étage, bake GPU ~10 s/étage via `Abysses → Tout construire + baker l'éclairage` (CC) — vérifié sur captures, pas encore en jeu
- [x] Ambiance sous-marine bon marché : fog URP, caustiques (cookie animé 16 images sur une directionnelle sans ombres, étage 1), plancton par module (cyan / braises / bioluminescence), gyrophares de secours à faisceaux additifs (étage 2), rayon de lumière révélé au final (étage 3) (CC)
- [x] Sound design procédural (`AbyssesContent`, aucun asset externe) : 4 ambiances en boucle (station, habitat, labo, cœur), ascenseur, alarme de zone, digicode (touche/erreur/succès), fusible, sas, leviers ratés, réacteur, étage résolu, final ; spatialisé sur les objets (CC) — volumes à régler au casque
- [x] Feedbacks : sons, lumière et **vibrations** (énigme réussie, mauvais code, leviers ratés, réacteur, descente, sortie de zone) (CC)
- [x] Symboles du digicode : formes + couleurs (● rouge, ▲ vert, ■ bleu, ★ jaune, ◆ blanc, ✚ violet) sur touches, saisie et codes muraux (CC)
- [x] Sons : ambiance, bulles, grondement de l'ascenseur, alarme de zone (CC)
- [x] Captures automatiques des étages → `docs/captures/` (`Abysses → Captures d'écran des étages`) (CC)
- [x] **Passe artistique** (`AbyssesArt.cs` + générateur) : textures PBR procédurales albédo + normal map (coque rivetée, tôle larmée, plafond ondulé, casiers, marquage danger, écrans), UV en coordonnées monde (pas d'étirement), cadres arrondis sur hublots et sas, plinthes/bandeau couleur du joueur/membrures/poutres/conduites/appliques, luminaires encastrés, mobilier par étage (casiers, bureaux, caisses, fûts, paillasses, cuves à échantillons, baies serveurs, bobines), cabine bathyscaphe (hublots laiton, dôme, coins arrondis lumineux), supports des appareils (digicode, fusible, manomètre), habillage du réacteur (CC) — vérifié sur captures + Play Mode solo
- [x] Finition étages 2 et 3 : labo (coque claire + faïence, hottes à réglette froide, armoires à échantillons, vapeur, sol mouillé) ; cœur (armoires électriques, cage tournante du réacteur, lumière du réacteur, balisage au sol vers la console) (CC) — Play Mode solo jusqu'à l'étage 3

### Améliorations (branche `feat/ameliorations-jeu`, 2026-11-04) — à valider au casque et par le binôme

- [x] Vibrations partout : crans et butées de vanne, levier en butée, touches, fusible enclenché, sas, arrivée de l'ascenseur (CC)
- [x] Objets perdus qui reviennent à leur dernier emplacement sûr (`LostItemReturn`) (CC)
- [x] Étage 1 : départ en éclairage de secours, néons qui se rallument un par un à la remise sous tension (2 bakes échangés, `PowerRestore`) (CC)
- [x] Hublots sur l'océan : neige marine, banc de poissons (-200 m), leurres lumineux (-1 500 m), bioluminescence (-4 000 m), silhouette géante (CC)
- [x] Coque habillée : remplacée par la passe artistique (ligne ci-dessus) lors de la fusion de `main` ; les hublots sur l'océan sont conservés (CC)
- [x] Station vivante : réverbération par module, coque qui grince, gouttes ; étage 2 : vapeur qui siffle, néon défaillant, eau qui monte (CC)
- [x] Descente : oreilles qui se bouchent (filtre audio), traînées derrière les hublots, choc d'arrivée (CC)
- [x] Montre de plongée au poignet (profondeur, O2, chrono) + indices progressifs de l'IA de la station (CC)
- [x] Pupitre du maître du jeu / écran spectateur (PC, F1 ou `-pupitre`) : suivi, indice, message, résoudre, passer l'étage, relancer, caméras, vitrine (CC)
- [x] Mode facile, classement des équipes, langue FR / EN (CC)
- [x] Reconnexion automatique d'un casque (retrouve son quadrant, 20 s de grâce côté serveur) (CC)
- [x] Avatars : buste, bouteilles, bras ; surbrillance des objets au survol de la main (CC)
- [x] Tutoriel dans la cabine (bouton, fusible, prise), plaques de coque à frapper (morse), journaux de bord de l'équipage (CC)
- [x] Vérification d'ergonomie automatique → `docs/ergonomie.md` (CC)
- [x] Relecture complète + parties de test automatisées : solo (36/36) et 2 plongeurs + pupitre avec reconnexion (28/28), 0 erreur ; 6 bugs corrigés dont 1 existant (étage resté chargé après avoir quitté) (CC)
- [ ] Hors de portée pour l'instant : spatialiseur Meta XR Audio, vrais bruitages libres de droits, énigmes morse / sonar / grue, Application SpaceWarp, modèles 3D importés

## Phase 4 — Optimisation (Quest 2, 72 FPS)

- [~] Relevé « avant » dans l'éditeur → `docs/optimisation.md` (154 draw calls, 29 SetPass, 22 k triangles, 0 ombre dynamique) ; après passe artistique : étage 1 = 266 batches / 40 SetPass / 41 k tri, étage 2 = 303 / 56 / 50 k, étage 3 = 187 / 34 / 34 k ; **à refaire sur le casque**
- [x] Static batching actif (60 draw calls regroupés), matériaux partagés SRP Batcher
- [~] Un seul étage chargé à la fois (fait) ; occlusion culling à tester si la mesure casque dépasse le budget
- [ ] Compression ASTC, tailles de textures, LOD
- [ ] Scripts sans alloc par frame
- [ ] Relevé « après » sur le casque

## Phase 5 — Qualité & rendu

- [ ] Playtests (confort, durée, compréhension des énigmes)
- [~] Vérifier chaque ligne de `CONSIGNES.md` : tableau des statuts à jour (2026-11-03) ; C5/C6/C7 à valider au casque
- [~] Chaîne de build : *Abysses → Build → APK debug / APK EVA* (define `ABYSSES_PROD`) ; **APK debug produit sans erreur (67 Mo, 7 min)** ; reste : installer sur le Quest 2, produire l'APK EVA
- [ ] Livrables selon le format demandé

## Tâches déléguées

| Tâche | Outil | Commande / session | Statut |
|-------|-------|--------------------|--------|
|       |       |                    |        |

## Journal

- **2026-10-16** — Dossier créé. Énoncé lu. Choix : escape game mono, binôme, sous-marin, ascenseur, Unity 6.3 LTS.
- **2026-10-16** — Dépôt Git initialisé (LFS + Smart Merge Unity) et publié sur GitHub (public).
- **2026-10-16** — Projet Unity créé (6000.3.23f1, Universal 3D) et déplacé dans `Projet/`. MCP connecté en Stdio (le mode HTTP 8080 n'est pas vu par l'app Claude).
- **2026-10-16** — Config Quest 2 : OpenXR 1.16.1 (Meta Quest + Touch, Single Pass Instanced, foveated rendering), XRI 3.3.2 (Starter Assets + Interaction Simulator), ProBuilder 6.1.3, Android IL2CPP ARM64 Vulkan ASTC, URP Mobile unique (HDR off, MSAA 4x, ombres 20 m). Validation XR/OpenXR : 0 erreur.
- **2026-10-16** — Pivot vers l'escape game **multijoueur** (2–4 joueurs, 15 × 8 m chacun). Réseau NGO + Relay/LAN, disposition mixte évolutive.
- **2026-10-16** — Game design v1. 23 scripts (réseau, jeu, énigmes, UI) + générateur de scènes `Abysses → Tout construire`. Partie solo testée de bout en bout en Play Mode : connexion, descente, étage 1 résolu (digicode, fusible, leviers), ascenseur, fin à -4000 m. Bug corrigé : identifiants réseau à 0 sur les objets générés par script.
- **2026-10-16** — Étages 2 et 3 construits et testés en Play Mode (solo) de la surface jusqu'au final. Leviers = dernière étape (prérequis). Couleur jaune → vert (confusion avec l'orange). Fusible consommé via Despawn(false).
- **2026-10-16** — Passe ambiance (branche `feat/ambiance-son-lumiere`, travail hors MCP : générateur relancé en batchmode, console sans erreur ni avertissement). Éclairage baké + probes, caustiques, plancton, gyrophares, rayon final, 17 sons procéduraux, digicode formes + couleurs, captures auto. Corrigé au passage : coque trop métallique (les lightmaps n'avaient presque aucun effet), lampe de cabine qui éclairait le plafond du module (isolée par rendering layer), intensités recalées pour l'atténuation physique du bake.
- **2026-11-03** — Pull de la passe ambiance de Jeremy (vérifiée en jeu : lightmaps, 336 light probes, caustiques, sons). Audit + corrections : calibration guidée et départ conditionné à la présence en cabine, menu en jeu (bouton Menu), « Nouvelle plongée », refus des arrivées en cours de partie, vibrations, menu de build APK debug/EVA, test multijoueur MPPM 2 joueurs (Host/Client auto) → 3 bugs réseau corrigés, matériaux sans faux diff, menu « Reconstruire la Station seulement ».
- **2026-11-04** — Branche `feat/ameliorations-jeu` : vibrations, objets perdus, panne de courant (2 bakes), hublots sur l'océan, station vivante, montre de plongée + indices, pupitre MJ / spectateur, mode facile, classement, FR/EN, reconnexion, avatars complets, surbrillance, tutoriel de cabine, plaques à frapper, journaux de bord, vérification d'ergonomie.
- **2026-11-05** — Passe artistique (le décor n'est plus en blockout). Nouveau `AbyssesArt.cs` : textures PBR générées (albédo + normal map), meshes en UV monde, cadres arrondis. Habillage complet des modules, mobilier propre à chaque étage, cabine redessinée, supports d'appareils, réacteur. Corrigé : vitres blanches sous angle rasant (reflets d'environnement + specular non multiplié par l'alpha), montants qui masquaient le réacteur, support de digicode qui chevauchait le bandeau. Étage 2 : halos verts dans les cuves pour casser le tout-rouge. Perf éditeur : 266 batches / 40 SetPass / 41 k triangles. Play Mode solo OK (descente, étage 1).
- **2026-11-05** — Finition des étages 2 et 3. Labo : coque peinte claire + faïence (le rouge de secours n'est plus absorbé), hotte avec réglette froide au-dessus des paillasses, armoire à échantillons, fuite de vapeur + sol mouillé, lumière un peu moins saturée. Cœur : armoires électriques, 2e baie, câbles des bobines, cage tournante autour du réacteur, liseré + lumière bakée du réacteur, balisage au sol à la couleur du joueur (cabine → console), lumière remontée au niveau de l'étage 1, montants vitrés décalés de l'axe de la cabine. Corrigé : bureaux, paillasses et baies étaient tournés vers le mur. Perf éditeur : étage 2 = 303 batches / 56 SetPass / 50 k triangles, étage 3 = 187 / 34 / 34 k. Partie solo jouée jusqu'à l'étage 3 sans erreur.
- **2026-11-05** — Fusion de `main` (passe artistique) dans `feat/ameliorations-jeu` : l'habillage et les textures de Lucas remplacent mon kit de modules et ma coque texturée ; hublots sur l'océan repositionnés aux deux bouts du mur extérieur (0,8 m, entre poteau d'angle et mobilier) ; à l'étage 1 les appliques servent d'éclairage de secours (ambre pendant la panne) ; main courante de la cabine descendue à 0,85 m (tutoriel).
- **2026-11-06** — Branche `feat/surface-et-plongee` (depuis `feat/ameliorations-jeu` + fusion de `feat/test-casque`). Surface : plateforme offshore (pont, puits autour des cabines, garde-corps par quadrant, potences + câbles), mer, 3 îles low poly avec palmiers, phare à faisceau tournant, bateau, nuages, mouettes, soleil (seule lumière temps réel, couche de rendu dédiée), sons (ressac, mouettes, treuil, éclaboussure). Plongée : 22 s pour la première (treuil, enfoncement, mer qui passe les hublots, eau qui s'assombrit, puits d'eau qui masque le chargement), descentes suivantes masquées, remontée (portes fermées jusqu'à l'émersion). Hublots de cabine agrandis (76 cm). Énigmes de module (non résolues automatiquement en solo) : disjoncteurs (2 interdits tirés au hasard, note sur le bureau, court-circuit), morse (balise derrière le hublot + haut-parleur, table au mur, cadenas à 3 molettes), sonar (Simon 6 notes, 4 en facile). Porte-fusible céramique + pinces + voyant, fusible à cartouche. Boutons : chapeau qui s'enfonce + clic, textes contrastés. Aide-mémoire collé aux manettes. Manette de jeu (debug).
