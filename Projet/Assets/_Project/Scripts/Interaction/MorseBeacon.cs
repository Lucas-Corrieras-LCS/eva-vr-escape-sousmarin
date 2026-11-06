using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 2 — balise de détresse visible par le hublot : elle clignote un code à 3 chiffres en morse, en boucle.
    /// Un haut-parleur près du cadenas bipe en même temps (lisible les yeux fermés, ou sans voir la balise).
    /// Le code est tiré par le serveur ; chaque client rejoue le clignotement localement.
    /// </summary>
    public class MorseBeacon : NetworkBehaviour
    {
        public const int Digits = 3;

        static readonly string[] Morse = { "-----", ".----", "..---", "...--", "....-", ".....", "-....", "--...", "---..", "----." };

        [SerializeField] Renderer lamp;
        [SerializeField] Material lampOff;
        [SerializeField] Material lampOn;
        [Tooltip("Bip synchronisé (boucle jouée pendant chaque signal).")]
        [SerializeField] AudioSource beep;
        [Tooltip("Durée d'un point (s). Trait = 3 points.")]
        [SerializeField, Range(0.15f, 0.6f)] float unit = 0.3f;

        /// <summary>Code de 000 à 999.</summary>
        public readonly NetworkVariable<int> Code = new NetworkVariable<int>(-1);

        readonly List<(bool on, float duration)> timeline = new List<(bool, float)>(64);
        int step;
        float stepEnd;
        bool lit;

        public static int DigitAt(int code, int index)
        {
            // index 0 = premier chiffre lu (centaines).
            int divisor = index == 0 ? 100 : index == 1 ? 10 : 1;
            return code / divisor % 10;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) Code.Value = Random.Range(0, 1000);
            Code.OnValueChanged += OnCodeChanged;
            Build(Code.Value);
        }

        public override void OnNetworkDespawn() => Code.OnValueChanged -= OnCodeChanged;

        void OnCodeChanged(int previous, int current) => Build(current);

        void Build(int code)
        {
            timeline.Clear();
            step = 0;
            stepEnd = 0f;
            SetLit(false);
            if (code < 0) return;
            for (int d = 0; d < Digits; d++)
            {
                string symbols = Morse[DigitAt(code, d)];
                for (int s = 0; s < symbols.Length; s++)
                {
                    timeline.Add((true, symbols[s] == '.' ? unit : unit * 3f));
                    timeline.Add((false, s < symbols.Length - 1 ? unit : unit * 5f));
                }
            }
            // Longue pause avant la répétition : on repère le début du message.
            timeline[timeline.Count - 1] = (false, unit * 12f);
        }

        void Update()
        {
            if (timeline.Count == 0 || Time.unscaledTime < stepEnd) return;
            (bool on, float duration) current = timeline[step];
            SetLit(current.on);
            stepEnd = Time.unscaledTime + current.duration;
            step = (step + 1) % timeline.Count;
        }

        void SetLit(bool on)
        {
            if (lit == on) return;
            lit = on;
            if (lamp != null) lamp.sharedMaterial = on ? lampOn : lampOff;
            if (beep == null) return;
            if (on) beep.Play();
            else beep.Stop();
        }
    }
}
