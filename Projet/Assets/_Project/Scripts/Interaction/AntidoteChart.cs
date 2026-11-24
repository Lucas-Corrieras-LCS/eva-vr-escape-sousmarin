using TMPro;
using UnityEngine;
using Abysses.Core;

namespace Abysses.Interaction
{
    /// <summary>Tableau des réactifs à l'encre UV (labo ORANGE) : correspondance molécules → couleurs de flacon, commune à tous.</summary>
    public class AntidoteChart : MonoBehaviour
    {
        [Tooltip("Mélangeur de référence (porte la graine de la correspondance).")]
        [SerializeField] AntidoteMixer reference;
        [SerializeField] TMP_Text text;

        void OnEnable()
        {
            reference.Changed += Refresh;
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            reference.Changed -= Refresh;
            Loc.Changed -= Refresh;
        }

        void Refresh()
        {
            if (text != null) text.text = AntidoteMixer.LegendText(reference.LegendSeed.Value);
        }
    }
}
