using UnityEngine;

namespace Abysses.Interaction
{
    /// <summary>
    /// Inscription à l'encre invisible : les renderers ne s'affichent que dans le faisceau d'une <see cref="UVLamp"/>.
    /// Vérification à 10 Hz ; rien n'est rendu tant que la lampe n'éclaire pas.
    /// </summary>
    public class UVReveal : MonoBehaviour
    {
        [SerializeField] Renderer[] hiddenRenderers;
        [SerializeField, Min(0.02f)] float checkInterval = 0.1f;

        bool revealed = true;
        float nextCheck;

        void OnEnable() => Apply(false);

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkInterval;

            bool lit = false;
            Vector3 point = transform.position;
            for (int i = 0; i < UVLamp.Active.Count && !lit; i++)
                lit = UVLamp.Active[i].Illuminates(point);
            if (lit != revealed) Apply(lit);
        }

        void Apply(bool value)
        {
            revealed = value;
            for (int i = 0; i < hiddenRenderers.Length; i++)
                if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = value;
        }
    }
}
