using System;
using UnityEngine;

namespace Abysses.Core
{
    public enum Language { Francais, English }

    /// <summary>
    /// Langue de l'interface (FR / EN, pour les touristes chez EVA). Choix local à chaque casque, mémorisé.
    /// Les textes sont écrits en paires sur place (<c>Loc.T("Bonjour", "Hello")</c>) : pas de table de clés à maintenir,
    /// aucune allocation (les chaînes sont des littéraux).
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "abysses.language";

        static Language current;
        static bool loaded;

        /// <summary>La langue vient de changer : chaque texte affiché se réapplique.</summary>
        public static event Action Changed;

        public static Language Current
        {
            get
            {
                // Lecture paresseuse : PlayerPrefs est interdit dans les initialiseurs statiques (constructeurs de MonoBehaviour).
                if (!loaded)
                {
                    loaded = true;
                    current = (Language)PlayerPrefs.GetInt(PrefKey, (int)Language.Francais);
                }
                return current;
            }
            set
            {
                if (value == Current) return;
                current = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                Changed?.Invoke();
            }
        }

        public static bool English => Current == Language.English;

        public static string T(string fr, string en) => English && !string.IsNullOrEmpty(en) ? en : fr;

        public static void Toggle() => Current = English ? Language.Francais : Language.English;
    }
}
