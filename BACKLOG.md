# BACKLOG — Projet VR EVA : Escape game sous-marin MULTIJOUEUR

> Suivi de progression. À mettre à jour **à chaque tâche terminée**.
> Statuts : `[ ]` à faire · `[~]` en cours · `[x]` fait · `[!]` bloqué
> Exécutant : **CC** = Claude Code (MCP Unity) · **OC** = OpenCode · **AG** = Antigravity · **B** = binôme

## Où on en est

- **Dernière session** : 2026-10-16 — fondations + étage 1 jouable de bout en bout dans l'éditeur (solo)
- **Prochaine action** : test multi dans l'éditeur (Multiplayer Play Mode, 2 joueurs) ; puis étages 2 et 3 (énigmes) ; puis passe artistique/lumière de l'étage 1
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
- [ ] Inviter le binôme comme collaborateur (B)
- [ ] Build « hello VR » sur le Quest 2 (valider la chaîne de build) — **en attente d'un casque**

## Phase 1 — Cadrage & conception

- [x] Scénario de l'escape game coop → `docs/game-design.md` (première version, à relire par le binôme)
- [x] Plan au sol : 4 quadrants 15 × 8 m, murs en retrait de 30 cm, alarme de pression si la tête sort de la zone sûre (C2, C4, C10) — salle commune de l'étage 3 à faire
- [ ] Moodboard DA + palette lumière par profondeur (C6, C7)
- [ ] Répartition des tâches dans le binôme

## Phase 2 — Fondations techniques

- [x] Packages réseau : Netcode for GameObjects 2.13.3, Multiplayer Services 2.4.0, Multiplayer Play Mode 2.0.2 (CC)
- [~] Projet lié à Unity Cloud (`eva-vr-escape-sousmarin`, org lucascorrieras06) — reste à activer Relay dans le dashboard (utilisateur)
- [x] Menu de connexion VR : En ligne (Relay, sans saisie de code) / Héberger LAN / Rejoindre LAN (découverte UDP auto) / Solo (CC)
- [x] Attribution d'un quadrant (0–3) par joueur + calibration de l'origine (CC) — vérifié en Play Mode
- [x] Avatars réseau légers (casque + gants, couleur par quadrant, NetworkTransform propriétaire) (CC)
- [x] Objets/leviers synchronisés (ownership au grab, sas à objets, leviers synchronisés) (CC)
- [ ] Test multi dans l'éditeur (Multiplayer Play Mode) puis 2 casques (CC + B)
- [x] Repli mono-joueur 15 × 8 m fonctionnel (même build, session solo, auto-résolution + écran de surveillance + fusible rapatrié) (CC) — vérifié en Play Mode
- [x] XR Origin + interactions (grab, poke, levier, bouton) + simulateur XR dans l'éditeur (EditorOnly)
- [x] Locomotion joystick derrière un flag, **off en build prod** via define `ABYSSES_PROD` ; téléportation/rotation au stick toujours coupées (C3)
- [ ] Recentrage / calibration de l'origine sur la piste
- [x] Système d'ascenseur : cabines fixes, étages en scènes additives synchronisées, portes/bulles/profondeur (C8) — vérifié en Play Mode
- [x] Gestionnaire d'énigmes / états de progression (GameStateManager + PuzzleObjective, événements)
- [~] Blockout à l'échelle 1:1 généré par `Abysses → Tout construire` : étage 1 complet, étages 2 et 3 = coques vides

## Phase 3 — Contenu & ambiance

- [~] Énigmes : étage 1 fait (digicode croisé, fusible par le sas, leviers synchronisés) ; étages 2 et 3 à faire
- [ ] Lighting baked par niveau + light/reflection probes (C6)
- [ ] Ambiance sous-marine bon marché : fog URP, caustiques en light cookie animé, god rays sur fausses cartes additives, particules de plancton
- [ ] Sound design (ambiance, bulles, sonar), audio spatialisé
- [ ] Feedbacks : haptique des manettes, sons, lumière sur les énigmes réussies

## Phase 4 — Optimisation (Quest 2, 72 FPS)

- [ ] Relevé « avant » : FPS, draw calls, triangles, GC alloc → `docs/optimisation.md`
- [ ] Static batching / GPU instancing / atlas
- [ ] Occlusion culling par niveau ; un seul niveau actif à la fois (chargement additif)
- [ ] Compression ASTC, tailles de textures, LOD
- [ ] Scripts sans alloc par frame
- [ ] Relevé « après » sur le casque

## Phase 5 — Qualité & rendu

- [ ] Playtests (confort, durée, compréhension des énigmes)
- [ ] Vérifier chaque ligne de `CONSIGNES.md`
- [ ] Build **prod EVA** (joystick off) + build debug
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
