# Setup Unity — Quest 2

## Version

**Unity 6000.3.23f1 (Unity 6.3 LTS)**, déjà installée avec le module Android, et déjà utilisée pour `imprimante3d-vr`.

## Création du projet (Unity Hub)

- Template : **Universal 3D** (URP)
- Emplacement : `UNITY/TP_Unity/` → nom `Projet`

## Packages

| Package | Version de réf. |
|---------|-----------------|
| XR Plugin Management | 4.6.x |
| OpenXR (+ feature group **Meta Quest**) | 1.16.x |
| XR Interaction Toolkit (+ Starter Assets) | 3.3.x |
| Input System | 1.20.x |
| URP | 17.3.x |
| ProBuilder (blockout) | dernière |
| _Multi seulement_ : Netcode for GameObjects + Multiplayer Services | dernière |

## Build Settings / Player

- Plateforme : **Android** · Texture compression **ASTC**
- Scripting backend **IL2CPP**, architecture **ARM64** uniquement
- Graphics API : **Vulkan** · Color space **Linear**
- Minimum API level : 29 (Android 10)
- XR : OpenXR → Interaction profile **Oculus Touch Controller**, Meta Quest Support coché
- Render mode **Single Pass Instanced** (multiview)

## Réglages URP pour Quest 2

- MSAA 4x, Render scale 1.0, HDR **off**
- Pas de SSAO, pas de Depth/Opaque texture si inutile
- Post-process minimal (Color grading LUT oui, Bloom léger à mesurer, pas de DoF/Motion blur)
- Ombres : 1 lumière principale, distance d'ombre courte, soft shadows off
- Éclairage **baked** (lightmaps + light probes + reflection probes) → c'est là qu'on gagne l'ambiance à coût quasi nul
- Fog URP pour la profondeur d'ambiance (très bon rapport qualité/coût)

## Budgets perf (cibles Quest 2 @72 Hz)

- ≤ 100–150 draw calls (SetPass) · ≤ 300–500 k triangles visibles
- 13,9 ms/frame max · 0 GC alloc par frame en jeu
- Textures 1024–2048 max, atlas quand possible

## État appliqué (2026-10-16)

| Élément | Valeur |
|---------|--------|
| Produit / ID | `EVA Abysses` · `com.iuttroyes.evaabysses` · v0.1.0 |
| Android | IL2CPP, ARM64, Vulkan seul, ASTC, min API 29, target auto, stripping Medium, Run In Background |
| XR | OpenXR (loader Android) · Meta Quest Support · Oculus Touch Controller Profile · Foveated Rendering · Single Pass Instanced · Depth 16 bit · Latency = Prioritize Input Polling |
| URP | Un seul niveau de qualité **Mobile** (`Mobile_RPAsset`) : HDR off, MSAA 4x, render scale 1, ombres principales 1024 / 20 m / 1 cascade, soft shadows off, 2 lumières add. par objet sans ombres, SRP Batcher on. Profil PC supprimé. |
| Samples XRI | Starter Assets + **XR Interaction Simulator** (test dans l'éditeur : pas de Quest Link sur macOS) |
| Validation | OpenXR + XR Project Validation : 0 erreur, 0 avertissement |

> MCP : l'app Claude se connecte en **Stdio**. Après un rechargement, le bridge peut passer de 6400 à 6401 automatiquement — c'est normal.
