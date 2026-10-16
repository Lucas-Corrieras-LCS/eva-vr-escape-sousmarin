using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Manomètre d'un module. Sa pression est réglée par la vanne d'un AUTRE joueur (voir <see cref="PressureNetwork"/>).
    /// Il faut amener l'aiguille dans la zone verte (tirée au hasard) et l'y maintenir quelques secondes.
    /// </summary>
    public class PressureGauge : NetworkBehaviour
    {
        [SerializeField] PuzzleObjective objective;
        [Tooltip("Aiguille : tourne autour de son Z local.")]
        [SerializeField] Transform needle;
        [Tooltip("Repère de la zone verte : tourne autour de son Z local.")]
        [SerializeField] Transform targetMarker;
        [SerializeField] float angleAtZero = 120f;
        [SerializeField] float angleAtOne = -120f;
        [SerializeField, Range(0.02f, 0.2f)] float tolerance = 0.06f;
        [SerializeField, Min(0.2f)] float holdTime = 1.5f;
        [SerializeField, Min(0.1f)] float needleSpeed = 1.5f;
        [SerializeField] Renderer statusLight;
        [SerializeField] Material idleMaterial;
        [SerializeField] Material inBandMaterial;
        [SerializeField] Material solvedMaterial;

        public readonly NetworkVariable<float> Pressure = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<float> Target = new NetworkVariable<float>(0.5f);

        public PuzzleObjective Objective => objective;

        float shown;
        Coroutine holding;

        public override void OnNetworkSpawn()
        {
            if (IsServer) Target.Value = Random.Range(0.25f, 0.85f);
            Target.OnValueChanged += OnTargetChanged;
            Pressure.OnValueChanged += OnPressureChanged;
            objective.StateChanged += OnSolvedChanged;
            shown = Pressure.Value;
            PlaceMarker();
            ApplyNeedle();
            RefreshStatus();
        }

        public override void OnNetworkDespawn()
        {
            Target.OnValueChanged -= OnTargetChanged;
            Pressure.OnValueChanged -= OnPressureChanged;
            objective.StateChanged -= OnSolvedChanged;
        }

        /// <summary>Serveur : nouvelle pression envoyée par la vanne qui pilote ce manomètre.</summary>
        public void ServerSetPressure(float value)
        {
            if (!IsServer || objective.Solved.Value) return;
            Pressure.Value = value;

            bool inBand = IsInBand(value);
            if (inBand && holding == null) holding = StartCoroutine(HoldThenSolve());
            else if (!inBand && holding != null)
            {
                StopCoroutine(holding);
                holding = null;
            }
        }

        bool IsInBand(float value) => Mathf.Abs(value - Target.Value) <= tolerance;

        IEnumerator HoldThenSolve()
        {
            yield return new WaitForSeconds(holdTime);
            holding = null;
            objective.ServerSolve();
        }

        void OnTargetChanged(float previous, float current) => PlaceMarker();
        void OnPressureChanged(float previous, float current) => RefreshStatus();
        void OnSolvedChanged(bool solved) => RefreshStatus();

        void Update()
        {
            float target = objective.Solved.Value ? Target.Value : Pressure.Value;
            if (Mathf.Approximately(shown, target)) return;
            shown = Mathf.MoveTowards(shown, target, needleSpeed * Time.deltaTime);
            ApplyNeedle();
        }

        void ApplyNeedle() => needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(angleAtZero, angleAtOne, shown));

        void PlaceMarker()
        {
            if (targetMarker != null)
                targetMarker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(angleAtZero, angleAtOne, Target.Value));
        }

        void RefreshStatus()
        {
            if (statusLight == null) return;
            Material material = objective.Solved.Value ? solvedMaterial : IsInBand(Pressure.Value) ? inBandMaterial : idleMaterial;
            if (material != null) statusLight.sharedMaterial = material;
        }
    }
}
