using System.Collections.Generic;
using UnityEngine;

namespace Abysses.Interaction
{
    /// <summary>Fusible : identifiant + couleur. À poser dans la <see cref="FuseSocket"/> correspondante.</summary>
    public class FuseItem : MonoBehaviour
    {
        [SerializeField, Range(0, 7)] int fuseId;

        /// <summary>Tous les fusibles de l'étage, actifs ou non (pour les masquer chez chaque joueur une fois posés).</summary>
        public static readonly List<FuseItem> All = new List<FuseItem>(4);

        public int FuseId => fuseId;

        void Awake() => All.Add(this);
        void OnDestroy() => All.Remove(this);
    }
}
