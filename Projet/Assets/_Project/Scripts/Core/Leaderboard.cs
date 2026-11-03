using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Abysses.Core
{
    /// <summary>
    /// Classement des équipes (meilleurs temps), conservé par l'hôte d'une session à l'autre (PlayerPrefs).
    /// Format brut compact, transmis tel quel aux casques : « secondes,joueurs,facile,aaaa-mm-jj; » × 5 max.
    /// Chaque casque le met en forme dans sa langue.
    /// </summary>
    public static class Leaderboard
    {
        const string PrefKey = "abysses.leaderboard";
        public const int MaxEntries = 5;

        public struct Entry
        {
            public int Seconds;
            public int Players;
            public bool Easy;
            public string Date;
        }

        public static string LoadRaw() => PlayerPrefs.GetString(PrefKey, string.Empty);

        /// <summary>Ajoute un temps, garde les meilleurs et renvoie le classement brut mis à jour.</summary>
        public static string Record(int seconds, int players, bool easy)
        {
            List<Entry> entries = Parse(LoadRaw());
            entries.Add(new Entry
            {
                Seconds = Mathf.Max(0, seconds),
                Players = players,
                Easy = easy,
                Date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });
            entries.Sort((a, b) => a.Seconds.CompareTo(b.Seconds));
            if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

            var raw = new StringBuilder();
            foreach (Entry e in entries)
                raw.Append(e.Seconds.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(e.Players.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(e.Easy ? '1' : '0').Append(',').Append(e.Date).Append(';');
            string result = raw.ToString();
            PlayerPrefs.SetString(PrefKey, result);
            PlayerPrefs.Save();
            return result;
        }

        public static List<Entry> Parse(string raw)
        {
            var entries = new List<Entry>(MaxEntries + 1);
            if (string.IsNullOrEmpty(raw)) return entries;
            foreach (string item in raw.Split(';'))
            {
                string[] f = item.Split(',');
                if (f.Length < 4 || !int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)) continue;
                int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int players);
                entries.Add(new Entry { Seconds = seconds, Players = players, Easy = f[2] == "1", Date = f[3] });
            }
            return entries;
        }

        /// <summary>Classement lisible (une ligne par équipe), dans la langue courante.</summary>
        public static string Format(string raw)
        {
            List<Entry> entries = Parse(raw);
            if (entries.Count == 0) return Loc.T("Aucun temps enregistré pour l'instant.", "No times recorded yet.");
            var text = new StringBuilder(Loc.T("Meilleures équipes", "Best teams")).Append('\n');
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                text.Append(i + 1).Append(".  ").Append(Duration(e.Seconds)).Append("  ·  ")
                    .Append(e.Players).Append(Loc.T(e.Players > 1 ? " plongeurs" : " plongeur", e.Players > 1 ? " divers" : " diver"));
                if (e.Easy) text.Append(Loc.T("  ·  facile", "  ·  easy"));
                text.Append('\n');
            }
            return text.ToString();
        }

        public static string Duration(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
