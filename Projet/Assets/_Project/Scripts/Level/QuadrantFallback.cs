using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Abysses.Core;

namespace Abysses.Level
{
    /// <summary>
    /// Adaptation au nombre de joueurs : quand le module « source » est inoccupé, son aide arrive ailleurs.
    /// - Affiche des objets de secours (ex. écran de surveillance montrant le code du voisin absent).
    /// - Côté serveur, déplace des objets réseau (ex. le fusible du voisin absent) vers un point de secours.
    /// </summary>
    public class QuadrantFallback : MonoBehaviour
    {
        [Tooltip("Module dont on compense l'absence.")]
        [SerializeField, Range(0, 3)] int sourceQuadrant;
        [Tooltip("Activés seulement si le module source est inoccupé.")]
        [SerializeField] GameObject[] showWhenAbsent;
        [Tooltip("Objets réseau à déplacer (serveur) si le module source est inoccupé.")]
        [SerializeField] NetworkTransform[] relocate;
        [SerializeField] Transform[] relocateTargets;

        bool relocated;

        void OnEnable()
        {
            GameStateManager.PhaseChanged += OnPhaseChanged;
            Refresh();
        }

        void OnDisable() => GameStateManager.PhaseChanged -= OnPhaseChanged;

        void OnPhaseChanged(GamePhase phase) => Refresh();

        void Refresh()
        {
            GameStateManager state = GameStateManager.Instance;
            bool known = state != null && state.Phase.Value == GamePhase.Playing;
            bool absent = known && (state.ActiveMask.Value & (1 << sourceQuadrant)) == 0;

            for (int i = 0; i < showWhenAbsent.Length; i++)
                if (showWhenAbsent[i] != null) showWhenAbsent[i].SetActive(absent);

            if (!absent || relocated || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            relocated = true;
            for (int i = 0; i < relocate.Length && i < relocateTargets.Length; i++)
            {
                NetworkTransform item = relocate[i];
                if (item == null || relocateTargets[i] == null || !item.IsSpawned) continue;
                item.NetworkObject.RemoveOwnership();
                Transform target = relocateTargets[i];
                item.Teleport(target.position, target.rotation, item.transform.localScale);
            }
        }

        void OnValidate()
        {
            if (!PlayArea.IsValid(sourceQuadrant)) sourceQuadrant = 0;
        }
    }
}
