# BACKLOG — Projet VR EVA : Escape game sous-marin

> Suivi de progression. À mettre à jour **à chaque tâche terminée**.
> Statuts : `[ ]` à faire · `[~]` en cours · `[x]` fait · `[!]` bloqué
> Exécutant : **CC** = Claude Code (MCP Unity) · **OC** = OpenCode · **AG** = Antigravity · **B** = binôme

## Où on en est

- **Dernière session** : 2026-10-16 — énoncé lu, décisions de cadrage prises
- **Prochaine action** : créer le projet Unity 6000.3.23f1 (Universal 3D) dans `TP_Unity/Projet`, puis lancer le MCP
- **Bloquants** : date de rendu, format de rendu et casque EVA de prod inconnus

## Décisions

| Date | Décision | Raison |
|------|----------|--------|
| 2026-10-16 | **Escape game mono-joueur**, piste entière 31 × 16,5 m | Équilibre note/risque |
| 2026-10-16 | Projet en **binôme** | — |
| 2026-10-16 | DA **sous-marine / fonds marins** | Ambiance forte, lumière centrale |
| 2026-10-16 | Extension du monde : **ascenseur / bathyscaphe** entre niveaux de profondeur | Bonus C8 + gradient de lumière par profondeur |
| 2026-10-16 | Unity **6000.3.23f1** (6.3 LTS), URP, OpenXR + XRI 3.3 | Installé + module Android + déjà validé sur Quest |

## Phase 0 — Mise en place

- [x] Dossier + fichiers de suivi (CC)
- [x] Lecture énoncé → `CONSIGNES.md` (CC)
- [ ] Créer le projet Unity via Hub (utilisateur)
- [ ] Lancer le serveur MCP Unity + vérifier la connexion (CC)
- [ ] Packages + Player Settings Quest 2 selon `docs/setup-unity.md` (CC)
- [~] `git init` + `.gitignore` Unity + Git LFS (fait) · repo GitHub partagé avec le binôme (à faire)
- [ ] Build « hello VR » sur le Quest 2 (valider la chaîne de build)

## Phase 1 — Cadrage & conception

- [ ] Scénario de l'escape game (histoire, nombre de niveaux, énigmes) → `docs/game-design.md`
- [ ] Plan au sol 31 × 16,5 m + limites de zone originales (C2, C4)
- [ ] Moodboard DA + palette lumière par profondeur (C6, C7)
- [ ] Répartition des tâches dans le binôme

## Phase 2 — Fondations techniques

- [ ] XR Origin + interactions (grab, poke, levier, bouton)
- [ ] Locomotion joystick derrière un flag, **off en build prod** (C3)
- [ ] Recentrage / calibration de l'origine sur la piste
- [ ] Système d'ascenseur : changement de niveau sans déplacement réel (C8)
- [ ] Gestionnaire d'énigmes / états de progression (événements, pas de polling)
- [ ] Blockout ProBuilder de chaque niveau à l'échelle 1:1

## Phase 3 — Contenu & ambiance

- [ ] Énigmes niveau par niveau
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
- **2026-10-16** — Dépôt Git local initialisé (LFS + Smart Merge Unity), premier commit.
