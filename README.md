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
