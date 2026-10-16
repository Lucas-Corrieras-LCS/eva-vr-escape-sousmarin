using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Digicode à symboles. Le code est tiré au hasard par le serveur à chaque partie
    /// et affiché ailleurs (<see cref="CodeDisplay"/>, en général sur le mur du module voisin).
    /// La saisie est locale ; seule la proposition complète part au serveur.
    /// </summary>
    public class CodeKeypad : NetworkBehaviour
    {
        public const int CodeLength = 4;
        public const int SymbolCount = 6;
        const int BitsPerSymbol = 3;

        [SerializeField] PuzzleObjective objective;
        [Tooltip("Cases d'affichage de la saisie (4).")]
        [SerializeField] Renderer[] entrySlots;
        [Tooltip("Un matériau par symbole (6).")]
        [SerializeField] Material[] symbolMaterials;
        [SerializeField] Material emptySlotMaterial;
        [SerializeField] Renderer statusLight;
        [SerializeField] Material idleMaterial;
        [SerializeField] Material successMaterial;
        [SerializeField] Material errorMaterial;
        [SerializeField] AudioSource keySound;
        [SerializeField] AudioSource errorSound;
        [SerializeField] AudioSource successSound;

        public readonly NetworkVariable<int> Code = new NetworkVariable<int>(-1);

        /// <summary>Le code vient d'être connu (pour les afficheurs).</summary>
        public event Action<int> CodeChanged;

        public Material[] SymbolMaterials => symbolMaterials;

        readonly int[] entry = new int[CodeLength];
        int entryCount;
        WaitForSeconds errorDelay;

        public static int SymbolAt(int code, int index) => (code >> (index * BitsPerSymbol)) & ((1 << BitsPerSymbol) - 1);

        void Awake() => errorDelay = new WaitForSeconds(1f);

        public override void OnNetworkSpawn()
        {
            if (IsServer) Code.Value = GenerateCode();
            Code.OnValueChanged += OnCodeChanged;
            objective.StateChanged += OnObjectiveChanged;
            ClearEntry();
            SetStatus(objective.Solved.Value ? successMaterial : idleMaterial);
            CodeChanged?.Invoke(Code.Value);
        }

        public override void OnNetworkDespawn()
        {
            Code.OnValueChanged -= OnCodeChanged;
            objective.StateChanged -= OnObjectiveChanged;
        }

        static int GenerateCode()
        {
            int code = 0;
            for (int i = 0; i < CodeLength; i++)
                code |= UnityEngine.Random.Range(0, SymbolCount) << (i * BitsPerSymbol);
            return code;
        }

        /// <summary>Appelé par un bouton du digicode (local).</summary>
        public void Press(int symbol)
        {
            if (!IsSpawned || objective.Solved.Value || entryCount >= CodeLength) return;
            if (keySound != null) keySound.Play();

            entry[entryCount] = symbol;
            if (entryCount < entrySlots.Length && entrySlots[entryCount] != null)
                entrySlots[entryCount].sharedMaterial = symbolMaterials[symbol];
            entryCount++;

            if (entryCount == CodeLength)
            {
                int attempt = 0;
                for (int i = 0; i < CodeLength; i++) attempt |= entry[i] << (i * BitsPerSymbol);
                SubmitRpc(attempt);
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitRpc(int attempt)
        {
            if (objective.Solved.Value) return;
            if (attempt == Code.Value) objective.ServerSolve();
            else RejectRpc();
        }

        [Rpc(SendTo.Everyone)]
        void RejectRpc()
        {
            if (errorSound != null) errorSound.Play();
            StopAllCoroutines();
            StartCoroutine(ShowError());
        }

        IEnumerator ShowError()
        {
            SetStatus(errorMaterial);
            yield return errorDelay;
            ClearEntry();
            if (!objective.Solved.Value) SetStatus(idleMaterial);
        }

        void OnCodeChanged(int previous, int current) => CodeChanged?.Invoke(current);

        void OnObjectiveChanged(bool solved)
        {
            if (!solved) return;
            StopAllCoroutines();
            SetStatus(successMaterial);
            if (successSound != null) successSound.Play();
        }

        void ClearEntry()
        {
            entryCount = 0;
            for (int i = 0; i < entrySlots.Length; i++)
                if (entrySlots[i] != null) entrySlots[i].sharedMaterial = emptySlotMaterial;
        }

        void SetStatus(Material material)
        {
            if (statusLight != null && material != null) statusLight.sharedMaterial = material;
        }
    }
}
