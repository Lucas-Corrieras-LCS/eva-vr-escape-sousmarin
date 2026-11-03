# Projet VR EVA — Escape game sous-marin

Escape game VR mono-joueur pour l'arène **EVA** (piste 31 × 16,5 m), sur **Meta Quest 2**.
Projet d'évaluation de fin de module, en binôme.

## Démarrer

1. Installer **Unity 6000.3.23f1** (6.3 LTS) avec le module **Android Build Support** (OpenJDK + SDK/NDK).
2. Installer Git LFS **avant** de cloner :
   ```bash
   brew install git-lfs && git lfs install
   ```
3. Cloner puis ouvrir le dossier `Projet/` dans Unity Hub.
4. (Recommandé) Activer le Smart Merge Unity pour les scènes/prefabs :
   ```bash
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver "'/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/Helpers/UnityYAMLMerge' merge -p %O %B %A %A"
   git config merge.unityyamlmerge.recursive binary
   ```
   (Windows : `C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/Tools/UnityYAMLMerge.exe`)

## Tester dans l'éditeur

- Ouvrir `Assets/_Project/Scenes/Station.unity`, Play. Le simulateur XR remplace le casque (clavier + souris).
- Menu : *Jouer en solo* → entrer dans la cabine → *Lancer la plongée*. En partie, touche **M** (bouton Menu de la manette gauche sur Quest) pour recalibrer ou quitter.
- **Multijoueur à 2** : *Abysses → Test multijoueur → Activer le scénario 2 joueurs*, puis Play : l'éditeur héberge, un second éditeur s'ouvre et rejoint tout seul. *Revenir au Play normal* pour repasser en solo.

## Menus `Abysses`

| Menu | Effet |
|------|-------|
| Tout construire (+ baker l'éclairage) | Régénère matériaux, prefabs, Station et les 3 étages (⚠️ écrase les scènes, bake ~20 s) |
| Reconstruire la Station seulement | Menu, réseau, rig, cabines — les étages et leur éclairage ne bougent pas |
| Baker l'éclairage des étages | Recalcule lightmaps / probes |
| Captures d'écran des étages | Images dans `docs/captures/` |
| Build → APK debug / APK EVA | APK dans `Projet/Builds/` ; la version EVA coupe le déplacement au joystick (`ABYSSES_PROD`) |
| Test multijoueur | Active / désactive le scénario Multiplayer Play Mode 2 joueurs |

## Règles de travail en binôme

- **Une scène = un propriétaire à la fois.** Prévenir l'autre avant de modifier une scène partagée ; travailler de préférence dans des **prefabs** ou des scènes de niveau séparées (chargement additif).
- Branches courtes : `feat/<sujet>`, merge sur `main` dès que ça marche sur le casque.
- `git pull` avant de commencer, commit + push en fin de session.
- Ne jamais commiter `Library/`, les builds (`.apk`) ou les réglages perso — le `.gitignore` s'en charge.
- Vérifier Unity : *Project Settings → Editor* → **Asset Serialization = Force Text**, **Version Control = Visible Meta Files**.

## Documentation

| Fichier | Contenu |
|---------|---------|
| [CONSIGNES.md](CONSIGNES.md) | Énoncé et contraintes à respecter |
| [BACKLOG.md](BACKLOG.md) | Tâches, progression, journal |
| [WORKFLOW.md](WORKFLOW.md) | Règles de travail, optimisation |
| [docs/setup-unity.md](docs/setup-unity.md) | Version Unity, packages, réglages Quest 2 |
| [docs/game-design.md](docs/game-design.md) | Scénario, étages, énigmes |
| [docs/optimisation.md](docs/optimisation.md) | Budgets et mesures de performance |
