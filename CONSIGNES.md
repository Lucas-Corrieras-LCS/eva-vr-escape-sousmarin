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

## Type retenu

**Escape game multijoueur** — chaque joueur dispose de **15 × 8 m** (¼ de piste), jusqu'à 4 joueurs dans le **même monde virtuel**. Repli : mono-joueur 15 × 8 m si le réseau échoue.

## Contraintes extraites

> ✅ fait et vérifié · 🟡 fait, à valider au casque / à améliorer · ⏳ à faire. Mis à jour le 2026-11-03.

| # | Consigne | Réalisation | Statut |
|---|----------|------------------|--------|
| C1 | Application VR (pas un FPS) adaptée à EVA | Escape game coopératif sous-marin, 3 étages | ✅ |
| C2 | Zone jouable **15 × 8 m par joueur**, monde à l'échelle 1:1 | Monde calqué sur la piste (`PlayArea`), 1 quadrant par joueur | ✅ |
| C3 | **Déplacement joystick désactivable** — désactivé en prod chez EVA (marche réelle uniquement) | `LocomotionGate` + define `ABYSSES_PROD` ; menu *Abysses → Build → APK EVA* ; téléportation / rotation au stick toujours coupées | ✅ (à valider sur casque) |
| C4 | Empêcher le joueur de sortir de sa zone (murs, précipices… idée **originale**) | Cloisons du module + « alarme de pression » (voile rouge, alarme, vibrations) ; étage 3 : parois vitrées + failles lumineuses | ✅ |
| C5 | **Qualité finale exigée** : « ça marche » = juste éviter le 0 | Parcours complet testé en solo et à 2 joueurs (éditeur) ; reste les playtests casque | 🟡 |
| C6 | **Lumière** : point très important de l'évaluation | Éclairage baké par étage, caustiques, gyrophares, bioluminescence, rayon final | 🟡 (à juger au casque) |
| C7 | **DA claire et travaillée**, atmosphère soignée | DA sous-marine, palette par profondeur, sons ; passe artistique : textures PBR, coque rivetée, mobilier par étage, cabine bathyscaphe (captures dans `docs/captures/`) | 🟡 (à juger au casque) |
| C8 | **Bonus** : astuce pour « étendre l'univers virtuel » (porte, sas, tapis roulant, ascenseur…) | Bathyscaphe : 3 étages sur la même surface, descente simulée (bulles, profondeur, vibrations) | ✅ |
| C9 | **Coopération obligatoire** (ex. un joueur actionne un levier, un autre une manette plus loin) ; **les joueurs voient les avatars** des autres (ouvertures, hublots…) | Infos croisées entre modules, sas, vannes en anneau, leviers synchronisés, séquence du réacteur ; avatars colorés vus par les hublots puis la salle vitrée — **testé à 2 joueurs** | ✅ |
| C10 | Pas de géolocalisation EVA : **dérive possible de 50 cm / 30 min** → marges de sécurité dans chaque zone 15 × 8 m | Murs en retrait de 30 cm, alerte à 50 cm du bord, recalibrage depuis le menu (bouton Menu en partie) | ✅ |

## Contraintes techniques implicites (Quest 2 / prod EVA)

- Performance VR stable : **72 FPS minimum** sur Quest 2 (sinon malaise → qualité non acquise).
- Pas de déplacement artificiel en prod → tout le gameplay doit être accessible **en marchant**.
- Recentrage / calibration de l'origine du monde par rapport à la piste.

## Rendu attendu

- Format : _(à préciser — APK ? projet ? vidéo ? soutenance ?)_
- Date limite : _(à préciser)_
- Casque EVA en prod : _(à confirmer — Quest 2 ou matériel EVA ?)_
