using System.Collections.Generic;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Textes du jeu écrits par le générateur : indices progressifs de l'IA de la station (du plus vague au plus précis)
    /// et journaux de bord de l'équipage. Français + anglais.
    /// </summary>
    public static class AbyssesHints
    {
        public static readonly Dictionary<string, (string[] fr, string[] en)> Table = new Dictionary<string, (string[] fr, string[] en)>
        {
            ["breakers"] = (new[]
            {
                "Le tableau des disjoncteurs du mur du fond doit être réarmé… mais pas en entier.",
                "Le chef mécanicien a laissé une note… sur le bureau de ton coéquipier : demande-lui de te la lire.",
                "Lève toutes les manettes sauf les deux nommés sur la note. Si tout disjoncte, recommence sans toucher à ces deux-là.",
            }, new[]
            {
                "The breaker panel on the back wall must be reset… but not all of it.",
                "The chief engineer left a note… on your teammate's desk: ask them to read it to you.",
                "Raise every switch except the two named on the note. If everything trips, start again without touching those two.",
            }),
            ["morse"] = (new[]
            {
                "Ta balise de détresse clignote devant le hublot du fond… de ton coéquipier. Lui seul la voit et l'entend à la radio.",
                "C'est du morse : chaque chiffre fait 5 signaux courts (·) ou longs (—). La table est fixée au mur, à côté du cadenas.",
                "Le message fait 3 chiffres et se répète après un long silence. Règle les 3 molettes dans l'ordre, puis VALIDER.",
            }, new[]
            {
                "Your distress beacon is flashing outside your teammate's back window. Only they can see it and hear it on the radio.",
                "It is Morse code: each digit is 5 short (·) or long (—) signals. The table is on the wall, next to the lock.",
                "The message has 3 digits and repeats after a long silence. Set the 3 wheels in order, then press CONFIRM.",
            }),
            ["simon"] = (new[]
            {
                "La console du sonar, sur le mur du fond, doit être recalibrée : appuie sur ÉCOUTER.",
                "La séquence s'affiche sur le RÉCEPTEUR de ton coéquipier, pas chez toi : il doit te la dicter, tu la reproduis.",
                "Une erreur et tout recommence : retiens les couleurs et les notes, l'écran indique où tu en es.",
            }, new[]
            {
                "The sonar console on the back wall needs recalibrating: press LISTEN.",
                "The sequence shows up on your teammate's RECEIVER, not yours: they read it to you, you repeat it.",
                "One mistake and it starts over: remember colours and notes, the screen shows your progress.",
            }),
            ["wiring"] = (new[]
            {
                "Le tableau de dérivation du mur du fond doit être désamorcé : il faut couper le bon fil, trois tableaux d'affilée.",
                "Les règles sont dans le manuel du chef mécanicien… affiché chez un autre plongeur. Décris-lui ton tableau : couleurs des fils dans l'ordre, voyant, numéro de série.",
                "Il applique la PREMIÈRE règle vraie et te dit quel fil couper. Un mauvais fil fait tout disjoncter : on repart du premier tableau.",
            }, new[]
            {
                "The bypass panel on the back wall must be defused: cut the right wire, three panels in a row.",
                "The rules are in the chief engineer's manual… shown at another diver's. Describe your panel: wire colours in order, light, serial number.",
                "They apply the FIRST true rule and tell you which wire to cut. A wrong wire trips everything: back to the first panel.",
            }),
            ["maze"] = (new[]
            {
                "Une partie de la cale est électrifiée : seules certaines dalles sont sûres, et rien ne les distingue.",
                "Un autre plongeur voit la carte de TA cale et ta position en direct sur son écran de surveillance : laisse-le te guider.",
                "Pars de la dalle DÉPART. Une décharge te renvoie au départ ; sortir de la grille aussi. Avance case par case jusqu'à la vanne.",
            }, new[]
            {
                "Part of the hold is electrified: only some tiles are safe, and nothing tells them apart.",
                "Another diver sees YOUR hold's map and your live position on their monitor: let them guide you.",
                "Start from the START tile. A shock sends you back; so does leaving the grid. Move tile by tile to the valve.",
            }),
            ["bulkhead"] = (new[]
            {
                "Une fuite inonde la coque des ballasts : l'eau passe d'un compartiment à l'autre par les portes ouvertes.",
                "Un seul plongeur a le PLAN ; les commandes des portes sont réparties entre vous. Il doit dire qui ferme quoi.",
                "Trois fermetures seulement : fermez toutes les portes entre l'eau et le sec avant qu'elle n'atteigne la POMPE (jaune).",
            }, new[]
            {
                "A leak is flooding the ballast hull: water moves between compartments through open doors.",
                "Only one diver has the MAP; the door controls are spread among you. They must say who closes what.",
                "Only three closures: close every door between water and dry before it reaches the PUMP (yellow).",
            }),
            ["sonarhunt"] = (new[]
            {
                "Le Léviathan rôde autour de la station. Chaque sonar mesure sa distance, en cases de la carte.",
                "Chacun lit la distance de SON sonar (coin de la carte à sa couleur) ; celle du réacteur, au centre, est la même pour tous.",
                "Distance = cases en ligne droite, sans diagonale. Cherchez ensemble la case qui colle à toutes les distances, puis lettre + chiffre + FEU.",
            }, new[]
            {
                "The Leviathan is circling the station. Each sonar measures its distance, in map tiles.",
                "Everyone reads THEIR sonar's distance (map corner in their colour); the reactor's, in the centre, is the same for all.",
                "Distance = tiles in straight lines, no diagonals. Find together the tile matching every distance, then letter + number + FIRE.",
            }),
            ["patch"] = (new[]
            {
                "Le standard des circuits doit être recâblé : 4 fiches à brancher sur les bonnes prises.",
                "L'opérateur ne voit que des numéros ; la liste des liaisons est en couleurs, et la légende traduit les couleurs en numéros. Chacun a une partie.",
                "Liste : « ROUGE → GRIS ». Légende : « ROUGE = 37, GRIS = 52 ». L'opérateur branche alors la fiche 37 sur la prise 52.",
            }, new[]
            {
                "The circuit switchboard must be rewired: 4 plugs into the right sockets.",
                "The operator only sees numbers; the link list is in colours, and the legend turns colours into numbers. Each of you has a part.",
                "List: “RED → GREY”. Legend: “RED = 37, GREY = 52”. The operator then plugs 37 into socket 52.",
            }),
            ["antidote"] = (new[]
            {
                "Le mélangeur du labo attend un antidote : un certain nombre de doses de chaque flacon de couleur, puis MÉLANGER.",
                "La recette de TON antidote est chez un autre plongeur, en noms de molécules. Quelle molécule est dans quel flacon ? C'est écrit à l'encre UV au labo ORANGE.",
                "Faites circuler la lampe UV par les sas jusqu'au tableau des réactifs, traduisez la recette en couleurs, puis dosez. VIDER pour recommencer.",
            }, new[]
            {
                "The lab mixer needs an antidote: a number of doses from each coloured flask, then MIX.",
                "YOUR antidote's recipe is at another diver's, in molecule names. Which molecule is in which flask? It is written in UV ink in the ORANGE lab.",
                "Pass the UV lamp through the airlocks to the reagent chart, turn the recipe into colours, then dose. EMPTY to start over.",
            }),
            ["knock"] = (new[]
            {
                "Ton pupitre d'écoute, au-dessus de la plaque FRAPPER, affiche le rythme que ton partenaire doit frapper sur la cloison.",
                "Décris-lui le rythme (« 3 coups, pause, 1 coup… ») : il frappe de son côté, ton pupitre affiche ce qui est reçu.",
                "Chiffre = nombre de coups rapides ; marquez une petite pause entre les groupes, puis attendez 2 secondes à la fin.",
            }, new[]
            {
                "Your listening panel, above the KNOCK plate, shows the rhythm your partner must knock on the bulkhead.",
                "Describe the rhythm (“3 knocks, pause, 1 knock…”): they knock on their side, your panel shows what was received.",
                "Digit = number of quick knocks; leave a short pause between groups, then wait 2 seconds at the end.",
            }),
            ["floorlock"] = (new[]
            {
                "La cabine est verrouillée par un code. Chaque énigme de l'étage en révèle un chiffre, sur le verrou d'un des modules.",
                "Chacun lit sur SON verrou les fragments qu'il a reçus (« fragment 2 : 7 ») et les annonce aux autres.",
                "Composez les chiffres dans l'ordre des fragments (1, 2, 3…) sur n'importe quel verrou, puis OK.",
            }, new[]
            {
                "The cabin is locked by a code. Each puzzle on this floor reveals one digit, on the lock of one of the modules.",
                "Everyone reads the fragments on THEIR lock (“fragment 2: 7”) and calls them out to the others.",
                "Enter the digits in fragment order (1, 2, 3…) on any lock, then OK.",
            }),
            ["manifold"] = (new[]
            {
                "L'eau monte : la pompe de ton module ne démarrera qu'avec les bonnes vannes ouvertes sur le collecteur du mur du fond.",
                "Le schéma de TON collecteur est chez ton coéquipier, sur son mur du fond : lui seul peut le lire. Demande-lui couleur par couleur.",
                "Poignée parallèle au tuyau = vanne ouverte. Règle les 5 vannes comme sur le schéma, puis AMORCER LA POMPE. Une erreur referme tout.",
            }, new[]
            {
                "The water is rising: your module's pump only starts with the right valves open on the manifold on the back wall.",
                "YOUR manifold's diagram is at your teammate's, on their back wall: only they can read it. Ask them colour by colour.",
                "Handle along the pipe = valve open. Set the 5 valves like the diagram, then PRIME THE PUMP. A mistake closes everything.",
            }),
            ["ballast"] = (new[]
            {
                "Une fois les pompes amorcées et la purge terminée, il faut chasser les ballasts pour descendre.",
                "Chaque module a un levier de chasse près du mur commun : il faut les abaisser tous ensemble.",
                "Comptez à voix haute : « 3, 2, 1, maintenant ! » — moins de 2 secondes d'écart entre les leviers.",
            }, new[]
            {
                "Once the pumps are primed and the purge is done, the ballasts must be blown to go down.",
                "Each module has a blow lever near the shared wall: pull them all together.",
                "Count out loud: “3, 2, 1, now!” — less than 2 seconds between the levers.",
            }),
            ["relay"] = (new[]
            {
                "Chaque module a un panneau d'appels sur le mur extérieur. Quand toute l'équipe est prête, appuyez sur DÉMARRER.",
                "Le symbole s'affiche chez un joueur, mais le bon bouton est chez un autre : décrivez la forme, pas la couleur (« deux vagues », « l'œil »…).",
                "Inventez des noms courts pour les 6 symboles avant de démarrer : les manches accélèrent et une erreur relance la manche.",
            }, new[]
            {
                "Every module has a call panel on the outer wall. When the whole team is ready, press START.",
                "The symbol shows up for one player, but the right button is someone else's: describe the shape, not the colour (“two waves”, “the eye”…).",
                "Agree on short names for the 6 symbols before starting: rounds speed up and a mistake restarts the round.",
            }),
            ["keypad"] = (new[]
            {
                "Le code de ton digicode n'est pas chez toi : il est affiché dans le module voisin.",
                "Ton coéquipier a ton code sur son mur du fond, et lui seul peut le lire : demande-lui de te le dicter.",
                "Chaque symbole a une forme ET une couleur : dictez-les de gauche à droite (« cercle rouge, étoile jaune… »).",
            }, new[]
            {
                "Your keypad code is not in your module: it is displayed in the neighbouring one.",
                "Your teammate has your code on their back wall, and only they can read it: ask them to read it out.",
                "Each symbol has a shape AND a colour: read them left to right (“red circle, yellow star…”).",
            }),
            ["keypadUV"] = (new[]
            {
                "Ton code est écrit à l'encre invisible, dans le module voisin.",
                "Il n'y a qu'une lampe UV pour toute l'équipe, dans le module ORANGE : faites-la passer par les sas.",
                "Éclaire le mur du fond avec la lampe UV pour révéler le code, puis dicte-le à ton coéquipier.",
            }, new[]
            {
                "Your code is written in invisible ink, in the neighbouring module.",
                "There is only one UV lamp for the whole team, in the ORANGE module: pass it through the airlocks.",
                "Shine the UV lamp on the back wall to reveal the code, then read it to your teammate.",
            }),
            ["fuse"] = (new[]
            {
                "Ta prise attend un fusible de sa couleur… mais il n'est pas dans ton module.",
                "Ton coéquipier a ton fusible sur son étagère : il doit le poser dans le sas du mur commun et appuyer sur ENVOYER.",
                "Le fusible arrive sur le plateau du sas : attrape-le et branche-le dans la prise de ta couleur.",
            }, new[]
            {
                "Your socket needs a fuse of its colour… but it is not in your module.",
                "Your teammate has your fuse on their shelf: they must put it in the airlock of the shared wall and press SEND.",
                "The fuse arrives on the airlock tray: grab it and plug it into the socket of your colour.",
            }),
            ["gauge"] = (new[]
            {
                "Ta vanne ne règle pas ton manomètre : elle règle celui d'un autre plongeur.",
                "Parlez à voix haute : chacun annonce où est son aiguille par rapport à la zone verte.",
                "Tournez les vannes doucement, une à la fois : l'aiguille doit rester quelques secondes dans le vert.",
            }, new[]
            {
                "Your valve does not drive your gauge: it drives another diver's.",
                "Talk out loud: each of you says where your needle is compared to the green zone.",
                "Turn the valves slowly, one at a time: the needle must stay in the green for a few seconds.",
            }),
            ["levers"] = (new[]
            {
                "Les leviers ne répondent qu'une fois toutes les autres énigmes de l'étage résolues.",
                "Tous les leviers doivent être abaissés presque en même temps.",
                "Comptez ensemble « 3, 2, 1, maintenant ! » et tirez tous votre levier au même moment.",
            }, new[]
            {
                "The levers only respond once every other puzzle on this floor is solved.",
                "All the levers must be pulled down at almost the same time.",
                "Count together “3, 2, 1, now!” and all pull your lever at the same moment.",
            }),
            ["reactor"] = (new[]
            {
                "Le cœur du réacteur montre une suite de couleurs : chaque couleur est celle d'un plongeur.",
                "Retenez la suite, puis appuyez chacun sur votre bouton quand c'est votre tour, dans l'ordre.",
                "Annoncez les couleurs à voix haute pendant la séquence, puis rejouez-la ensemble, sans vous presser.",
            }, new[]
            {
                "The reactor core shows a sequence of colours: each colour belongs to one diver.",
                "Remember the sequence, then each press your button on your turn, in order.",
                "Call out the colours during the sequence, then replay it together, without rushing.",
            }),
            ["ascent"] = (new[]
            {
                "La remontée d'urgence se déclenche avec les leviers, une fois le réacteur relancé.",
                "Tous les leviers doivent être abaissés presque en même temps.",
                "Comptez ensemble « 3, 2, 1, maintenant ! » et tirez tous votre levier au même moment.",
            }, new[]
            {
                "The emergency ascent is triggered with the levers, once the reactor is restarted.",
                "All the levers must be pulled down at almost the same time.",
                "Count together “3, 2, 1, now!” and all pull your lever at the same moment.",
            }),
        };

        /// <summary>Journaux de bord : [étage 1..3][0 = chef de station (modules ORANGE/VERT), 1 = ingénieure (CYAN/MAGENTA)].</summary>
        public static readonly (string fr, string en)[][] CrewLogs =
        {
            new[]
            {
                ("Cmdt Réa Morel — Jour 212.\nCoupure générale à 03 h 10. Le réseau a disjoncté module par module. J'ai retiré les fusibles pour éviter l'incendie et je les ai confiés aux modules voisins. Si vous lisez ceci : remettez le courant ensemble.",
                 "Cdr Réa Morel — Day 212.\nTotal blackout at 03:10. The grid tripped module by module. I pulled the fuses to prevent a fire and left them with the neighbouring modules. If you read this: restore the power together."),
                ("Ing. Ines Valdès — Jour 212.\nLes digicodes ont été réinitialisés après la coupure. Par sécurité, chaque code s'affiche dans le module d'à côté. Personne ne peut sortir seul d'ici. C'était voulu.",
                 "Eng. Ines Valdès — Day 212.\nThe keypads were reset after the blackout. For safety, each code is shown in the next module. Nobody can get out of here alone. That was the point."),
            },
            new[]
            {
                ("Cmdt Réa Morel — Jour 213.\nLa pression grimpe au laboratoire. Les vannes ont été croisées pendant la construction : chacun règle la pression d'un autre. On a passé la nuit à se crier des chiffres à travers les cloisons.",
                 "Cdr Réa Morel — Day 213.\nPressure is rising in the lab. The valves were cross-wired during construction: each one sets someone else's pressure. We spent the night shouting numbers through the walls."),
                ("Ing. Ines Valdès — Jour 213.\nJ'ai noté les codes à l'encre UV : les créatures du dehors réagissent à la lumière vive. Une seule lampe UV fonctionne encore. Elle voyage par les sas.",
                 "Eng. Ines Valdès — Day 213.\nI wrote the codes in UV ink: the creatures outside react to bright light. Only one UV lamp still works. It travels through the airlocks."),
            },
            new[]
            {
                ("Cmdt Réa Morel — Jour 214.\nQuelque chose de très grand tourne autour de la station. Le réacteur est en sécurité. Pour le relancer, il faut toute l'équipe, chacun à sa console. Puis la remontée d'urgence.",
                 "Cdr Réa Morel — Day 214.\nSomething very large is circling the station. The reactor is in safe mode. Restarting it takes the whole team, each at their console. Then the emergency ascent."),
                ("Ing. Ines Valdès — Jour 214, dernier message.\nNous partons par la capsule de secours. Laissez les leviers de remontée pour la fin, et tirez-les ensemble. Bonne chance, plongeurs.",
                 "Eng. Ines Valdès — Day 214, last entry.\nWe are leaving in the escape pod. Keep the ascent levers for last, and pull them together. Good luck, divers."),
            },
            // Style 4 : ballasts (3e étage de la descente).
            new[]
            {
                ("Cmdt Réa Morel — Jour 213, soir.\nLa coque fuit aux ballasts. Les pompes de cale tiennent, à condition d'ouvrir les bonnes vannes. Les schémas ont été affichés dans le module voisin, comme tout le reste ici. Ne forcez pas : un coup de bélier et l'eau monte d'un coup.",
                 "Cdr Réa Morel — Day 213, evening.\nThe hull is leaking at the ballasts. The bilge pumps hold, as long as the right valves are open. The diagrams were posted in the next module, like everything else here. Don't force it: one water hammer and the water surges up."),
                ("Ing. Ines Valdès — Jour 213, nuit.\nPour descendre au cœur, il faut chasser les ballasts : les quatre leviers d'un coup. Avant ça, videz l'eau et lancez la purge. Et parlez-vous. Cette station ne pardonne pas le silence.",
                 "Eng. Ines Valdès — Day 213, night.\nTo reach the core, the ballasts must be blown: all four levers at once. Before that, pump out the water and run the purge. And talk to each other. This station does not forgive silence."),
            },
        };
    }
}
