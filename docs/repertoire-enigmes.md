# Répertoire des énigmes — ABYSSES

> 2026-11-24, après le test à 3 casques. **Document de travail : à valider (choix des nouvelles énigmes) avant de coder.**
>
> Retours du test qui ont mené à ce document :
> - le relais de symboles revient dans plusieurs salles : on ne veut **jamais deux fois la même énigme** ;
> - c'est toujours le même joueur qui aide le même joueur, toujours avec des symboles ;
> - l'étage des ballasts est bien, mais vide ;
> - on se voit bien à l'étage 4, mais pas avant : il faut se voir **de mieux en mieux** d'un étage à l'autre.

## 1. Règles de conception (nouvelles)

| # | Règle | Conséquence concrète |
|---|-------|----------------------|
| R1 | **Une mécanique = une seule fois dans la partie.** | Le relais d'appels, les leviers synchronisés et le « lis-moi le code » ne reviennent plus d'un étage à l'autre. |
| R2 | **Les binômes tournent.** | Chaque énigme route l'indice vers un joueur différent : partenaire (mur commun), voisin du dessus ou du dessous, diagonale, anneau. À 2 joueurs, les rôles s'alternent : celui qui sait sur une énigme agit sur la suivante. |
| R3 | **Les canaux de communication varient.** | Décrire (voix), montrer (vitre), échanger un objet (sas), frapper (cloison), guider un déplacement, se synchroniser dans le temps, déduire à plusieurs. Plus « que des symboles ». |
| R4 | **Énigmes à étapes et à règles**, pas seulement « recopier une info ». | L'indice doit être *interprété* : règles conditionnelles, combinaison de deux sources, déduction. |
| R5 | **Une énigme d'étage relie les autres** (méta-énigme). | Chaque énigme d'un étage donne un morceau (chiffre, couleur, position) du verrou final de l'étage. |
| R6 | **On se voit de mieux en mieux** (visibilité croissante). | Étage 1 : petits hublots ronds. Étage 2 : baies. Étage 3 : grandes vitres, en diagonale aussi. Étage 4 : salle commune vitrée. |
| R7 | Contraintes EVA inchangées. | Tout se joue en marchant (pas de déplacement artificiel), dans 15 × 8 m, à 0,8–1,6 m de hauteur. Partie possible à 2, 3 ou 4 (un module vide est résolu ou son info est re-routée). Pas de solo. |

## 2. Répertoire actuel

Durées estimées pour une équipe de 2 à 3 joueurs qui découvre le jeu.

