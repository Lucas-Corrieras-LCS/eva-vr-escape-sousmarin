# Optimisation — Quest 2 (cible 72 FPS)

## Budgets

| Indicateur | Budget Quest 2 |
|------------|----------------|
| Temps de frame | ≤ 13,9 ms |
| Draw calls | ≤ 100–150 |
| SetPass calls | le plus bas possible (< 50) |
| Triangles visibles | ≤ 300–500 k |
| Allocations GC par frame | 0 |

## Relevé « avant » — éditeur (2026-11-03)

Mesuré en Play Mode dans l'éditeur (Mac, simulateur XR, rendu mono), joueur dans sa cabine, portes ouvertes sur l'**étage 1** (éclairage baké, caustiques, plancton).
⚠️ À refaire **sur le casque** (profilage à distance avec l'APK debug) : c'est la seule mesure qui compte pour la note.

| Indicateur | Valeur | Verdict |
|------------|--------|---------|
| Draw calls | 154 | limite haute du budget |
| Batches | 143 | — |
| SetPass calls | 29 | ✅ |
| Triangles | 21 800 | ✅ très large marge |
| Sommets | 18 500 | ✅ |
| Draw calls regroupés (static batching) | 60 | ✅ batching actif |
| Ombres dynamiques (shadow casters) | 0 | ✅ tout est baké |

## Relevé après la passe artistique — éditeur (2026-11-05)

Mêmes conditions (étage 1, joueur dans sa cabine, porte ouverte).

| Indicateur | Avant | Après | Verdict |
|------------|-------|-------|---------|
| Draw calls | 154 | 361 | au-dessus du budget prudent ; voir ci-dessous |
| Batches | 143 | 266 | dans la fourchette courante Quest 2 (≈ 200–300), à confirmer au casque |
| SetPass calls | 29 | 40 | ✅ |
| Triangles | 21 800 | 40 600 | ✅ très large marge |
| Draw calls regroupés (static batching) | 60 | 245 | ✅ |
| Ombres dynamiques | 0 | 0 | ✅ |

Après la finition des étages 2 et 3 (même mesure, joueur dans sa cabine à chaque étage) :

| Étage | Batches | SetPass | Triangles |
|-------|---------|---------|-----------|
| 1 — Habitat | 266 | 40 | 41 k |
| 2 — Labo | 303 | 56 | 50 k |
| 3 — Cœur | 187 | 34 | 34 k |

L'étage 2 est le plus chargé (4 modules fermés mais tous rendus, 5 matériaux de plus). C'est lui qu'il faut mesurer en premier sur le casque.

Le décor ajoute beaucoup de petites pièces (membrures, colliers, appliques, mobilier). Elles partagent une quinzaine de matériaux : le **SRP Batcher** (actif) rend chaque draw call bon marché, et seul le nombre de SetPass coûte vraiment (40). Les textures générées font 128 à 512 px (≈ 2 Mo au total) et sont compressées en ASTC (format par défaut du build Android).
Si le casque ne tient pas 72 FPS : occlusion culling d'abord (les murs pleins cachent les modules voisins), puis fusion des pièces de décor par module (`Mesh.CombineMeshes` à la génération).

## Ce qui est déjà en place

- **Éclairage baké** par étage (lightmaps + 336 light probes + reflection probes) ; lumières temps réel limitées à la cabine, la lampe UV, le rayon final.
- **Un seul étage chargé à la fois** (scènes additives, l'étage précédent est déchargé pendant la descente).
- **Static batching** sur toute la géométrie de décor (murs, sols, panneaux).
- **Matériaux partagés** (SRP Batcher) : les avatars changent de matériau (4 matériaux partagés), pas de copie.
- **Scripts** : événements plutôt que polling ; vérifications périodiques (présence en cabine 5 Hz, limite de zone 10 Hz, lampe UV 10 Hz) ; textes mis à jour seulement quand la valeur change ; aucune allocation par frame dans les `Update`.
- **Réseau** : 30 ticks/s, poses des avatars en NetworkTransform avec compression des rotations ; objets physiques simulés uniquement par leur propriétaire.
- **Quest** : Vulkan, Single Pass Instanced (multiview), rendu fovéal, MSAA 4x, HDR désactivé, ASTC.

## Pistes si la mesure sur casque dépasse le budget

1. **Occlusion culling** par étage : les modules sont séparés par des murs, gain probable important (chaque joueur ne voit que son module + les hublots).
2. Fusionner les petites pièces de murs (segments autour des hublots) en un seul mesh par mur.
3. Réduire le plancton (nombre de particules) et la portée des lumières de cabine.
4. Désactiver le rendu des 3 cabines qui ne sont pas celle du joueur local quand les portes sont fermées.
