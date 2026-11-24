using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Panneau d'un module pour le <see cref="CallOutRelay"/> : écran, grand symbole (visible seulement par l'appelant),
    /// jauge de temps et 6 boutons-symboles. Boutons 0..5 = symboles, 6 = démarrer.
    /// Piloté par événements ; seule la jauge de temps est animée pendant un appel.
    /// </summary>
    public class CallOutPanel : MonoBehaviour, IPokeReceiver
    {
        public const int StartIndex = CallOutRelay.Symbols;

        [SerializeField] CallOutRelay relay;
        [SerializeField, Range(0, 3)] int quadrant;
        [SerializeField] TMP_Text screen;
        [SerializeField] TMP_Text title;
        [Tooltip("Grand symbole à décrire, affiché chez l'appelant.")]
        [SerializeField] Renderer bigSymbol;
        [SerializeField] Material[] symbolMaterials;
        [Tooltip("Jauge de temps (mise à l'échelle sur X local).")]
        [SerializeField] Transform timerBar;
        [SerializeField] Renderer timerRenderer;
        [SerializeField] Material timerOk;
        [SerializeField] Material timerLate;
        [SerializeField] Renderer statusLamp;
        [SerializeField] Material statusIdle;
        [SerializeField] Material statusActive;
        [SerializeField] Material statusError;
        [SerializeField] Material statusOk;
        [SerializeField] GameObject startButton;
        [SerializeField] AudioSource callSound;
        [SerializeField] AudioSource successSound;
        [SerializeField] AudioSource errorSound;
        [SerializeField] AudioSource roundSound;
        [Tooltip("Titre du panneau selon l'étage (surtension, fuites…).")]
        [SerializeField] string titleFrench;
        [SerializeField] string titleEnglish;

        int seenResult;
        int seenCall = -1;
        Vector3 barScale;

        bool IsTarget => relay.HasCall && relay.Target == quadrant;
        bool IsCaller => relay.HasCall && relay.Caller == quadrant;

        void Awake()
        {
            if (timerBar != null) barScale = timerBar.localScale;
        }

        void OnEnable()
        {
            relay.Changed += Refresh;
            Loc.Changed += Refresh;
            seenResult = relay.LastResult.Value;
            Refresh();
        }

        void OnDisable()
        {
            relay.Changed -= Refresh;
            Loc.Changed -= Refresh;
        }

        public void Pressed(int index)
        {
            if (!relay.IsSpawned || relay.Finished) return;
            if (index == StartIndex)
            {
                if (!relay.Started) relay.StartRpc();
                return;
            }
            if (IsTarget) relay.SubmitRpc(quadrant, index);
        }

        void Update()
        {
            if (timerBar == null || !IsTarget || relay.NetworkManager == null) return;
            float left = (float)(relay.Deadline.Value - relay.NetworkManager.ServerTime.Time);
            float k = Mathf.Clamp01(left / Mathf.Max(0.1f, relay.Duration.Value));
            Vector3 scale = barScale;
            scale.x *= k;
            timerBar.localScale = scale;
            if (timerRenderer != null) timerRenderer.sharedMaterial = k < 0.35f ? timerLate : timerOk;
        }

        void Refresh()
        {
            if (relay == null) return;
            PlayFeedback();

            bool caller = IsCaller;
            bool target = IsTarget;
            if (title != null) title.text = Loc.T(titleFrench, titleEnglish);
            if (startButton != null) startButton.SetActive(!relay.Started && !relay.Finished);

            if (bigSymbol != null)
            {
                bigSymbol.gameObject.SetActive(caller);
                if (caller && symbolMaterials != null && relay.Symbol < symbolMaterials.Length)
                    bigSymbol.sharedMaterial = symbolMaterials[relay.Symbol];
            }
            if (timerBar != null)
            {
                timerBar.gameObject.SetActive(target);
                timerBar.localScale = barScale;
            }

            Material lamp = relay.Finished ? statusOk : caller || target ? statusActive : statusIdle;
            if (relay.LastKind == CallOutRelay.Result.Wrong || relay.LastKind == CallOutRelay.Result.Timeout)
                if (!relay.HasCall && !relay.Finished) lamp = statusError;
            if (statusLamp != null && lamp != null) statusLamp.sharedMaterial = lamp;

            if (screen != null) screen.text = ScreenText(caller, target);

            // Nouvel appel qui me concerne : bip d'appel et petite vibration.
            int call = relay.Call.Value;
            if (call != seenCall)
            {
                seenCall = call;
                if ((caller || target) && LocalRig.IsLocalOrShared(quadrant))
                {
                    if (callSound != null) callSound.Play();
                    LocalRig.Pulse(0.25f, 0.08f);
                }
            }
        }

        string ScreenText(bool caller, bool target)
        {
            if (relay.Finished) return Loc.T("SYSTÈME STABILISÉ\nBravo l'équipe !", "SYSTEM STABILISED\nWell done, team!");
            if (!relay.Started)
                return Loc.T("Toute l'équipe à son panneau ?\nAppuyez sur DÉMARRER.", "Whole team at their panel?\nPress START.");

            string progress = Loc.T("Manche ", "Round ") + Mathf.Min(relay.Round.Value, relay.Rounds) + "/" + relay.Rounds
                + "   ·   " + relay.Streak.Value + "/" + relay.CallsNeeded(relay.Round.Value);
            if (!relay.HasCall)
            {
                switch (relay.LastKind)
                {
                    case CallOutRelay.Result.Wrong: return Loc.T("MAUVAIS SYMBOLE !\nLa manche recommence…", "WRONG SYMBOL!\nThe round restarts…") + "\n" + progress;
                    case CallOutRelay.Result.Timeout: return Loc.T("TROP TARD !\nLa manche recommence…", "TOO LATE!\nThe round restarts…") + "\n" + progress;
                    case CallOutRelay.Result.RoundDone: return Loc.T("MANCHE RÉUSSIE !\nPréparez-vous, ça accélère…", "ROUND CLEARED!\nGet ready, it speeds up…") + "\n" + progress;
                    default: return Loc.T("Bien joué !\nAppel suivant…", "Nice!\nNext call…") + "\n" + progress;
                }
            }
            if (caller && target) return Loc.T("Appuie sur ce symbole !", "Press this symbol!") + "\n" + progress;
            if (caller)
                return Loc.T("DÉCRIS ce symbole au plongeur ", "DESCRIBE this symbol to the ")
                    + CallOutRelay.ColorName(relay.Target) + Loc.T(" !", " diver!") + "\n" + progress;
            if (target)
                return Loc.T("ÉCOUTE le plongeur ", "LISTEN to the ") + CallOutRelay.ColorName(relay.Caller)
                    + Loc.T(" et appuie sur le bon symbole !", " diver and press the right symbol!") + "\n" + progress;
            return Loc.T("Appel en cours : ", "Call in progress: ") + CallOutRelay.ColorName(relay.Caller) + Loc.T(" guide ", " guides ")
                + CallOutRelay.ColorName(relay.Target) + "\n" + progress;
        }

        void PlayFeedback()
        {
            int result = relay.LastResult.Value;
            if (result == seenResult) return;
            seenResult = result;
            if (!LocalRig.IsLocalOrShared(quadrant)) return;
            switch (relay.LastKind)
            {
                case CallOutRelay.Result.Success:
                    if (successSound != null) successSound.Play();
                    break;
                case CallOutRelay.Result.RoundDone:
                    if (roundSound != null) roundSound.Play();
                    LocalRig.Pulse(0.4f, 0.2f);
                    break;
                case CallOutRelay.Result.Wrong:
                case CallOutRelay.Result.Timeout:
                    if (errorSound != null) errorSound.Play();
                    LocalRig.Pulse(0.7f, 0.3f);
                    break;
            }
        }
    }
}
