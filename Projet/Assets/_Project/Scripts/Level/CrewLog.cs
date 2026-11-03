using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Core;

namespace Abysses.Level
{
    /// <summary>
    /// Journal de bord de l'équipage disparu : on appuie sur « LIRE », le message défile lettre par lettre
    /// avec un grésillement radio. Raconte l'histoire de la station sans bloquer le jeu (lecture facultative, locale).
    /// </summary>
    public class CrewLog : MonoBehaviour
    {
        [SerializeField] XRSimpleInteractable playButton;
        [SerializeField] TMP_Text screen;
        [SerializeField] AudioSource radio;
        [SerializeField, TextArea] string french;
        [SerializeField, TextArea] string english;
        [SerializeField, Min(5f)] float charactersPerSecond = 28f;

        Coroutine typing;

        void OnEnable()
        {
            if (playButton != null) playButton.selectEntered.AddListener(OnPlay);
            Loc.Changed += ShowIdle;
            ShowIdle();
        }

        void OnDisable()
        {
            if (playButton != null) playButton.selectEntered.RemoveListener(OnPlay);
            Loc.Changed -= ShowIdle;
        }

        void ShowIdle()
        {
            if (typing != null) return;
            if (screen == null) return;
            screen.maxVisibleCharacters = 99999;
            screen.text = Loc.T("JOURNAL DE BORD\n\nAppuie sur LIRE", "CREW LOG\n\nPress READ");
        }

        void OnPlay(SelectEnterEventArgs args)
        {
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(Type(Loc.T(french, english)));
        }

        IEnumerator Type(string text)
        {
            if (radio != null) radio.Play();
            screen.text = text;
            screen.maxVisibleCharacters = 0;
            float shown = 0f;
            while (shown < text.Length)
            {
                shown += charactersPerSecond * Time.deltaTime;
                screen.maxVisibleCharacters = Mathf.Min(text.Length, (int)shown);
                yield return null;
            }
            if (radio != null) radio.Stop();
            typing = null;
        }
    }
}
