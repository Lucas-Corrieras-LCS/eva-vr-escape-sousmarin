namespace Abysses.Core
{
    /// <summary>
    /// Rotation des rôles entre joueurs : pour chaque énigme, l'info d'un joueur s'affiche chez un AUTRE joueur présent,
    /// décalé de <c>shift</c> places dans l'anneau physique de la piste (Q0 → Q1 → Q3 → Q2 → Q0), en ne gardant que les
    /// modules occupés. Chaque énigme a son propre décalage : les binômes changent d'une énigme à l'autre.
    /// - 2 joueurs : chacun lit pour l'autre (décalage 1, toujours) ;
    /// - 3 joueurs : décalage 1 ou 2 (sens horaire ou inverse) ;
    /// - 4 joueurs : 1 = voisin d'anneau, 2 = diagonale, 3 = voisin dans l'autre sens.
    /// Calcul déterministe à partir du masque des modules actifs : identique chez tous les clients, sans réseau.
    /// </summary>
    public static class Routing
    {
        static readonly int[] Ring = { 0, 1, 3, 2 };

        /// <summary>Joueurs actifs dans l'ordre de l'anneau.</summary>
        public static int Collect(int activeMask, int[] buffer)
        {
            int count = 0;
            for (int i = 0; i < Ring.Length; i++)
                if ((activeMask & (1 << Ring[i])) != 0) buffer[count++] = Ring[i];
            return count;
        }

        /// <summary>Module où s'affiche l'info du joueur <paramref name="owner"/> (lui-même s'il est seul).</summary>
        public static int ReaderOf(int owner, int activeMask, int shift)
        {
            var players = new int[4];
            int count = Collect(activeMask, players);
            int index = System.Array.IndexOf(players, owner, 0, count);
            if (index < 0 || count < 2) return owner;
            int step = 1 + Mod(shift - 1, count - 1);
            return players[(index + step) % count];
        }

        /// <summary>Joueur dont l'info s'affiche dans le module <paramref name="reader"/> (-1 si aucun).</summary>
        public static int OwnerReadBy(int reader, int activeMask, int shift)
        {
            for (int q = 0; q < 4; q++)
                if ((activeMask & (1 << q)) != 0 && q != reader && ReaderOf(q, activeMask, shift) == reader) return q;
            return (activeMask & (1 << reader)) != 0 && ReaderOf(reader, activeMask, shift) == reader ? reader : -1;
        }

        /// <summary>Module qui affiche le fragment n° <paramref name="index"/> (méta-énigme d'étage), réparti sur l'anneau.</summary>
        public static int FragmentHolder(int index, int activeMask, int shift)
        {
            var players = new int[4];
            int count = Collect(activeMask, players);
            if (count == 0) return 0;
            return players[Mod(index + shift, count)];
        }

        static int Mod(int a, int m) => m <= 0 ? 0 : ((a % m) + m) % m;
    }
}
