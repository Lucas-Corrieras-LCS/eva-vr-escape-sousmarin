using System.Collections;
using System.Globalization;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 2 — cadenas à 3 molettes qui ouvre la vanne d'urgence du module. Le code est celui que clignote
    /// la <see cref="MorseBeacon"/> ; une table du morse est fixée au mur à côté.
    /// Boutons : 0..2 = molette +1, 3..5 = molette -1, 6 = valider. La saisie est locale, le serveur vérifie.
    /// </summary>
    public class CombinationLock : NetworkBehaviour, IPokeReceiver
    {
        public const int ValidateIndex = 6;

        [SerializeField] PuzzleObjective objective;
        [SerializeField] MorseBeacon beacon;
        [SerializeField] TMP_Text[] wheels;
        [SerializeField] Renderer status;
        [SerializeField] Material idleMaterial;
        [SerializeField] Material successMaterial;
        [SerializeField] Material errorMaterial;
        [SerializeField] AudioSource errorSound;
        [SerializeField] AudioSource successSound;

        readonly int[] digits = new int[MorseBeacon.Digits];
        WaitForSeconds errorDelay;

        void Awake() => errorDelay = new WaitForSeconds(1f);

        public override void OnNetworkSpawn()
        {
            objective.StateChanged += OnObjectiveChanged;
            SetStatus(objective.Solved.Value ? successMaterial : idleMaterial);
            RefreshWheels();
        }

        public override void OnNetworkDespawn() => objective.StateChanged -= OnObjectiveChanged;

        public void Pressed(int index)
        {
            if (!IsSpawned || objective.Solved.Value) return;
            if (index == ValidateIndex)
            {
                SubmitRpc(digits[0] * 100 + digits[1] * 10 + digits[2]);
                return;
            }
            int wheel = index % MorseBeacon.Digits;
            int delta = index < MorseBeacon.Digits ? 1 : -1;
            digits[wheel] = (digits[wheel] + delta + 10) % 10;
            RefreshWheels();
        }

        [Rpc(SendTo.Server)]
        void SubmitRpc(int attempt)
        {
            if (objective.Solved.Value || beacon == null) return;
            if (attempt == beacon.Code.Value) objective.ServerSolve();
            else RejectRpc();
        }

        [Rpc(SendTo.Everyone)]
        void RejectRpc()
        {
            if (errorSound != null) errorSound.Play();
            if (LocalRig.IsLocalOrShared(objective.Quadrant)) LocalRig.Pulse(0.6f, 0.25f);
            StopAllCoroutines();
            StartCoroutine(ShowError());
        }

        IEnumerator ShowError()
        {
            SetStatus(errorMaterial);
            yield return errorDelay;
            if (!objective.Solved.Value) SetStatus(idleMaterial);
        }

        void OnObjectiveChanged(bool solved)
        {
            if (!solved) return;
            StopAllCoroutines();
            SetStatus(successMaterial);
            if (successSound != null) successSound.Play();
            // Résolu par le maître du jeu : les molettes affichent le bon code.
            if (beacon != null && beacon.Code.Value >= 0)
            {
                for (int i = 0; i < digits.Length; i++) digits[i] = MorseBeacon.DigitAt(beacon.Code.Value, i);
                RefreshWheels();
            }
        }

        void RefreshWheels()
        {
            for (int i = 0; i < wheels.Length && i < digits.Length; i++)
                if (wheels[i] != null) wheels[i].text = digits[i].ToString(CultureInfo.InvariantCulture);
        }

        void SetStatus(Material material)
        {
            if (status != null && material != null) status.sharedMaterial = material;
        }
    }
}
