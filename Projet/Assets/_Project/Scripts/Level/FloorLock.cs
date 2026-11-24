using System;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;

namespace Abysses.Level
{
    /// <summary>
    /// Méta-énigme de fin d'étage (remplace les leviers synchronisés) : chaque énigme de l'étage, une fois résolue,
    /// révèle un chiffre (fragment). Les fragments s'affichent dans des modules différents (<see cref="Routing.FragmentHolder"/>) :
    /// l'équipe les rassemble à voix haute et compose le code sur le verrou de la cabine, dans l'ordre des fragments.
    /// Autorité serveur ; le code est tiré à chaque partie.
    /// </summary>
    public class FloorLock : NetworkBehaviour
    {
        [Serializable]
        public class Group
        {
            [Tooltip("Objectifs de l'énigme (un par module, ou un seul partagé) : le fragment apparaît quand tous sont résolus.")]
            public PuzzleObjective[] members;
        }

        [SerializeField] PuzzleObjective objective;
        [SerializeField] Group[] groups;
        [Tooltip("Décalage de la répartition des fragments entre modules (différent à chaque étage).")]
        [SerializeField] int shift;

        /// <summary>Code complet (fragment 1 = premier chiffre).</summary>
        public readonly NetworkVariable<int> Code = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Wrong = new NetworkVariable<int>(0);

        public event Action Changed;

        public int Count => groups.Length;
        public int Shift => shift;
        public PuzzleObjective Objective => objective;
        public bool Unlocked => objective != null && objective.Solved.Value;

        public int Digit(int index)
        {
            if (Code.Value < 0) return -1;
            int divisor = 1;
            for (int i = index + 1; i < groups.Length; i++) divisor *= 10;
            return Code.Value / divisor % 10;
        }

        public bool GroupSolved(int index)
        {
            PuzzleObjective[] members = groups[index].members;
            for (int i = 0; i < members.Length; i++)
                if (members[i] != null && !members[i].Solved.Value) return false;
            return true;
        }

        public bool AllFragments
        {
            get
            {
                for (int i = 0; i < groups.Length; i++) if (!GroupSolved(i)) return false;
                return true;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                int code = 0;
                for (int i = 0; i < groups.Length; i++) code = code * 10 + UnityEngine.Random.Range(0, 10);
                Code.Value = code;
            }
            Code.OnValueChanged += OnIntChanged;
            Wrong.OnValueChanged += OnIntChanged;
            objective.StateChanged += OnSolvedChanged;
            foreach (Group group in groups)
                foreach (PuzzleObjective member in group.members)
                    if (member != null) member.StateChanged += OnSolvedChanged;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            Code.OnValueChanged -= OnIntChanged;
            Wrong.OnValueChanged -= OnIntChanged;
            objective.StateChanged -= OnSolvedChanged;
            foreach (Group group in groups)
                foreach (PuzzleObjective member in group.members)
                    if (member != null) member.StateChanged -= OnSolvedChanged;
        }

        void OnIntChanged(int previous, int current) => Changed?.Invoke();
        void OnSolvedChanged(bool solved) => Changed?.Invoke();

        /// <summary>Proposition du verrou (chiffres saisis dans l'ordre).</summary>
        [Rpc(SendTo.Server)]
        public void SubmitRpc(int value, int length)
        {
            if (Unlocked || Code.Value < 0) return;
            if (AllFragments && length == groups.Length && value == Code.Value) objective.ServerSolve();
            else Wrong.Value++;
        }
    }
}
