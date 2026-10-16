# WORKFLOW — TP Unity

## Organisation du dossier

```
TP_Unity/
├── CONSIGNES.md   ← énoncé du TP (source de vérité)
├── BACKLOG.md     ← tâches + progression + journal
├── WORKFLOW.md    ← ce fichier
├── docs/          ← PDF de l'énoncé, mesures de perf, captures
└── Projet/        ← projet Unity (créé via Unity Hub)
```

## Règles

1. **Consignes d'abord** : chaque consigne de `CONSIGNES.md` correspond à au moins une tâche du backlog. Rien n'est ajouté hors consignes sans validation.
2. **Tout passe par le MCP Unity** : scènes, GameObjects, composants, matériaux, builds, tests en Play Mode. Les scripts `.cs` sont écrits sur disque puis importés/compilés en une fois via le MCP (une recompilation par fichier serait beaucoup plus lente). Ne pas modifier les `.unity` / `.prefab` à la main.
3. **Backlog à jour** : après chaque tâche, cocher, renseigner « Où on en est » et ajouter une ligne au journal.
4. **Vérifier la console** Unity (`read_console`) après chaque modification de script : zéro erreur avant de passer à la suite.
5. **Commit Git** à chaque étape stable.
6. **Scènes générées** : `Abysses → Tout construire` régénère matériaux, prefabs, Station et étages (blockout). ⚠️ Écrase les scènes : une fois la passe artistique commencée sur un étage, ne plus le régénérer (ou retirer son appel du générateur).

## Optimisation (à appliquer tout du long)

- **Scripts** : mettre en cache les références (`GetComponent`, `Camera.main`) dans `Awake`/`Start` ; aucune allocation dans `Update` (pas de `new`, LINQ, concaténation de chaînes) ; préférer les événements au polling ; pooling pour les objets instanciés souvent.
- **Rendu** : objets statiques marqués *Static* ; matériaux partagés + GPU instancing ; lumières *Baked* / *Mixed* plutôt que *Realtime* ; peu de lumières avec ombres.
- **Assets** : textures compressées et dimensionnées au besoin ; meshes low-poly / LOD si nécessaire ; audio en *Compressed in Memory* ou *Streaming* selon la taille.
- **Physique** : colliders primitifs plutôt que *Mesh Collider* ; matrice de collisions des layers réduite.
- **Mesures** : relever FPS / draw calls / batches avant et après optimisation (`manage_profiler`), noter dans `docs/optimisation.md`.

## Délégation (tâches différables)

Claude Code garde la main sur tout ce qui touche l'éditeur Unity (MCP). Les tâches **indépendantes de l'éditeur** peuvent être déléguées :

| Outil | Usage conseillé | Commande |
|-------|-----------------|----------|
| **OpenCode** | Scripts C# isolés, refactor, revue de code | `opencode run "…"` (dans `TP_Unity/`) |
| **Antigravity** | Rédaction (rapport, README), recherche doc, tâches longues | `agy -p "…" --add-dir .` |

Règles de délégation :
- Le prompt délégué doit être autonome (chemins, consignes concernées, critères de fini).
- Chaque délégation est inscrite dans le tableau « Tâches déléguées » du backlog.
- Le résultat est **toujours relu et vérifié** (console Unity sans erreur) avant d'être coché.
