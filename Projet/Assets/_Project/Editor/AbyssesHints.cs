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
            ["keypad"] = (new[]
            {
                "Le code de ton digicode n'est pas chez toi : il est affiché dans le module voisin.",
                "Regarde par le hublot du mur commun : ton coéquipier voit ton code sur son mur du fond. Demande-lui de te le dicter.",
                "Chaque symbole a une forme ET une couleur : dictez-les de gauche à droite (« cercle rouge, étoile jaune… »).",
            }, new[]
            {
                "Your keypad code is not in your module: it is displayed in the neighbouring one.",
                "Look through the window in the shared wall: your teammate sees your code on their back wall. Ask them to read it out.",
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
        };
    }
}
