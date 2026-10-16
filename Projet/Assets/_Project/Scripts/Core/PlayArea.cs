using UnityEngine;

namespace Abysses.Core
{
    /// <summary>
    /// Géométrie de la piste EVA (31 x 16,5 m) découpée en 4 quadrants de 15 x 8 m.
    /// Le monde virtuel est calqué sur la piste : origine = coin du quadrant 0, X = longueur, Z = largeur.
    ///
    ///   Q2 | Q3      (z de 8,5 à 16,5)
    ///   Q0 | Q1      (z de 0 à 8)
    ///   x : Q0/Q2 de 0 à 15, Q1/Q3 de 16 à 31
    /// </summary>
    public static class PlayArea
    {
        public const float ArenaWidth = 31f;
        public const float ArenaDepth = 16.5f;
        public const float QuadrantWidth = 15f;
        public const float QuadrantDepth = 8f;
        /// <summary>Marge de sécurité : dérive possible du tracking de 50 cm / 30 min (consigne C10).</summary>
        public const float SafetyMargin = 0.5f;
        /// <summary>Retrait des murs virtuels par rapport au bord du quadrant : ils apparaissent avant la limite physique.</summary>
        public const float WallInset = 0.3f;
        public const int QuadrantCount = 4;

        static readonly Vector2[] QuadrantMin =
        {
            new Vector2(0f, 0f),
            new Vector2(16f, 0f),
            new Vector2(0f, 8.5f),
            new Vector2(16f, 8.5f),
        };

        /// <summary>Direction regardée pendant la calibration (vers le fond de la piste).</summary>
        public static Vector3 CalibrationForward => Vector3.forward;

        public static bool IsValid(int quadrant) => quadrant >= 0 && quadrant < QuadrantCount;

        /// <summary>Centre du quadrant au niveau du sol (point de calibration).</summary>
        public static Vector3 GetCenter(int quadrant)
        {
            Vector2 min = QuadrantMin[quadrant];
            return new Vector3(min.x + QuadrantWidth * 0.5f, 0f, min.y + QuadrantDepth * 0.5f);
        }

        /// <summary>Rectangle (XZ) du quadrant, réduit de la marge demandée.</summary>
        public static Rect GetRect(int quadrant, float margin = 0f)
        {
            Vector2 min = QuadrantMin[quadrant];
            return new Rect(min.x + margin, min.y + margin, QuadrantWidth - 2f * margin, QuadrantDepth - 2f * margin);
        }

        /// <summary>Vrai si la position (XZ) est dans la zone jouable sûre du quadrant.</summary>
        public static bool IsInside(int quadrant, Vector3 worldPosition, float margin = SafetyMargin)
        {
            return GetRect(quadrant, margin).Contains(new Vector2(worldPosition.x, worldPosition.z));
        }
    }
}
