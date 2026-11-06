using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 3 — console de calibrage du sonar (jeu de mémoire type « Simon »).
    /// La console joue une séquence de couleurs (lumière + note) qui s'allonge à chaque manche ; il faut la reproduire.
    /// 6 notes à retenir (4 en mode facile). Une erreur relance depuis le début.
    /// La séquence est tirée par le serveur, la partie se joue localement, le serveur vérifie la séquence complète.
    /// Boutons : 0..3 = touches de couleur, 4 = « écouter » (démarre / rejoue la manche).
    /// </summary>
    public class SimonConsole : NetworkBehaviour, IPokeReceiver
    {
        public const int Pads = 4;
        public const int MaxLength = 6;
        public const int ListenIndex = 4;
        const int EasyLength = 4;

        [SerializeField] PuzzleObjective objective;
        [SerializeField] Renderer[] pads;
        [SerializeField] Material[] padOff;
        [SerializeField] Material[] padOn;
        [SerializeField] TMP_Text screen;
        [Tooltip("Une note par couleur (même son, hauteur différente).")]
        [SerializeField] AudioSource tone;
        [SerializeField] AudioSource errorSound;
        [SerializeField] AudioSource successSound;

        static readonly float[] Pitches = { 0.8f, 1f, 1.2f, 1.5f };

        public readonly NetworkVariable<int> Sequence = new NetworkVariable<int>(-1);

        int round;
        int input;
        bool playing;
        WaitForSeconds flash;
        WaitForSeconds gap;

        int Length
        {
            get
            {
                GameStateManager state = GameStateManager.Instance;
                return state != null && state.IsSpawned && state.EasyMode.Value ? EasyLength : MaxLength;
            }
        }

        public static int PadAt(int sequence, int index) => (sequence >> (index * 2)) & 3;

        void Awake()
        {
            flash = new WaitForSeconds(0.45f);
            gap = new WaitForSeconds(0.25f);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                int sequence = 0;
                for (int i = 0; i < MaxLength; i++) sequence |= Random.Range(0, Pads) << (i * 2);
                Sequence.Value = sequence;
            }
            objective.StateChanged += OnObjectiveChanged;
            Loc.Changed += RefreshScreen;
            round = 0;
            RefreshScreen();
        }

        public override void OnNetworkDespawn()
        {
            objective.StateChanged -= OnObjectiveChanged;
            Loc.Changed -= RefreshScreen;
        }

        public void Pressed(int index)
        {
            if (!IsSpawned || objective.Solved.Value || Sequence.Value < 0 || playing) return;
            if (index == ListenIndex)
            {
                if (round == 0) round = 1;
                input = 0;
                StartCoroutine(Play());
                return;
            }
            if (round == 0 || index < 0 || index >= Pads) return;

            StartCoroutine(Flash(index));
            if (PadAt(Sequence.Value, input) != index)
            {
                if (errorSound != null) errorSound.Play();
                if (LocalRig.IsLocalOrShared(objective.Quadrant)) LocalRig.Pulse(0.6f, 0.25f);
                round = 1;
                input = 0;
                StartCoroutine(Play(1.2f));
                return;
            }
            input++;
            if (input < round) return;
            if (round >= Length)
            {
                SubmitRpc(Sequence.Value & ((1 << (round * 2)) - 1), round);
                return;
            }
            round++;
            input = 0;
            StartCoroutine(Play(0.8f));
        }

        [Rpc(SendTo.Server)]
        void SubmitRpc(int attempt, int length)
        {
            if (objective.Solved.Value || length < Length) return;
            int mask = (1 << (length * 2)) - 1;
            if ((Sequence.Value & mask) == attempt) objective.ServerSolve();
        }

        IEnumerator Play(float delay = 0.3f)
        {
            playing = true;
            RefreshScreen();
            yield return new WaitForSeconds(delay);
            for (int i = 0; i < round; i++)
            {
                yield return Flash(PadAt(Sequence.Value, i));
                yield return gap;
            }
            playing = false;
            RefreshScreen();
        }

        IEnumerator Flash(int pad)
        {
            if (pad < pads.Length && pads[pad] != null) pads[pad].sharedMaterial = padOn[pad];
            if (tone != null)
            {
                tone.pitch = Pitches[pad];
                tone.Play();
            }
            yield return flash;
            if (pad < pads.Length && pads[pad] != null) pads[pad].sharedMaterial = padOff[pad];
        }

        void OnObjectiveChanged(bool solved)
        {
            if (!solved) return;
            StopAllCoroutines();
            playing = false;
            for (int i = 0; i < pads.Length; i++)
                if (pads[i] != null) pads[i].sharedMaterial = padOn[i];
            if (successSound != null) successSound.Play();
            RefreshScreen();
        }

        void RefreshScreen()
        {
            if (screen == null) return;
            if (objective != null && objective.Solved.Value) screen.text = Loc.T("SONAR CALIBRÉ", "SONAR CALIBRATED");
            else if (round == 0) screen.text = Loc.T("Appuie sur ÉCOUTER\npuis répète la séquence", "Press LISTEN\nthen repeat the sequence");
            else if (playing) screen.text = Loc.T("Écoute…  ", "Listen…  ") + round + " / " + Length;
            else screen.text = Loc.T("À toi !  ", "Your turn!  ") + round + " / " + Length;
        }
    }
}
