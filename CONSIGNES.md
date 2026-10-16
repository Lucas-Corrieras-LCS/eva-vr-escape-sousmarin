# CONSIGNES — Projet VR EVA (évaluation fin de module)

> Énoncé original : [`docs/enonce-original.md`](docs/enonce-original.md). Ce fichier est la source de vérité : chaque consigne ↔ au moins une tâche du `BACKLOG.md`.

## Contexte

- Partenaire : **EVA** (arène VR, Troyes). Objectif : proposer **une autre expérience que le FPS VR** pour élargir leur clientèle.
- Groupe de **2 max**. 33 h présentiel + 33 h perso.
- Casque de dev : **Meta Quest 2**.
- Test prévu sur place chez EVA en milieu de semestre (date à fixer).

## Les 3 types de projet (au choix)

| Type | Difficulté | Surface de jeu | Barème axé sur |
|------|-----------|----------------|----------------|
| Balade contemplative mono-joueur | ★★ | Piste entière **31 × 16,5 m** | Design, ambiance |
| Escape game mono-joueur | ★★★ | Piste entière **31 × 16,5 m** | Gameplay + ambiance |
| Escape game multi-joueur (4 max) | ★★★★★ | **15 × 8 m** par joueur (¼ de piste), même monde virtuel | Technique |

Variante autorisée : partir sur le multi en 15 × 8 m, et si le réseau échoue → repli mono-joueur en 15 × 8 m.

## Contraintes extraites

| # | Consigne | Tâche(s) backlog | Statut |
|---|----------|------------------|--------|
| C1 | Application VR (pas un FPS) adaptée à EVA | Cadrage | ⏳ |
| C2 | Respecter la surface de jeu du type choisi (31×16,5 m ou 15×8 m), monde à l'échelle 1:1 | Blockout | ⏳ |
| C3 | **Déplacement joystick désactivable** — désactivé en prod chez EVA (marche réelle uniquement) | Flag `allowJoystickLocomotion` | ⏳ |
| C4 | Empêcher le joueur de sortir de sa zone (murs, précipices… idée **originale**) | Limites de zone | ⏳ |
| C5 | **Qualité finale exigée** : « ça marche » = juste éviter le 0 | Polish, tests | ⏳ |
| C6 | **Lumière** : point très important de l'évaluation | Lighting | ⏳ |
| C7 | **DA claire et travaillée**, atmosphère soignée | Direction artistique | ⏳ |
| C8 | **Bonus** : astuce pour « étendre l'univers virtuel » (porte, sas, tapis roulant, ascenseur…) | Extension monde | ⏳ |
| C9 | (Multi) actions nécessitant de **coopérer** ; les joueurs voient les avatars des autres | — | si multi |
| C10 | (Multi) dérive possible de 50 cm / 30 min → prévoir des marges | — | si multi |

## Contraintes techniques implicites (Quest 2 / prod EVA)

- Performance VR stable : **72 FPS minimum** sur Quest 2 (sinon malaise → qualité non acquise).
- Pas de déplacement artificiel en prod → tout le gameplay doit être accessible **en marchant**.
- Recentrage / calibration de l'origine du monde par rapport à la piste.

## Rendu attendu

- Format : _(à préciser — APK ? projet ? vidéo ? soutenance ?)_
- Date limite : _(à préciser)_
- Casque EVA en prod : _(à confirmer — Quest 2 ou matériel EVA ?)_