| Étage | Énigme | Mécanique | Qui sait → qui agit | Canal | ~Durée | Problème |
|-------|--------|-----------|---------------------|-------|--------|----------|
| Cabine | Entraînement | appuyer, brancher un fusible | seul | — | 1 min | **Mal modélisé, à retirer** (demande du test). |
| 1 Habitat | Digicode à glyphes | code à 4 symboles chez le partenaire, privé | partenaire → moi | voix | 1–2 min | Même schéma que disjoncteurs, morse, collecteur et sonar. |
| 1 | Fusibles échangés | le fusible de l'autre est chez moi, envoi par le sas | moi → partenaire | objet | 1–2 min | Bon (physique). Toujours le même binôme. |
| 1 | Disjoncteurs | note chez le partenaire : 2 manettes à laisser coupées | partenaire → moi | voix | 1 min | Trop simple (2 mots à dicter). |
| 1 | Remise sous tension | leviers abaissés ensemble | tous | temps | 30 s | **Doublon** : revient aux étages 3 et 4. |
| 1 | Relais « surtension » | symbole chez l'appelant, bouton chez la cible | rôles tirés au hasard | voix + temps | 2–3 min | **Doublon** : revient aux étages 2, 3 et 4. |
| 2 Labo | Vannes croisées | ma vanne règle la jauge du joueur suivant (anneau) | anneau | voix (« encore… stop ») | 2 min | Bon : les rôles tournent. |
| 2 | Lampe UV | une seule lampe, qui circule par les sas | tous | objet | 2–3 min | Bon. Refait un digicode (doublon de mécanique). |
| 2 | Morse | balise chez le partenaire, cadenas et table chez moi | partenaire → moi | voix / lumière | 2 min | Même schéma « lis-moi », même binôme. |
| 2 | Relais « fuites » | — | — | — | 2–3 min | **Doublon.** |
| 3 Ballasts | Collecteur de vannes | schéma chez le partenaire, 5 vannes chez moi | partenaire → moi | voix | 1–2 min | Même schéma, même binôme ; étage « vide ». |
| 3 | Relais « purge » | — | — | — | 2–3 min | **Doublon.** |
| 3 | Leviers de chasse | leviers ensemble | tous | temps | 30 s | **Doublon.** |
| 4 Cœur | Sonar croisé (Simon) | séquence sur le récepteur du partenaire | partenaire → moi | voix | 2 min | Même schéma, même binôme. |
| 4 | Séquence du réacteur | chacun appuie quand vient sa couleur | tous | vue + temps | 1–2 min | Bon (on se voit). |
| 4 | Relais « barres » | — | — | — | 3 min | **Doublon.** |
| 4 | Remontée | leviers ensemble | tous | temps | 30 s | À garder une seule fois : le final. |

**Bilan.** 17 énigmes, mais seulement 9 mécaniques. 5 énigmes suivent le même schéma : « mon partenaire fixe me lit une info, je la recopie ». Le relais revient 4 fois et les leviers 3 fois. Le binôme ORANGE ↔ CYAN (VERT ↔ MAGENTA) ne change jamais. Total d'environ 15 minutes, conforme au test.

## 3. Ce qu'on garde, ce qu'on retire

- **On garde, une seule fois chacune :**
  - fusibles par le sas ;
  - vannes croisées en anneau ;
  - lampe UV ;
  - morse, en re-routant la balise vers un autre joueur ;
  - collecteur, avec un nouveau routage ;
  - séquence du réacteur ;
  - relais d'appels, à un seul étage ;
  - leviers de la remontée finale.
- **On retire :**
  - l'entraînement de la cabine ;
  - 3 des 4 relais ;
  - les leviers des étages 1 et 3 ;
  - le digicode en double de l'étage 2 (la lampe UV révélera autre chose qu'un code) ;
  - le sonar « Simon », trop proche du relais et du morse.
- **On durcit :**
  - les disjoncteurs deviennent le « manuel de désamorçage » (N1) ;
  - le collecteur reçoit un schéma à interpréter, et non plus à recopier (voir N6).

## 4. Catalogue de nouvelles énigmes

Inspirations : *Keep Talking and Nobody Explodes* (manuel de règles), *We Were Here* (informations asymétriques, guidage à l'aveugle), *Escape Simulator* (énigmes physiques à plusieurs étapes), escape games réels (méta-énigme d'étage).

### N1 — Manuel de désamorçage (étage 1, remplace les disjoncteurs) ★★★
- **Matériel** : un tableau électrique en panne chez A, avec 5 à 7 fils de couleur, 3 voyants, un compteur et un numéro de série, tirés au hasard.
- **Indice** : le **manuel du chef mécanicien** est chez B, avec 4 pages de règles conditionnelles. Exemple : « S'il y a plus de deux fils rouges et que le voyant du haut clignote, coupez le dernier fil bleu ; sinon… ».
- **Jeu** : A décrit ce qu'il voit, B cherche la bonne règle, A coupe. Une erreur provoque un court-circuit (étincelles, choc dans la manette) et un nouveau tirage.
- **Pourquoi c'est plus dur** : il faut interpréter des règles. Aucune info ne se recopie telle quelle.
- **Coût** : moyen. Fils attrapables à « couper », générateur de règles, pupitre-livre à pages.

