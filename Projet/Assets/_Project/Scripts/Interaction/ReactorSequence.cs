using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Énigme finale « Séquence du réacteur » : le cœur s'illumine d'une suite de couleurs de joueurs.
    /// Chacun doit appuyer sur SON bouton quand vient sa couleur, dans l'ordre. Une erreur rejoue la séquence.
    /// Les joueurs se voient enfin tous (salle commune) : coordination à vue et à la voix.
    /// </summary>
    public class ReactorSequence : NetworkBehaviour
    {
        [Serializable]
        public struct Console
        {
            [Range(0, 3)] public int quadrant;
            public XRSimpleInteractable button;
        }

        public const int Idle = -1;
        public const int Success = 4;
        public const int Error = 5;

        [SerializeField] PuzzleObjective objective;
        [SerializeField] Console[] consoles;
        [SerializeField] Renderer core;
        [SerializeField] Material idleMaterial;
        [Tooltip("Une couleur par quadrant (matériaux des avatars).")]
        [SerializeField] Material[] quadrantMaterials;
        [SerializeField] Material successMaterial;
        [SerializeField] Material errorMaterial;
        [SerializeField, Range(3, 10)] int length = 6;
        [SerializeField, Min(0.2f)] float showTime = 0.8f;
        [SerializeField, Min(0.1f)] float gapTime = 0.35f;
        [SerializeField, Min(0f)] float startDelay = 5f;
        [SerializeField, Min(0f)] float retryDelay = 2.5f;
        [SerializeField] AudioSource tone;
        [SerializeField] AudioSource failSound;

        /// <summary>Ce qu'affiche le cœur : -1 repos, 0..3 couleur d'un joueur, 4 succès, 5 erreur.</summary>
        public readonly NetworkVariable<int> Display = new NetworkVariable<int>(Idle);

        readonly List<int> sequence = new List<int>(10);
        int progress;
        bool accepting;
        Coroutine playing;
        UnityAction<SelectEnterEventArgs>[] handlers;
        WaitForSeconds showWait;
        WaitForSeconds gapWait;

        void Awake()
        {
            showWait = new WaitForSeconds(showTime);
            gapWait = new WaitForSeconds(gapTime);
        }

        public override void OnNetworkSpawn()
        {
            handlers = new UnityAction<SelectEnterEventArgs>[consoles.Length];
            for (int i = 0; i < consoles.Length; i++)
            {
                int quadrant = consoles[i].quadrant;
                handlers[i] = _ => OnLocalPress(quadrant);
                consoles[i].button.selectEntered.AddListener(handlers[i]);
            }
            Display.OnValueChanged += OnDisplayChanged;
            ApplyDisplay(Display.Value);

            if (IsServer)
            {
                GameStateManager.PhaseChanged += OnPhaseChanged;
                if (GameStateManager.Instance != null && GameStateManager.Instance.Phase.Value == GamePhase.Playing) Begin();
            }
        }

        public override void OnNetworkDespawn()
        {
            for (int i = 0; i < consoles.Length; i++)
                if (consoles[i].button != null) consoles[i].button.selectEntered.RemoveListener(handlers[i]);
            Display.OnValueChanged -= OnDisplayChanged;
            if (IsServer) GameStateManager.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Playing && sequence.Count == 0) Begin();
        }

        void Begin()
        {
            int active = GameStateManager.Instance.ActiveMask.Value;
            var players = new List<int>(PlayArea.QuadrantCount);
            for (int q = 0; q < PlayArea.QuadrantCount; q++)
                if ((active & (1 << q)) != 0) players.Add(q);
            if (players.Count == 0) return;

            // Chaque joueur apparaît au moins une fois, le reste au hasard.
            sequence.Clear();
            for (int i = players.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (players[i], players[j]) = (players[j], players[i]);
            }
            sequence.AddRange(players);
            while (sequence.Count < length) sequence.Add(players[UnityEngine.Random.Range(0, players.Count)]);

            Replay(startDelay);
        }

        void Replay(float delay)
        {
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(PlaySequence(delay));
        }

        IEnumerator PlaySequence(float delay)
        {
            accepting = false;
            progress = 0;
            yield return new WaitForSeconds(delay);
            Display.Value = Idle;
            for (int i = 0; i < sequence.Count; i++)
            {
                Display.Value = sequence[i];
                yield return showWait;
                Display.Value = Idle;
                yield return gapWait;
            }
            accepting = true;
            playing = null;
        }

        void OnLocalPress(int quadrant)
        {
            if (LocalRig.Instance == null || LocalRig.Instance.Quadrant != quadrant) return;
            PressRpc(quadrant);
        }

        [Rpc(SendTo.Server)]
        void PressRpc(int quadrant)
        {
            if (objective.Solved.Value || !accepting) return;

            if (sequence[progress] == quadrant)
            {
                progress++;
                FlashRpc(quadrant);
                if (progress < sequence.Count) return;
                accepting = false;
                Display.Value = Success;
                objective.ServerSolve();
            }
            else
            {
                accepting = false;
                Display.Value = Error;
                FailRpc();
                Replay(retryDelay);
            }
        }

        [Rpc(SendTo.Everyone)]
        void FlashRpc(int quadrant)
        {
            PlayTone(quadrant);
            StopCoroutine(nameof(Flash));
            StartCoroutine(nameof(Flash), quadrant);
        }

        IEnumerator Flash(int quadrant)
        {
            ApplyDisplay(quadrant);
            yield return gapWait;
            ApplyDisplay(Display.Value);
        }

        [Rpc(SendTo.Everyone)]
        void FailRpc()
        {
            if (failSound != null) failSound.Play();
        }

        void OnDisplayChanged(int previous, int current)
        {
            ApplyDisplay(current);
            if (PlayArea.IsValid(current)) PlayTone(current);
        }

        void PlayTone(int quadrant)
        {
            if (tone == null) return;
            tone.pitch = 0.8f + 0.15f * quadrant;
            tone.Play();
        }

        void ApplyDisplay(int value)
        {
            Material material = value switch
            {
                Success => successMaterial,
                Error => errorMaterial,
                _ when PlayArea.IsValid(value) => quadrantMaterials[value],
                _ => idleMaterial,
            };
            if (core != null && material != null) core.sharedMaterial = material;
        }
    }
}
