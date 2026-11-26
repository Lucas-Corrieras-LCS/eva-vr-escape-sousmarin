using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// « Faux bloom » : halos lumineux autour des lampes, toujours tournés vers la tête du joueur.
    /// Un seul composant par étage oriente tous les halos (quelques dizaines de quads), sans aucun post-traitement :
    /// le vrai bloom coûterait 20 à 30 % du GPU du Quest 2.
    /// </summary>
    public class HaloBillboards : MonoBehaviour
    {
        [SerializeField] Transform[] halos;

        Camera cachedCamera;

        void LateUpdate()
        {
            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
            if (cachedCamera == null) return;
            Vector3 eye = cachedCamera.transform.position;
            for (int i = 0; i < halos.Length; i++)
            {
                Transform halo = halos[i];
                if (halo == null) continue;
                Vector3 toHalo = halo.position - eye;
                if (toHalo.sqrMagnitude > 0.0001f) halo.rotation = Quaternion.LookRotation(toHalo, Vector3.up);
            }
        }
    }
}
