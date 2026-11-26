using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 1 — « Code frappé sur la coque » (mur entre deux partenaires, <see cref="KnockWall"/>) :
    /// chaque plongeur a, sur son pupitre d'écoute, un rythme de détresse que SON PARTENAIRE doit frapper sur la cloison
    /// (ex. « 3 · 1 · 2 » = 3 coups, pause, 1 coup, pause, 2 coups). Il le lui décrit ; le partenaire frappe ; le pupitre
    /// de l'émetteur affiche ce qui a été reçu. Seuls les coups réellement frappés comptent (détection serveur).
    /// Une pause de plus de 0,8 s sépare deux groupes ; 2 s de silence terminent le message.
    /// Partenaire absent : l'énigme de ce côté est résolue d'office.
    /// </summary>
    public class KnockCode : NetworkBehaviour
    {
        const float GroupGap = 0.8f;
        const float EndGap = 2f;

        [SerializeField] KnockWall wall;
        [Tooltip("Objectif par côté : indice 0 = code frappé PAR le module A du mur, 1 = par le module B.")]
        [SerializeField] PuzzleObjective[] objectives;
        [Tooltip("Pupitre de chaque côté : rythme demandé (au partenaire) et rythme reçu.")]
        [SerializeField] TMP_Text[] panelTexts;
        [SerializeField] AudioSource[] successSounds;

        /// <summary>Rythme attendu pour chaque côté frappeur, en chiffres (ex. 312).</summary>
        public readonly NetworkVariable<int> PatternA = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> PatternB = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> ReceivedA = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> ReceivedB = new NetworkVariable<int>(-1);

        readonly List<float>[] knocks = { new List<float>(), new List<float>() };

        public int Pattern(int side) => side == 0 ? PatternA.Value : PatternB.Value;
        public PuzzleObjective ObjectiveOf(int side) => objectives[side];

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                PatternA.Value = RandomPattern();
                PatternB.Value = RandomPattern();
                KnockWall.ServerKnocked += OnKnock;
            }
            PatternA.OnValueChanged += OnIntChanged;
            PatternB.OnValueChanged += OnIntChanged;
            ReceivedA.OnValueChanged += OnIntChanged;
            ReceivedB.OnValueChanged += OnIntChanged;
            foreach (PuzzleObjective objective in objectives) objective.StateChanged += OnSolved;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            GameStateManager.ModulesChanged += Refresh;
            Loc.Changed += Refresh;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            KnockWall.ServerKnocked -= OnKnock;
            PatternA.OnValueChanged -= OnIntChanged;
            PatternB.OnValueChanged -= OnIntChanged;
            ReceivedA.OnValueChanged -= OnIntChanged;
            ReceivedB.OnValueChanged -= OnIntChanged;
            foreach (PuzzleObjective objective in objectives) objective.StateChanged -= OnSolved;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            GameStateManager.ModulesChanged -= Refresh;
            Loc.Changed -= Refresh;
        }

        static int RandomPattern()
        {
            int pattern = 0;
            for (int i = 0; i < 3; i++) pattern = pattern * 10 + Random.Range(1, 5);
            return pattern;
        }

        void OnIntChanged(int previous, int current) => Refresh();

        readonly bool[] played = new bool[2];

        void OnSolved(bool solved)
        {
            // Un seul son par côté, au moment où CE côté est résolu (avant : résoudre B rejouait aussi le son de A).
            for (int side = 0; side < 2; side++)
            {
                bool now = objectives[side].Solved.Value;
                if (now && !played[side] && side < successSounds.Length && successSounds[side] != null) successSounds[side].Play();
                played[side] = now;
            }
            Refresh();
        }

        void OnPhaseChanged(GamePhase phase)
        {
            // Sans partenaire de l'autre côté du mur, personne ne peut frapper ni écouter : résolu d'office.
            if (IsServer && phase == GamePhase.Playing && GameStateManager.Instance != null)
            {
                int active = GameStateManager.Instance.ActiveMask.Value;
                bool both = (active & (1 << wall.QuadrantOf(0))) != 0 && (active & (1 << wall.QuadrantOf(1))) != 0;
                if (!both)
                    foreach (PuzzleObjective objective in objectives) objective.ServerSolve();
            }
            Refresh();
        }

        void OnKnock(KnockWall from, int quadrant)
        {
            if (from != wall) return;
            int side = quadrant == wall.QuadrantOf(0) ? 0 : 1;
            if (objectives[side].Solved.Value) return;
            knocks[side].Add(Time.unscaledTime);
        }

        void Update()
        {
            if (!IsServer) return;
            for (int side = 0; side < 2; side++)
            {
                List<float> list = knocks[side];
                if (list.Count == 0 || Time.unscaledTime - list[list.Count - 1] < EndGap) continue;
                int received = 0, group = 1;
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i] - list[i - 1] > GroupGap)
                    {
                        received = received * 10 + Mathf.Min(group, 9);
                        group = 1;
                    }
                    else group++;
                }
                received = received * 10 + Mathf.Min(group, 9);
                list.Clear();
                if (side == 0) ReceivedA.Value = received;
                else ReceivedB.Value = received;
                if (received == Pattern(side)) objectives[side].ServerSolve();
            }
        }

        static string Spell(int pattern)
        {
            if (pattern < 0) return "—";
            string digits = pattern.ToString();
            return string.Join(" · ", digits.ToCharArray());
        }

        /// <summary>Pupitre du module A (indice 0) : rythme que B doit frapper (Pattern(1)) et ce que A a reçu de B.</summary>
        void Refresh()
        {
            for (int panel = 0; panel < 2 && panel < panelTexts.Length; panel++)
            {
                if (panelTexts[panel] == null) continue;
                int knocker = 1 - panel;
                if (objectives[knocker].Solved.Value)
                {
                    panelTexts[panel].text = Loc.T("<b>MESSAGE REÇU</b>\nLe code de détresse est bien passé.", "<b>MESSAGE RECEIVED</b>\nThe distress code got through.");
                    continue;
                }
                int received = knocker == 0 ? ReceivedA.Value : ReceivedB.Value;
                panelTexts[panel].text = Loc.T("<b>ÉCOUTE DE LA CLOISON</b>\nTon équipier doit frapper :  <b>", "<b>BULKHEAD LISTENING</b>\nYour teammate must knock:  <b>")
                    + Spell(Pattern(knocker)) + "</b>\n" + Loc.T("Reçu : ", "Received: ") + Spell(received)
                    + Loc.T("\n(chiffre = nombre de coups, · = courte pause)", "\n(digit = number of knocks, · = short pause)");
            }
        }
    }
}
