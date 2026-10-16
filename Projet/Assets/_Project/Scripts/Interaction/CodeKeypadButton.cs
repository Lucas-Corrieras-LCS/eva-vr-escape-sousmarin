using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Abysses.Interaction
{
    /// <summary>Touche du digicode (on l'appuie du doigt via le Poke Interactor, ou par sélection).</summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class CodeKeypadButton : MonoBehaviour
    {
        [SerializeField] CodeKeypad keypad;
        [SerializeField, Range(0, CodeKeypad.SymbolCount - 1)] int symbol;
        [Tooltip("Optionnel : affiche le symbole de la touche.")]
        [SerializeField] Renderer symbolRenderer;

        XRSimpleInteractable interactable;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (symbolRenderer != null && keypad != null && symbol < keypad.SymbolMaterials.Length)
                symbolRenderer.sharedMaterial = keypad.SymbolMaterials[symbol];
        }

        void OnEnable() => interactable.selectEntered.AddListener(OnPressed);
        void OnDisable() => interactable.selectEntered.RemoveListener(OnPressed);

        void OnPressed(SelectEnterEventArgs args) => keypad.Press(symbol);
    }
}
