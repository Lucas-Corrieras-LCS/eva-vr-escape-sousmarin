using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abysses.Level
{
    /// <summary>
    /// Étage 1 « Rétablir le courant » (consigne C6) : l'étage démarre en éclairage de secours.
    /// Quand l'objectif de remise sous tension est résolu, les néons se rallument un par un en grésillant,
    /// puis l'éclairage complet revient en clignotant.
    /// Deux jeux de lightmaps bakés (secours = celui de la scène, courant = <see cref="poweredLightmaps"/>) :
    /// on échange les textures, aucune lumière temps réel ajoutée sur le Quest.
    /// </summary>
    public class PowerRestore : MonoBehaviour
    {
        [SerializeField] PuzzleObjective trigger;
        [Tooltip("Néons du plafond, éteints au départ.")]
        [SerializeField] Renderer[] neons;
        [SerializeField] Material neonOff;
        [SerializeField] Material neonOn;
        [Tooltip("Lampes de secours (visuels émissifs) éteintes au retour du courant.")]
        [SerializeField] Renderer[] emergencyLamps;
        [SerializeField] Material emergencyOff;
        [Tooltip("Lightmaps (couleur) bakées courant rétabli, dans l'ordre des lightmaps de la scène.")]
        [SerializeField] Texture2D[] poweredLightmaps;
        [SerializeField] AudioSource powerUpSound;
        [Tooltip("Grésillement joué à chaque néon qui s'allume.")]
        [SerializeField] AudioSource neonSound;
        [SerializeField, Min(0.05f)] float neonInterval = 0.35f;

        int lightmapOffset = -1;
        Texture2D[] emergencyLightmaps;
        bool restored;

        void OnEnable()
        {
            if (trigger != null) trigger.StateChanged += OnTriggerChanged;
        }

        void OnDisable()
        {
            if (trigger != null) trigger.StateChanged -= OnTriggerChanged;
        }

        void Start()
        {
            FindLightmapSlots();
            // Arrivée d'un joueur après la remise sous tension (reconnexion) : directement allumé.
            if (trigger != null && trigger.IsSpawned && trigger.Solved.Value) Apply(true, true);
        }

        void OnTriggerChanged(bool solved)
        {
            if (!solved || restored) return;
            restored = true;
            StartCoroutine(Restore());
        }

        /// <summary>
        /// En chargement additif, les lightmaps de l'étage sont ajoutées à la suite de celles déjà chargées :
        /// on retrouve leur position à partir des renderers de la scène.
        /// </summary>
        void FindLightmapSlots()
        {
            if (poweredLightmaps == null || poweredLightmaps.Length == 0) return;
            Scene scene = gameObject.scene;
            int min = int.MaxValue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    int index = r.lightmapIndex;
                    if (index >= 0 && index < 0xFFFE && index < min) min = index;
                }
            }
            LightmapData[] maps = LightmapSettings.lightmaps;
            if (min == int.MaxValue || min + poweredLightmaps.Length > maps.Length) return;
            lightmapOffset = min;
            emergencyLightmaps = new Texture2D[poweredLightmaps.Length];
            for (int i = 0; i < poweredLightmaps.Length; i++) emergencyLightmaps[i] = maps[min + i].lightmapColor;
        }

        IEnumerator Restore()
        {
            if (powerUpSound != null) powerUpSound.Play();
            yield return new WaitForSeconds(0.6f);

            // Les néons s'allument un par un, avec un raté.
            for (int i = 0; i < neons.Length; i++)
            {
                if (neons[i] == null) continue;
                neons[i].sharedMaterial = neonOn;
                if (neonSound != null)
                {
                    neonSound.transform.position = neons[i].transform.position;
                    neonSound.Play();
                }
                yield return new WaitForSeconds(neonInterval * 0.25f);
                neons[i].sharedMaterial = neonOff;
                yield return new WaitForSeconds(neonInterval * 0.2f);
                neons[i].sharedMaterial = neonOn;
                yield return new WaitForSeconds(neonInterval * 0.55f);
            }

            // Toute la pièce clignote entre secours et courant, puis se stabilise.
            float[] flicker = { 0.08f, 0.12f, 0.06f, 0.2f, 0.1f };
            for (int i = 0; i < flicker.Length; i++)
            {
                SwapLightmaps(i % 2 == 0);
                yield return new WaitForSeconds(flicker[i]);
            }
            Apply(true, false);
        }

        void Apply(bool powered, bool instant)
        {
            restored = restored || powered;
            for (int i = 0; i < neons.Length; i++)
                if (neons[i] != null) neons[i].sharedMaterial = powered ? neonOn : neonOff;
            for (int i = 0; i < emergencyLamps.Length; i++)
                if (emergencyLamps[i] != null && powered) emergencyLamps[i].sharedMaterial = emergencyOff;
            SwapLightmaps(powered);
        }

        void SwapLightmaps(bool powered)
        {
            if (lightmapOffset < 0) return;
            LightmapData[] maps = LightmapSettings.lightmaps;
            if (lightmapOffset + poweredLightmaps.Length > maps.Length) return;
            for (int i = 0; i < poweredLightmaps.Length; i++)
                maps[lightmapOffset + i].lightmapColor = powered ? poweredLightmaps[i] : emergencyLightmaps[i];
            LightmapSettings.lightmaps = maps;
        }
    }
}
