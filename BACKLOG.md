# BACKLOG — Projet VR EVA : Escape game sous-marin MULTIJOUEUR

> Suivi de progression. À mettre à jour **à chaque tâche terminée**.
> Statuts : `[ ]` à faire · `[~]` en cours · `[x]` fait · `[!]` bloqué
> Exécutant : **CC** = Claude Code (MCP Unity) · **OC** = OpenCode · **AG** = Antigravity · **B** = binôme

## Où on en est

> Bilan complet de la dernière session : [`docs/etat-du-projet.md`](docs/etat-du-projet.md)

- **Dernière session** : 2026-11-03 — audit + corrections : multijoueur testé à 2 dans l'éditeur (3 bugs réseau corrigés), calibration guidée, menu en jeu, nouvelle plongée, vibrations, chaîne de build APK
- **Prochaine action** : build APK debug + premier test sur Quest 2 (mesures de perf sur casque) ; passe artistique sur la géométrie (blockout → décor) ; playtests
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
- [ ] Moodboard DA + palette lumière par profondeur (C6, C7)
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

## Phase 4 — Optimisation (Quest 2, 72 FPS)

- [~] Relevé « avant » dans l'éditeur → `docs/optimisation.md` (154 draw calls, 29 SetPass, 22 k triangles, 0 ombre dynamique) ; **à refaire sur le casque**
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
