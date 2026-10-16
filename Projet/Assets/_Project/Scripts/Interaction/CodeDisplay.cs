using UnityEngine;

namespace Abysses.Interaction
{
    /// <summary>
    /// Affiche le code d'un digicode, en général peint sur le mur du module VOISIN :
    /// seul un autre joueur peut le lire (à travers le hublot) et le dicter.
    /// </summary>
    public class CodeDisplay : MonoBehaviour
    {
        [SerializeField] CodeKeypad keypad;
        [Tooltip("Une case par symbole du code (4).")]
        [SerializeField] Renderer[] slots;
        [SerializeField] Material hiddenMaterial;

        void OnEnable()
        {
            keypad.CodeChanged += Show;
            Show(keypad.IsSpawned ? keypad.Code.Value : -1);
        }

        void OnDisable() => keypad.CodeChanged -= Show;

        void Show(int code)
        {
            Material[] symbols = keypad.SymbolMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].sharedMaterial = code < 0 ? hiddenMaterial : symbols[CodeKeypad.SymbolAt(code, i)];
            }
        }
    }
}