### N2 — Labyrinthe électrifié (étage 3, Ballasts) ★★★★
- **Matériel** : la cale de A est noyée sous 30 cm d'eau trouble, son sol est une grille de dalles de 50 cm, et certaines sont sous tension sans que ça se voie.
- **Indice** : B voit sur un écran de contrôle la carte des dalles et un point qui suit A en temps réel.
- **Jeu** : B guide A pas à pas (« deux pas à gauche… stop… avance ») jusqu'à la vanne de purge au fond de la cale. Une mauvaise dalle déclenche un choc, un voile rouge, et A repart du départ.
- **Pourquoi c'est bien pour EVA** : on **marche vraiment** dans les 15 × 8 m, et c'est la seule énigme de déplacement.
- **Coût** : moyen. Grille logique, détection de la tête, écran-carte. Aucune physique.

### N3 — Câblage du standard (étage 4) ★★★★
- **Matériel** : un tableau de 12 prises chez C, à relier par 4 cordons.
- **Indices, répartis sur 3 joueurs qui parlent des langues différentes** :
  - A a la liste des liaisons en **couleurs** (« le rouge va au bleu ») ;
  - B a la légende qui traduit les **couleurs en numéros de circuit** ;
  - C ne voit que des **numéros** sur ses prises.
- **Jeu** : il faut faire passer l'info par 2 personnes. À 2 joueurs, A cumule liste et légende, mais en codes mélangés.
- **Coût** : moyen. Cordons attrapables avec fiche, détection prise par prise.

### N4 — Triangulation du Léviathan (étage 4, salle commune vitrée) ★★★★
- **Matériel** : une créature tourne autour de la station, et chaque console sonar donne sa **distance** au module (un cercle sur la carte).
- **Jeu** : en combinant 2 à 3 relevés sur la grande carte commune, on trouve la case (lettre + chiffre) où tirer le **leurre lumineux**. Trois tirs sont possibles. Une erreur fait charger la créature : secousse, lumières qui vacillent.
- **Pourquoi** : déduction collective, et on utilise la vue de la salle commune.
- **Coût** : moyen. Les cercles sont dessinés sur des écrans, sans physique.

### N5 — Antidote du labo (étage 2) ★★★
- **Matériel** : 4 flacons de réactifs colorés chez A, qu'il verse par doses dans un mélangeur ; une pipette compte les doses.
- **Indices** :
  - B a la **recette**, en noms de molécules ;
  - le tableau de correspondance molécules → couleurs est **éclairé seulement à la lampe UV** ;
  - la lampe UV voyage par les sas (on réutilise la lampe existante).
- **Jeu** : mélange réussi, la vanne de sortie s'ouvre ; raté, le mélangeur mousse et se vide.
- **Coût** : moyen. Doses par appui ou inclinaison du flacon, pas de fluide simulé.

### N6 — Cloisons étanches (étage 3, final de l'étage, remplace les leviers) ★★★★
- **Situation** : l'eau monte. Le plan des compartiments (8 portes étanches et la source de la fuite) est affiché chez un seul joueur, et les **commandes des portes sont réparties** chez tous.
- **Jeu** : il faut fermer les portes dans le bon ordre pour isoler la fuite sans enfermer l'eau du côté de la pompe. Un mauvais ordre fait monter l'eau d'un cran : temps perdu, jamais de défaite.
- **Pourquoi** : réflexion stratégique à plusieurs, sous pression.
- **Coût** : moyen. Logique de graphe simple, voyants, leviers ou boutons.

### N7 — Code frappé sur la coque (étage 1 ou 2) ★★
- **Matériel** : la plaque de cloison existe déjà (KnockWall).
- **Jeu** : A reçoit un rythme à transmettre (« 3 coups, pause, 1 coup… »). Il le frappe sur la cloison, B le ressent dans la manette et le recopie sur un pupitre de rythmes. Sans voix : un **canal tactile**.
- **Coût** : faible.

