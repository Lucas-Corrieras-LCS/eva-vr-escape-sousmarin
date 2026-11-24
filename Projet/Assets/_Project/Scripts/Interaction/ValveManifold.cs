using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 3 (ballasts) — collecteur de vannes, énigme croisée :
    /// 5 vannes de couleur sur le collecteur de ce module ; la bonne combinaison (ouvertes / fermées) est dessinée sur le
    /// schéma hydraulique du module PARTENAIRE, visible de lui seul. On règle les vannes, puis « AMORCER LA POMPE » :
    /// - bonne combinaison : la pompe démarre, l'eau du module est vidée ;
    /// - mauvaise : coup de bélier (vapeur, choc, l'eau monte d'un coup) et toutes les vannes se referment.
    /// Poignée parallèle au tuyau = vanne ouverte (convention réelle). Boutons : 0..4 = vannes, 5 = amorcer.
    /// </summary>
    public class ValveManifold : NetworkBehaviour, IPokeReceiver
    {
        public const int Count = 5;
        public const int PrimeIndex = Count;

        static readonly string[] ColorsFr = { "ROUGE", "VERTE", "BLEUE", "JAUNE", "BLANCHE" };
        static readonly string[] ColorsEn = { "RED", "GREEN", "BLUE", "YELLOW", "WHITE" };

        [SerializeField] PuzzleObjective objective;
        [Tooltip("Pivots des poignées (rotation autour du Z local) : 0° = parallèle au tuyau (ouverte), 90° = fermée.")]
        [SerializeField] Transform[] handles;
        [Tooltip("Icônes du schéma (Count par copie : schéma chez le partenaire, puis copie de repli).")]
        [SerializeField] Renderer[] diagramIcons;
        [SerializeField] Material iconOpen;
        [SerializeField] Material iconClosed;
        [SerializeField] TMP_Text[] diagramTexts;
        [SerializeField] Renderer statusLamp;
        [SerializeField] Material lampIdle;
        [SerializeField] Material lampOk;
        [SerializeField] Material lampError;
        [SerializeField] AudioSource turnSound;
        [SerializeField] AudioSource surgeSound;
        [SerializeField] AudioSource successSound;
        [SerializeField] ParticleSystem steam;
        [Tooltip("Eau du module : coup de bélier = montée brusque, pompe amorcée = vidange.")]
        [SerializeField] RisingWater water;
        [SerializeField, Min(0.01f)] float surgeHeight = 0.07f;

        /// <summary>Vannes à ouvrir (masque), tiré par le serveur : 2 ou 3 vannes.</summary>
        public readonly NetworkVariable<int> Target = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Open = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Surges = new NetworkVariable<int>(0);

        public static string ColorName(int valve) => Loc.T(ColorsFr[valve], ColorsEn[valve]);

        readonly float[] angles = new float[Count];

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                int mask;
                do mask = Random.Range(1, 1 << Count);
                while (Bits(mask) < 2 || Bits(mask) > 3);
                Target.Value = mask;
                Open.Value = 0;
            }
            Target.OnValueChanged += OnTargetChanged;
            Surges.OnValueChanged += OnSurge;
            objective.StateChanged += OnSolved;
            Loc.Changed += RefreshDiagram;
            for (int i = 0; i < Count; i++) angles[i] = Goal(i);
            ApplyHandles();
            RefreshDiagram();
            RefreshLamp(false);
        }

        public override void OnNetworkDespawn()
        {
            Target.OnValueChanged -= OnTargetChanged;
            Surges.OnValueChanged -= OnSurge;
            objective.StateChanged -= OnSolved;
            Loc.Changed -= RefreshDiagram;
        }

        public void Pressed(int index)
        {
            if (!IsSpawned || objective.Solved.Value) return;
            if (index == PrimeIndex) PrimeRpc();
            else if (index >= 0 && index < Count)
            {
                ToggleRpc(index);
                if (turnSound != null) turnSound.Play();
            }
        }

        [Rpc(SendTo.Server)]
        void ToggleRpc(int valve)
        {
            if (objective.Solved.Value) return;
            Open.Value ^= 1 << valve;
        }

        [Rpc(SendTo.Server)]
        void PrimeRpc()
        {
            if (objective.Solved.Value || Target.Value < 0) return;
            if (Open.Value == Target.Value)
            {
                objective.ServerSolve();
                return;
            }
            Surges.Value++;
            Open.Value = 0;
        }

        float Goal(int valve) => (Open.Value & (1 << valve)) != 0 ? 0f : 90f;

        void Update()
        {
            // Les poignées tournent en un quart de seconde vers leur position (ouverte / fermée).
            bool moving = false;
            for (int i = 0; i < Count; i++)
            {
                float goal = Goal(i);
                if (Mathf.Approximately(angles[i], goal)) continue;
                angles[i] = Mathf.MoveTowards(angles[i], goal, 360f * Time.deltaTime);
                moving = true;
            }
            if (moving) ApplyHandles();
        }

        void ApplyHandles()
        {
            for (int i = 0; i < Count && i < handles.Length; i++)
                if (handles[i] != null) handles[i].localRotation = Quaternion.Euler(0f, 0f, angles[i]);
        }

        void OnTargetChanged(int previous, int current) => RefreshDiagram();

        void RefreshDiagram()
        {
            int target = Target.Value;
            for (int i = 0; i < diagramIcons.Length; i++)
            {
                if (diagramIcons[i] == null) continue;
                int valve = i % Count;
                diagramIcons[i].sharedMaterial = target >= 0 && (target & (1 << valve)) != 0 ? iconOpen : iconClosed;
            }
            if (target < 0) return;
            var text = new System.Text.StringBuilder();
            for (int v = 0; v < Count; v++)
            {
                bool open = (target & (1 << v)) != 0;
                text.Append(ColorName(v)).Append(open ? Loc.T("  ouverte", "  open") : Loc.T("  fermée", "  closed"));
                if (v < Count - 1) text.Append('\n');
            }
            for (int i = 0; i < diagramTexts.Length; i++)
                if (diagramTexts[i] != null) diagramTexts[i].text = text.ToString();
        }

        void OnSurge(int previous, int current)
        {
            if (current <= previous) return;
            if (surgeSound != null) surgeSound.Play();
            if (steam != null) steam.Play();
            if (water != null) water.Surge(surgeHeight);
            if (LocalRig.IsLocalOrShared(objective.Quadrant)) LocalRig.Pulse(0.9f, 0.4f);
            RefreshLamp(true);
            CancelInvoke(nameof(ResetLamp));
            Invoke(nameof(ResetLamp), 1.5f);
        }

        void ResetLamp() => RefreshLamp(false);

        void OnSolved(bool solved)
        {
            if (!solved) return;
            if (successSound != null) successSound.Play();
            if (water != null) water.Drain();
            RefreshLamp(false);
        }

        void RefreshLamp(bool error)
        {
            if (statusLamp == null) return;
            statusLamp.sharedMaterial = objective.Solved.Value ? lampOk : error ? lampError : lampIdle;
        }

        static int Bits(int value)
        {
            int count = 0;
            for (; value != 0; value &= value - 1) count++;
            return count;
        }
    }
}
