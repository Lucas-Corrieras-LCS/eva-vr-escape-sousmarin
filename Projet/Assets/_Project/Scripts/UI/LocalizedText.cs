using TMPro;
using UnityEngine;
using Abysses.Core;

namespace Abysses.UI
{
    /// <summary>Texte fixe traduit (étiquettes du décor, boutons du menu) : se réapplique quand la langue change.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField, TextArea] string french;
        [SerializeField, TextArea] string english;

        TMP_Text label;

        void Awake() => label = GetComponent<TMP_Text>();

        void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        void OnDisable() => Loc.Changed -= Apply;

        void Apply() => label.text = Loc.T(french, english);
    }
}
