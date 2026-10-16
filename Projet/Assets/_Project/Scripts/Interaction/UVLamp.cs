using System.Collections.Generic;
using UnityEngine;

namespace Abysses.Interaction
{
    /// <summary>
    /// Lampe UV (objet réseau attrapable). Son faisceau révèle les inscriptions <see cref="UVReveal"/>.
    /// Chaque client calcule la révélation à partir de la position synchronisée de la lampe : tout le monde voit la même chose.
    /// </summary>
    public class UVLamp : MonoBehaviour
    {
        [Tooltip("Spot violet : sa portée et son angle définissent la zone révélée.")]
        [SerializeField] Light beam;

        public static readonly List<UVLamp> Active = new List<UVLamp>(2);

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        public bool Illuminates(Vector3 point)
        {
            Transform origin = beam.transform;
            Vector3 toPoint = point - origin.position;
            float sqrDistance = toPoint.sqrMagnitude;
            if (sqrDistance > beam.range * beam.range) return false;
            return Vector3.Angle(origin.forward, toPoint) <= beam.spotAngle * 0.5f;
        }
    }
}
