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
    /// Étage 3 — console de calibrage du sonar (jeu de mémoire type « Simon »), CROISÉE entre partenaires :
    /// la séquence (lumière + note) s'affiche sur le récepteur placé dans le module du partenaire, les touches sont ici.
    /// Le partenaire dicte (« carré, carré, étoile… »), le joueur reproduit. Module partenaire vide : la séquence
    /// s'affiche aussi sur les touches (repli). 6 notes (4 en mode facile) ; une erreur relance depuis le début.
    /// La séquence est tirée par le serveur ; la lecture est diffusée à tous, la saisie se joue localement,
    /// le serveur vérifie la séquence complète.
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
        [Tooltip("Lampes du récepteur, dans le module du partenaire : c'est là que la séquence s'affiche.")]
        [SerializeField] Renderer[] displayPads;
        [SerializeField] AudioSource displayTone;
        [SerializeField] TMP_Text displayScreen;
        [Tooltip("Module du partenaire (où se trouve le récepteur).")]
        [SerializeField, Range(0, 3)] int partnerQuadrant;
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
        int displayed;
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
                PlayRpc(round, 0.3f);
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
                PlayRpc(round, 1.2f);
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
            PlayRpc(round, 0.8f);
        }

        /// <summary>Lecture de la séquence chez tout le monde : le partenaire la voit sur son récepteur.</summary>
        [Rpc(SendTo.Everyone)]
        void PlayRpc(int length, float delay)
        {
            StopAllCoroutines();
            ResetDisplay();
            StartCoroutine(Play(length, delay));
        }

        bool PartnerAbsent
        {
            get
            {
                GameStateManager state = GameStateManager.Instance;
                return state != null && state.IsSpawned && state.Phase.Value == GamePhase.Playing
                    && (state.ActiveMask.Value & (1 << partnerQuadrant)) == 0;
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitRpc(int attempt, int length)
        {
            if (objective.Solved.Value || length < Length) return;
            int mask = (1 << (length * 2)) - 1;
            if ((Sequence.Value & mask) == attempt) objective.ServerSolve();
        }

        IEnumerator Play(int length, float delay)
        {
            playing = true;
            displayed = length;
            RefreshScreen();
            yield return new WaitForSeconds(delay);
            bool absent = PartnerAbsent;
            for (int i = 0; i < length; i++)
            {
                yield return Show(PadAt(Sequence.Value, i), absent);
                yield return gap;
            }
            playing = false;
            RefreshScreen();
        }

        /// <summary>Note de la séquence : sur le récepteur du partenaire (et sur les touches si le partenaire est absent).</summary>
        IEnumerator Show(int pad, bool alsoOnPads)
        {
            SetPad(displayPads, pad, true);
            if (alsoOnPads) SetPad(pads, pad, true);
            AudioSource source = alsoOnPads || displayTone == null ? tone : displayTone;
            if (source != null)
            {
                source.pitch = Pitches[pad];
                source.Play();
            }
            yield return flash;
            SetPad(displayPads, pad, false);
            if (alsoOnPads) SetPad(pads, pad, false);
        }

        /// <summary>Appui sur une touche : retour local, sur les touches seulement.</summary>
        IEnumerator Flash(int pad)
        {
            SetPad(pads, pad, true);
            if (tone != null)
            {
                tone.pitch = Pitches[pad];
                tone.Play();
            }
            yield return flash;
            SetPad(pads, pad, false);
        }

        void SetPad(Renderer[] set, int pad, bool on)
        {
            if (set == null || pad >= set.Length || set[pad] == null) return;
            set[pad].sharedMaterial = on ? padOn[pad] : padOff[pad];
        }

        void ResetDisplay()
        {
            for (int i = 0; i < Pads; i++)
            {
                SetPad(displayPads, i, false);
                SetPad(pads, i, false);
            }
        }

        void OnObjectiveChanged(bool solved)
        {
            if (!solved) return;
            StopAllCoroutines();
            playing = false;
            for (int i = 0; i < Pads; i++)
            {
                SetPad(pads, i, true);
                SetPad(displayPads, i, true);
            }
            if (successSound != null) successSound.Play();
            RefreshScreen();
        }

        void RefreshScreen()
        {
            if (displayScreen != null)
            {
                bool solved = objective != null && objective.Solved.Value;
                displayScreen.text = solved ? Loc.T("SONAR CALIBRÉ", "SONAR CALIBRATED")
                    : playing ? Loc.T("RÉCEPTION…  ", "RECEIVING…  ") + displayed + Loc.T(" signaux\nDicte-les à ton équipier !", " signals\nRead them to your teammate!")
                    : Loc.T("Récepteur sonar\nLa séquence de ton équipier\ns'affiche ici", "Sonar receiver\nYour teammate's sequence\nshows here");
            }
            if (screen == null) return;
            if (objective != null && objective.Solved.Value) screen.text = Loc.T("SONAR CALIBRÉ", "SONAR CALIBRATED");
            else if (round == 0) screen.text = Loc.T("Appuie sur ÉCOUTER : ton équipier\nreçoit la séquence et te la dicte", "Press LISTEN: your teammate\nreceives the sequence and reads it to you");
            else if (playing) screen.text = Loc.T("Écoute…  ", "Listen…  ") + round + " / " + Length;
            else screen.text = Loc.T("À toi !  ", "Your turn!  ") + round + " / " + Length;
        }
    }
}
