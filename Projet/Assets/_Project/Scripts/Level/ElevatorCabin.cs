using System.Globalization;
using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Player;

namespace Abysses.Level
{
    /// <summary>
    /// Cabine du bathyscaphe d'un module (consigne C8 : étendre l'univers).
    /// Toujours au même endroit physique : seul l'étage autour change pendant la descente.
    /// - Portes pilotées par la phase de jeu.
    /// - La cabine du joueur local signale au serveur si sa tête est dedans (prêt à descendre).
    /// - Pendant la descente : bulles, grondement, lumière, profondeur qui défile.
    /// </summary>
    public class ElevatorCabin : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] int quadrant;

        [Header("Portes (glissent sur leur axe X local)")]
        [SerializeField] Transform leftDoor;
        [SerializeField] Transform rightDoor;
        [SerializeField] float doorOpenOffset = 0.65f;
        [SerializeField] float doorSpeed = 0.9f;

        [Header("Intérieur (espace local) pour détecter la tête du joueur")]
        [SerializeField] Vector3 interiorCenter = new Vector3(0f, 1.2f, 0f);
        [SerializeField] Vector3 interiorSize = new Vector3(2.2f, 2.4f, 2.2f);

        [Header("Ambiance")]
        [SerializeField] Light cabinLight;
        [SerializeField] Color idleColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] Color boardColor = new Color(0.3f, 1f, 0.55f);
        [SerializeField] Color descentColor = new Color(1f, 0.45f, 0.2f);
        [SerializeField] ParticleSystem bubbles;
        [SerializeField] AudioSource rumble;
        [SerializeField] TMP_Text depthText;

        const float PresenceCheckInterval = 0.2f;

        Vector3 leftClosed;
        Vector3 rightClosed;
        float doorOpen;
        float doorTarget;
        bool reportedInside;
        float nextPresenceCheck;

        bool descending;
        float descentStart;
        int depthFrom;
        int depthTo;
        int shownDepth = int.MinValue;

        public int Quadrant => quadrant;

        void Awake()
        {
            if (leftDoor != null) leftClosed = leftDoor.localPosition;
            if (rightDoor != null) rightClosed = rightDoor.localPosition;
            SetDepth(0);
        }

        void OnEnable() => GameStateManager.PhaseChanged += OnPhaseChanged;
        void OnDisable() => GameStateManager.PhaseChanged -= OnPhaseChanged;

        void OnPhaseChanged(GamePhase phase)
        {
            GameStateManager state = GameStateManager.Instance;
            doorTarget = phase == GamePhase.Playing || phase == GamePhase.FloorSolved || phase == GamePhase.Finished ? 1f : 0f;

            if (cabinLight != null)
                cabinLight.color = phase == GamePhase.FloorSolved ? boardColor : phase == GamePhase.Descending ? descentColor : idleColor;

            bool wasDescending = descending;
            descending = phase == GamePhase.Descending;
            if (descending && !wasDescending && state != null)
            {
                descentStart = Time.unscaledTime;
                depthFrom = state.DepthOf(state.Floor.Value);
                depthTo = state.DepthOf(state.Floor.Value + 1);
                if (bubbles != null) bubbles.Play();
                if (rumble != null) rumble.Play();
            }
            else if (!descending && wasDescending)
            {
                if (bubbles != null) bubbles.Stop();
                if (rumble != null) rumble.Stop();
                if (state != null) SetDepth(state.DepthOf(state.Floor.Value));
            }

            // Nouvelle phase : on renverra l'état de présence si besoin.
            reportedInside = false;
            nextPresenceCheck = 0f;
        }

        void Update()
        {
            AnimateDoors();
            if (descending) AnimateDepth();
            CheckLocalPresence();
        }

        void AnimateDoors()
        {
            if (Mathf.Approximately(doorOpen, doorTarget)) return;
            doorOpen = Mathf.MoveTowards(doorOpen, doorTarget, doorSpeed * Time.deltaTime);
            float offset = doorOpen * doorOpenOffset;
            if (leftDoor != null) leftDoor.localPosition = leftClosed + Vector3.left * offset;
            if (rightDoor != null) rightDoor.localPosition = rightClosed + Vector3.right * offset;
        }

        void AnimateDepth()
        {
            GameStateManager state = GameStateManager.Instance;
            float duration = state != null ? state.DescentDuration : 8f;
            float t = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - descentStart) / duration);
            SetDepth(Mathf.RoundToInt(Mathf.Lerp(depthFrom, depthTo, t) / 10f) * 10);
        }

        void SetDepth(int meters)
        {
            // Texte mis à jour seulement quand la valeur change (pas d'allocation à chaque frame).
            if (depthText == null || meters == shownDepth) return;
            shownDepth = meters;
            depthText.text = meters == 0 ? "SURFACE" : "-" + meters.ToString(CultureInfo.InvariantCulture) + " m";
        }

        void CheckLocalPresence()
        {
            GameStateManager state = GameStateManager.Instance;
            LocalRig rig = LocalRig.Instance;
            if (state == null || rig == null || rig.Quadrant != quadrant) return;
            if (state.Phase.Value != GamePhase.FloorSolved) return;
            if (Time.unscaledTime < nextPresenceCheck) return;
            nextPresenceCheck = Time.unscaledTime + PresenceCheckInterval;

            Vector3 local = transform.InverseTransformPoint(rig.Head.position) - interiorCenter;
            Vector3 half = interiorSize * 0.5f;
            bool inside = Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
            if (inside == reportedInside) return;
            reportedInside = inside;
            state.SetReadyRpc(quadrant, inside);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.4f);
            Gizmos.DrawWireCube(interiorCenter, interiorSize);
        }

        void OnValidate()
        {
            if (!PlayArea.IsValid(quadrant)) quadrant = 0;
        }
    }
}
