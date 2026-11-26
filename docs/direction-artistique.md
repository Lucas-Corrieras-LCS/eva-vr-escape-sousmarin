# Direction artistique : « illustration sous-marine », version sombre

> Branche `feat/da-stylisee` (2026-11-26). Inspirations d'ambiance seulement (aplats doux, lumière qui raconte, silhouettes lisibles) : aucun élément repris d'œuvres existantes.

## L'idée

- **Des fonds presque noirs, où seul l'important brille** : énigmes, voyants, lampes, créatures, coéquipiers.
- **Des formes douces** : la texture est adoucie (grain conservé ≈ 0,7, on voit encore les tôles et les rivets), chaque objet se lit d'abord à sa couleur.
- **Une palette par étage** :

| Étage | Palette |
|-------|---------|
| Habitat | Bleu-vert profond, contours cyan |
| Laboratoires | Rouge sourd, cuves vertes |
| Ballasts | Vert d'eau |
| Cœur | Presque noir, néons cyan et magenta |

- **Des personnages reconnaissables de loin dans le noir** :
  - une combinaison bleu nuit, lisse ;
  - un casque, des bouteilles, une ceinture et des bandes de bras et de jambes à la couleur du joueur ;
  - une visière éclairée de l'intérieur ;
  - un faisceau de lampe frontale qui montre où regarde le coéquipier.

## Derrière les hublots : chaque hublot est une petite illustration vivante

Plus on descend, moins il y a de couleurs vives et de lumière du jour, et plus il y a de bioluminescence :

| Étage | Eau | Vie |
|-------|-----|-----|
| Habitat (-200 m) | Bleue, rayons de soleil | Récif coloré (éponges violettes, roses, ocre), algues qui ondulent, bancs de poissons orange, jaunes et bleus |
| Laboratoires (-1 500 m) | Sombre | Récif terne aux pointes chaudes, poissons-lanternes, méduses orangées |
| Ballasts (-2 800 m) | Vert d'eau | Algues pâles, poissons et méduses turquoise |
| Cœur (-4 000 m) | Noire | Seulement de la bioluminescence cyan et magenta, méduses violettes, léviathan |

Sur tous les étages, on retrouve aussi :
- un fond d'eau en dégradé, des colonnes de bulles et de la neige marine ;
- une grande créature qui passe à 5,5 m du mur.

Le générateur est `Editor/AbyssesBuilderOcean.cs`. Les animations sont légères, avec un composant par groupe : `FishSchool`, `JellyPulse` et `Sway`. Les coraux sont fusionnés par matériau, et il n'y a aucune lumière en plus.

## Surface : le paysage au loin

Trois chaînes de montagnes en anneau, en couches comme du papier découpé : la plus proche est verte et sombre, la plus lointaine pâle et bleutée (perspective aérienne). S'y ajoutent les îles et le phare dans le même style doux, un halo autour du soleil et plus de nuages.

## Comment c'est fait (et pourquoi ça tient sur Quest 2)

| Élément | Technique | Coût |
|---------|-----------|------|
| Matériaux | Shader `Abysses/Stylized` (`Assets/_Project/Shaders`). Il reprend l'éclairage Simple Lit d'URP (lightmaps, lumière de cabine, brouillard, rendu stéréo) et ajoute un grain adouci, un dégradé vertical et un contour lumineux. | Moins cher que le Lit d'avant : pas de normal map ni de reflets |
| Palette | Variables globales posées par `FloorAmbience` (étages) et `SurfaceWorld` (surface, plongée), via `AbyssStyle` | Nul : aucun matériau dupliqué, SRP Batcher conservé |
| « Bloom » | Halos additifs autour des appliques et des diffuseurs, tournés vers le joueur (`HaloBillboards`) | Quelques quads ; le vrai bloom coûterait 20 à 30 % du GPU |
| Rayons | Cônes additifs qui tombent des hublots sur l'océan | 8 cônes par étage, petits et très transparents |
| Ombres et lumières | Toujours précalculées (bake) ; aucune lumière temps réel en plus | Nul |

Le contour lumineux est dosé matériau par matériau, par l'alpha de la couleur spéculaire (`_SpecColor.a`) :

| Matériaux | Contour |
|-----------|---------|
| Murs | Discret, autour de 0,2 |
| Objets | Marqué, de 0,5 à 0,8 |
| Personnages | Fort, 1 |

Il s'atténue avec la distance, pour que les grands murs vus de biais ne « délavent » pas l'image.

## Régler l'ambiance

- **Palettes des étages** : `FloorPalettes` dans `Editor/AbyssesBuilderStyle.cs`. Pour chacune :
  - teinte au sol et en hauteur ;
  - couleur et force du contour ;
  - grain conservé ;
  - hauteur du dégradé.
- **Couleurs des matériaux** : `StylizeMaterials` (même fichier).
- **Après un réglage** : *Abysses → Tout construire + baker l'éclairage*, puis *Captures d'écran des étages* pour comparer.

## À vérifier au casque

- Le confort et les FPS, qui doivent être au moins égaux à avant, puisqu'il n'y a plus de normal maps.
- La lisibilité des textes des énigmes sur les fonds plus sombres.
