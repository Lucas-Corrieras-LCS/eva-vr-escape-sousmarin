# Game design — ABYSSES

> Escape game VR coopératif, 2 à 4 joueurs, Meta Quest 2, arène EVA.
> Première version proposée par Claude Code — **à relire et amender par le binôme**.

## Pitch

2047. La station de recherche **HADAL-4**, ancrée sur le flanc d'une fosse océanique, vient de perdre son alimentation principale. Les plongeurs-techniciens se réveillent chacun **enfermé dans son module**. La pression monte, les cloisons grincent. Pour survivre, l'équipe doit **descendre** jusqu'au cœur de la station, à -4 000 m, et lancer la **remontée d'urgence**.

Personne ne peut s'en sortir seul : chaque module contient ce dont **l'autre** a besoin.

- Durée cible : **15–20 min**
- Ton : tension douce, émerveillement, pas d'horreur (public large chez EVA)

## Espace physique → espace virtuel

Piste EVA **31 × 16,5 m**, 4 quadrants de **15 × 8 m** (allées de séparation : 1 m au centre en X, 0,5 m en Z).

```
        x=0            x=15  x=16           x=31
z=16.5  ┌───────────────┐    ┌───────────────┐
        │      Q2       │    │      Q3       │
z=8.5   └───────────────┘    └───────────────┘
z=8     ┌───────────────┐    ┌───────────────┐
        │      Q0       │    │      Q1       │
z=0     └───────────────┘    └───────────────┘
```

- **Le monde virtuel est calqué sur la piste** : un joueur du quadrant Qn est toujours, virtuellement, dans le module Qn. Aucune collision physique possible entre joueurs.
- **Zone jouable réelle** de chaque module : **14 × 7 m** (marge 0,5 m tout autour → dérive EVA de 50 cm / 30 min, C10).
- **Ordre d'attribution** : Q0, Q1, Q2, Q3 → à 2 joueurs, Q0 et Q1 sont **côte à côte** (mur commun de 8 m).
- **Calibration** : au lancement, chaque joueur se place sur le repère au sol au **centre de son quadrant**, face au mur du fond (+Z), et valide. L'origine XR est recalée sur ce point.
- **Déplacement joystick** : uniquement en build de debug. **Désactivé en prod EVA** (C3).

### Limites de zone (C4) — idée originale

- Dans les niveaux 1 et 2 : les **cloisons du module** (naturel, rien à expliquer).
- Si la tête dépasse quand même la zone (dérive) : **alarme de pression** — l'écran se voile de buée/eau, son de coque qui craque, la lumière vire au rouge → on recule instinctivement.
- Au niveau 3 (salle commune) : les modules sont séparés par des **colonnes d'eau sous pression** (vitre courbe + courant de bulles) et une **fissure lumineuse** au sol vers les abysses.

## Extension de l'univers (C8) — le bathyscaphe

Chaque module contient une **cabine d'ascenseur** (3 × 3 m) **toujours au même endroit physique**. Quand l'étage est résolu :

1. les portes de toutes les cabines s'ouvrent ;
2. chaque joueur entre dans **sa** cabine (détection de la tête dans la cabine) ;
3. quand **tous** sont dedans : portes fermées, vibration, bulles qui défilent vers le haut derrière le hublot, profondeur qui défile sur un cadran ;
4. l'étage suivant est chargé derrière les portes, qui se rouvrent sur un **nouveau décor**.

→ 3 étages sur la même surface au sol = **univers 3 fois plus grand** sans faire un pas de plus.

## Les 3 étages

| # | Lieu | Profondeur | Ambiance lumière (C6) |
|---|------|-----------|------------------------|
| 1 | Modules d'habitation | -200 m | Bleu-vert, lumière de surface filtrée, **caustiques** animées au plafond, néons blancs qui clignotent |
| 2 | Laboratoires | -1 500 m | Pénombre, **éclairage de secours rouge/orange**, lampes frontales, faisceaux dans les particules |
| 3 | Cœur de la station — salle commune | -4 000 m | Noir presque total, **bioluminescence** (cyan/magenta), le cœur du réacteur qui pulse, final : lumière blanche venue d'en haut |

