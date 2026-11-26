using System;
using UnityEngine;

namespace Abysses.Core
{
    /// <summary>
    /// Palette de la direction artistique « illustration sombre » (shader Abysses/Stylized), posée en variables globales :
    /// un seul réglage par étage, aucun matériau dupliqué, le SRP Batcher reste actif.
    /// </summary>
    [Serializable]
    public struct AbyssPalette
    {
        [Tooltip("Teinte au ras du sol (multiplie l'éclairage).")]
        public Color low;
        [Tooltip("Teinte en hauteur (multiplie l'éclairage).")]
        public Color high;
        [Tooltip("Couleur du contour lumineux des objets et des plongeurs.")]
        public Color rim;
        [Range(0f, 2f)] public float rimStrength;
        [Tooltip("Grain de texture conservé (0 = aplats lisses, 1 = texture complète ; ~0,7 : style doux qui garde le détail des tôles).")]
        [Range(0f, 1f)] public float grain;
        [Tooltip("Hauteurs (m) du bas et du haut du dégradé.")]
        public Vector2 gradientRange;
        [Range(0.5f, 8f)] public float rimPower;

        public static AbyssPalette Default => new AbyssPalette
        {
            low = new Color(0.4f, 0.48f, 0.55f), high = new Color(1f, 1.05f, 1.1f),
            rim = new Color(0.35f, 0.85f, 1f), rimStrength = 0.5f, grain = 0.7f,
            gradientRange = new Vector2(0f, 2.8f), rimPower = 3f,
        };
    }

    public static class AbyssStyle
    {
        static readonly int LowId = Shader.PropertyToID("_AbyssLow");
        static readonly int HighId = Shader.PropertyToID("_AbyssHigh");
        static readonly int RimId = Shader.PropertyToID("_AbyssRim");
        static readonly int ParamsId = Shader.PropertyToID("_AbyssParams");

        /// <summary>Palette actuellement posée (pour la restaurer quand un étage se décharge).</summary>
        public static AbyssPalette Current { get; private set; } = AbyssPalette.Default;

        public static void Apply(AbyssPalette palette)
        {
            Current = palette;
            Shader.SetGlobalVector(LowId, (Vector4)palette.low);
            Color high = palette.high;
            high.a = 1f; // active le style dans le shader
            Shader.SetGlobalVector(HighId, (Vector4)high);
            Color rim = palette.rim;
            rim.a = palette.rimStrength;
            Shader.SetGlobalVector(RimId, (Vector4)rim);
            Shader.SetGlobalVector(ParamsId, new Vector4(palette.grain, palette.gradientRange.x, palette.gradientRange.y, palette.rimPower));
        }

        // Dès le lancement (et dans l'éditeur), une palette par défaut : le style n'est jamais « éteint ».
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyDefault() => Apply(AbyssPalette.Default);

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void ApplyInEditor() => Apply(AbyssPalette.Default);
#endif
    }
}