### N8 — Contrepoids des ballasts (étage 3) ★★★
- **Matériel** : une balance à 4 plateaux, un par module, et des lests de 1, 2, 5 et 10 kg répartis dans les modules.
- **Indice** : la jauge de chaque plateau s'affiche dans **un autre** module.
- **Jeu** : il faut équilibrer à la valeur demandée en s'échangeant des lests par les sas.
- **Pourquoi** : logistique, calcul, objets.
- **Coût** : moyen.

### Méta-énigme d'étage (R5)
Chaque énigme résolue fait apparaître un morceau du verrou final de l'étage, par exemple un chiffre sur l'écran du module qui l'a résolue. Les joueurs doivent rassembler les morceaux à voix haute pour ouvrir la cabine. Ça remplace les leviers synchronisés en fin d'étage.

## 5. Nouvel enchaînement proposé (≈ 25–30 min)

Rôles : A = celui qui agit, B = celui qui sait. Le routage change à chaque énigme.

| Étage | Visibilité (R6) | Énigmes (toutes différentes) | Routage des rôles |
|-------|-----------------|------------------------------|-------------------|
| 1 Habitat | petits hublots ronds vers le partenaire seulement | Digicode à glyphes · Fusibles par le sas · **N1 Manuel de désamorçage** · **N7 Code frappé** · méta : code de cabine | partenaire, puis inversé, puis partenaire |
| 2 Labo | baies vers le partenaire **et** le voisin | Vannes croisées (anneau) · Lampe UV + **N5 Antidote** · Morse (balise chez le voisin) · Relais d'appels (seule occurrence) | anneau, puis tous, puis voisin |
| 3 Ballasts | grandes vitres, vue en diagonale | Collecteur (schéma à interpréter, routé en diagonale) · **N2 Labyrinthe électrifié** · **N8 Contrepoids** · final **N6 Cloisons étanches** | diagonale, puis partenaire, puis tous |
| 4 Cœur | salle commune entièrement vitrée | **N4 Triangulation** · **N3 Câblage du standard** · Séquence du réacteur · **Remontée** (leviers, seule occurrence) | 3 rôles, puis tous |

**À 2 joueurs**, les modules vides sont résolus automatiquement, et toute info destinée à un module vide part chez « le prochain joueur présent qui n'est pas celui qui agit ». L'info est donc toujours chez quelqu'un d'autre, et les rôles s'inversent d'une énigme à l'autre.

## 6. Bugs relevés au même test (à corriger dans la foulée)

| Bug | Cause probable | Correctif prévu |
|-----|----------------|-----------------|
| Hublot de cabine rond, mais les coins carrés de l'ouverture du mur dépassent | Trou carré dans la paroi, cadre rond posé devant | Remplir les coins entre le trou et l'anneau, ou faire un vrai trou rond |
| Joueur grand coincé à la sortie de la cabine | Le corps physique du déplacement debug prend la hauteur de la tête et bute sur le linteau de porte | Rehausser le passage, limiter la hauteur du corps physique, ignorer le linteau |
| Entraînement de la cabine mal modélisé | — | Retiré (avec son pupitre) |
| Ballasts « vides » | Seulement 3 éléments | Couvert par N2, N6 et N8 |
| On ne se voit qu'à l'étage 4 | Hublots identiques aux étages 1 à 3 | Visibilité croissante (R6) |

## 7. À décider

1. Quelles nouvelles énigmes coder en premier ? Proposition : **N1** (manuel), **N2** (labyrinthe) et **N6** (cloisons), les plus fortes et les plus différentes.
2. On garde le relais d'appels une fois, à l'étage 2, ou on le retire complètement ?
3. Méta-énigme d'étage (chiffres à rassembler) pour remplacer les leviers en fin d'étage : oui ou non ?