## Énigmes coopératives (C9)

Principe : **l'information et l'action sont séparées** entre modules voisins. Les joueurs se parlent à voix haute (même salle physique), se **voient par les hublots**, et s'échangent des objets par les **sas à objets** (tube pneumatique dans le mur commun).

### Étage 1 — « Rétablir le courant »

1. **Le code du voisin** : le panneau de disjoncteurs de chaque module est verrouillé par un code à 4 symboles. Le code est **peint sur le mur du module voisin**, visible seulement par le hublot. → Chacun lit le code de l'autre.
2. **Le fusible échangé** : chaque module contient le fusible coloré dont le module voisin a besoin. → On l'envoie par le sas.
3. **Remise sous tension synchronisée** : tous les leviers doivent être abaissés **à moins de 2 s d'intervalle** (« 3, 2, 1… »). → Ascenseur déverrouillé.

### Étage 2 — « Équilibrer la pression »

1. **Vannes croisées** : chaque module a un manomètre et une vanne ; tourner **sa** vanne fait varier la pression du **voisin**. Il faut amener toutes les aiguilles dans la zone verte → guidage oral + vue par le hublot.
2. **La lampe UV** : un module plongé dans le noir a besoin de la lampe UV d'un autre (envoyée par le sas) pour révéler l'ordre des valves.

### Étage 3 — « Remontée d'urgence » (réunion finale)

- Les cloisons tombent : les joueurs se **voient enfin en entier**, chacun dans son quart de la grande salle.
- Le **cœur du réacteur** est au point de jonction des 4 quadrants (centre de la piste) : chacun en touche **un quart** depuis sa zone.
- Chacun insère sa **clé d'activation** (récupérée aux étages précédents) puis tous tournent **en même temps**.
- Final : la structure se met en mouvement, lumière blanche, bulles, fondu au blanc → écran de fin avec le temps de l'équipe.

### Adaptation au nombre de joueurs

- Les énigmes sont définies **par paires de modules voisins**.
- Les éléments des modules **inoccupés** sont **résolus automatiquement** (le panneau du module vide s'allume tout seul, son fusible arrive déjà dans le sas…).
- **Repli mono-joueur (15 × 8 m)** : même build, session solo dans Q0 ; les infos « du voisin » sont affichées par un écran de surveillance dans le module.

## Architecture technique

| Brique | Choix |
|--------|-------|
| Réseau | Netcode for GameObjects 2.x, client-serveur (un casque est **hôte**) |
| Connexion | **Relay** via Multiplayer Services : bouton unique « Rejoindre la partie » → `CreateOrJoinSession` sur un nom de salle (pas de saisie de code en VR). **LAN** en secours : découverte UDP automatique de l'hôte |
| Joueur | XR Origin local (non réseau) + **NetworkPlayer** (tête + 2 mains) recopiant les poses du rig local, couleur par quadrant |
| Objets | `NetworkGrabbable` : transfert d'ownership au grab, transform possédé par le client qui tient l'objet |
| Logique | `GameStateManager` côté serveur (NetworkVariables + événements, aucun polling) ; les leviers/boutons envoient des RPC au serveur |
| Scènes | `Bootstrap` (menu, NetworkManager) → `Station` (rigs, cabines d'ascenseur, managers, persistante) → `Etage1/2/3` chargées **en additif** et synchronisées par `NetworkSceneManager` |
| Lumière | Lightmaps **par étage** (scènes additives), light probes pour les avatars et objets mobiles, fog URP |
| Perf | 1 seul étage chargé à la fois, objets statiques batchés, matériaux partagés, sync réseau à 30 Hz |
