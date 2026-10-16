using UnityEngine;

namespace Abysses.Interaction
{
    /// <summary>Fusible : identifiant + couleur. À poser dans la <see cref="FuseSocket"/> correspondante.</summary>
    public class FuseItem : MonoBehaviour
    {
        [SerializeField, Range(0, 7)] int fuseId;

        public int FuseId => fuseId;
    }
}
