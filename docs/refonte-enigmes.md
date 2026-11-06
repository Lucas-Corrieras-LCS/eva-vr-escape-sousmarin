# Refonte des énigmes : coopération obligatoire

> Proposition du 2026-11-06, après le premier test à plusieurs casques.
> **Fait** (branche `feat/coop-et-correctifs`) : digicode privé, note des disjoncteurs et balise morse chez le partenaire, Simon croisé, relais d'appels aux 3 étages (il remplace « Surtension », « Fuites » et ajoute les barres de contrôle à l'étage 3).
> **Pas encore fait** : fusibles « presque identiques », séquence du réacteur montrée au seul lecteur, passe visuelle complète des objets.
> Retour de test : « je peux résoudre des choses tout seul, ça ne doit pas être possible en équipe ; les réponses doivent être chez les autres ».

## Règles de conception (inspirées de *Big Walk*)

1. **Info et action toujours séparées** dès qu'on est 2 ou plus. Ce qu'on voit chez soi sert à un autre ; ce qu'on actionne chez soi dépend de ce qu'un autre voit.
2. **Des infos à décrire, pas à recopier.** Plus de codes lisibles par le hublot : des **glyphes** (« le poisson à trois traits »), des formes, des sons, des clignotements. Il faut inventer un vocabulaire commun.
3. **Infos cachées du hublot.** Les indices sont tournés vers l'intérieur du module, ou visibles seulement par le joueur du module. Plus de lecture par-dessus l'épaule du voisin.
4. **Chaînes de 2 à 4 joueurs.** À 3 ou 4 joueurs, l'info fait le tour de l'anneau (Q0 → Q1 → Q3 → Q2) : chacun est tour à tour **lecteur** et **acteur**.
5. **Boucles de jeu courtes et répétées** (3 manches qui accélèrent) plutôt qu'une énigme unique. On rate, on rit, on recommence, on s'améliore.
6. **Moments synchronisés** (« 3, 2, 1… maintenant ! ») et **échanges physiques** par les sas.
7. **Adaptation au nombre de joueurs inchangée.** Un module vide est résolu automatiquement. **En solo**, les écrans de surveillance montrent l'info du partenaire (repli des consignes).

## Étage 1 : « Rétablir le courant »

| Énigme | Avant | Après |
|--------|-------|-------|
| Digicode | Code à 4 symboles lisible par le hublot | **Clavier à 9 glyphes.** Le code est affiché **dans le module du partenaire**, tourné vers l'intérieur : il faut le décrire de vive voix. |
| Disjoncteurs | Note du chef mécanicien sur **son propre** bureau (jouable seul) | Le **schéma** (quels disjoncteurs lever, sous forme de pictogrammes) est **chez le partenaire**. |
| Fusible | Le fusible du voisin est dans mon module | **4 fusibles presque identiques.** Seul mon module sait lequel est le bon (calibre et bague) : je le décris, le voisin le trouve et me l'envoie par le sas. |
| Leviers | Abaisser ensemble (2 s) | Inchangé : c'est le final de l'étage. |
| **Nouvelle boucle : « Surtension »** | – | Après les leviers, 3 manches qui accélèrent : un disjoncteur saute chez moi, mais **seul le voisin voit lequel** sur son tableau. Il crie « le 3 ! », je le relève avant que le voyant passe au rouge. Raté : la manche recommence. |

## Étage 2 : « Équilibrer la pression »

| Énigme | Avant | Après |
|--------|-------|-------|
| Vannes croisées | Déjà coopératif | Inchangé (ma vanne règle la jauge du suivant). |
| Lampe UV | Déjà coopératif | Inchangé (une seule lampe, qui circule par les sas). |
| Morse | Balise et cadenas dans le même module (jouable seul) | La **balise** n'est visible que par le hublot du module A. L'**alphabet morse** et le **cadenas** sont chez B. A décrit les clignotements (« long, court, court »), B décode et entre le code. |
| **Nouvelle boucle : « Fuites »** | Fuites décoratives | 3 manches : une fuite éclate chez moi (jet de vapeur, alarme). La **vanne qui la coupe est chez le voisin**, parmi 4 vannes de couleur. Je décris le tuyau qui fuit, il tourne la bonne vanne. Trop lent : l'eau monte (temps perdu, pas d'échec définitif). |

## Étage 3 : « Remontée d'urgence »

| Énigme | Avant | Après |
|--------|-------|-------|
| Console sonar | Simon jouable seul | **Simon croisé** : la séquence de couleurs s'affiche sur la console d'un joueur, mais **les boutons sont chez le suivant**. Le premier dicte, le second joue. Les rôles tournent à chaque manche, donc tout le monde parle et tout le monde joue. |
| Séquence du réacteur | Chacun voit toute la séquence | Le cœur ne montre la séquence **qu'au joueur « lecteur »** du moment ; les autres appuient quand il les appelle. |
| Leviers de remontée | Simultanés | Inchangé (final). |

## Visuel et interactions des objets d'énigme

« On ne peut pas avoir un truc moche essentiel au gameplay. » Un même langage visuel pour **tout ce qui se manipule** :

- **Repérable de loin** : liseré jaune et noir « danger » et petite lumière d'appel autour de chaque objet interactif. Un objet décoratif n'en a jamais.
- **Retour immédiat** sur chaque geste : survol (halo), prise (vibration, son), réussite (voyant vert, clic métallique, petite animation), erreur (étincelles ou buzzer, voyant rouge).
- **Objets « en dur »** : plus de cubes nus.
  - Leviers à poignée caoutchouc.
  - Volants de vanne à rayons.
  - Fusibles à cartouche lumineuse.
  - Claviers rétroéclairés.
  - Écrans avec reflet et lueur.
- **Lisibilité VR** : textes et glyphes d'au moins 4 cm à 1 m, contraste fort, rien sous 0,6 m ni au-dessus de 1,8 m (ergonomie déjà vérifiée par *Abysses → Vérifier l'ergonomie*).
- **Un voyant d'état par énigme** (rouge, orange ou vert) visible depuis l'entrée du module : on sait toujours ce qui reste à faire.

## Correctifs déjà faits (branche `feat/coop-et-correctifs`)

- **Réseau local sur Quest** :
  - verrou multicast Wi-Fi et permission Android ajoutés (sans eux, le casque jette les annonces de l'hôte) ;
  - annonce aussi envoyée sur le broadcast du sous-réseau ;
  - l'hôte affiche son adresse IP ;
  - message clair si le réseau bloque (eduroam).
- **Jeu en ligne** : la cause de l'échec s'affiche dans le menu. Le projet doit être **lié à Unity Cloud**, avec **Relay et Lobby activés** (action sur le compte Unity).
- **Chute dans le vide** : sol de secours invisible sous toute la piste ; retour automatique au centre du module (fondu au noir) si on tombe, ou si, au joystick, on traverse un mur et qu'on s'éloigne.
- **Bouton MENU** sur la montre (poignet gauche), en plus du bouton Menu de la manette.
