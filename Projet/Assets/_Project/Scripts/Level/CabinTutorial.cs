using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Abysses.Core;
using Abysses.Player;

namespace Abysses.Level
{
    /// <summary>
    /// Entraînement dans la cabine, avant la plongée : appuyer sur un bouton du doigt, attraper un fusible,
    /// le brancher. Chaque étape est indiquée sur le panneau et la cible clignote. Purement local (aucun réseau) :
    /// chacun apprend les gestes à son rythme pendant que les autres s'équipent.
    /// </summary>
    public class CabinTutorial : MonoBehaviour
    {
        [SerializeField] TMP_Text instructions;
        [SerializeField] XRSimpleInteractable button;
        [SerializeField] XRGrabInteractable fuse;
        [SerializeField] XRSocketInteractor socket;
        [Tooltip("Cible de l'étape courante (bouton, fusible, prise) : halo qui pulse.")]
        [SerializeField] Renderer[] stepMarkers;
        [SerializeField] AudioSource successSound;

        static readonly string[] French =
        {
            "ENTRAÎNEMENT 1/3\nAppuie sur le bouton rouge\navec ton index.",
            "ENTRAÎNEMENT 2/3\nAttrape le fusible d'entraînement\n(gâchette sous le majeur).",
            "ENTRAÎNEMENT 3/3\nBranche-le dans la prise.",
            "PARFAIT !\nTu sais tout faire.\nPlace-toi sur le repère, face à la porte.",
        };

        static readonly string[] English =
        {
            "TRAINING 1/3\nPress the red button\nwith your index finger.",
            "TRAINING 2/3\nGrab the training fuse\n(grip trigger under your middle finger).",
            "TRAINING 3/3\nPlug it into the socket.",
            "PERFECT!\nYou know all the moves.\nStand on the marker, facing the door.",
        };

        int step;
        Rigidbody fuseBody;
        Vector3 fuseHome;
        Quaternion fuseHomeRotation;
        float nextFuseCheck;

        void Awake()
        {
            if (fuse != null)
            {
                fuseBody = fuse.GetComponent<Rigidbody>();
                fuseHome = fuse.transform.position;
                fuseHomeRotation = fuse.transform.rotation;
            }
        }

        void OnEnable()
        {
            if (button != null) button.selectEntered.AddListener(OnButton);
            if (fuse != null) fuse.selectEntered.AddListener(OnFuseGrabbed);
            if (socket != null) socket.selectEntered.AddListener(OnSocketed);
            Loc.Changed += Show;
            Show();
        }

        void OnDisable()
        {
            if (button != null) button.selectEntered.RemoveListener(OnButton);
            if (fuse != null) fuse.selectEntered.RemoveListener(OnFuseGrabbed);
            if (socket != null) socket.selectEntered.RemoveListener(OnSocketed);
            Loc.Changed -= Show;
        }

        void OnButton(SelectEnterEventArgs args) => Advance(0);
        void OnFuseGrabbed(SelectEnterEventArgs args) => Advance(1);
        void OnSocketed(SelectEnterEventArgs args) => Advance(2);

        void Advance(int from)
        {
            if (step != from) return;
            step++;
            if (successSound != null) successSound.Play();
            LocalRig.Pulse(0.3f, 0.08f);
            Show();
        }

        void Show()
        {
            if (instructions != null) instructions.text = Loc.English ? English[step] : French[step];
            for (int i = 0; i < stepMarkers.Length; i++)
                if (stepMarkers[i] != null) stepMarkers[i].enabled = i == step;
        }

        void Update()
        {
            // Le halo de l'étape courante pulse doucement.
            if (step < stepMarkers.Length && stepMarkers[step] != null)
            {
                float k = 0.85f + 0.15f * Mathf.Sin(Time.time * 5f);
                stepMarkers[step].transform.localScale = Vector3.one * k;
            }

            // Fusible lâché par terre ou sorti de la cabine : il revient sur son support.
            if (fuse == null || fuse.isSelected || Time.time < nextFuseCheck) return;
            nextFuseCheck = Time.time + 1f;
            if (socket != null && socket.hasSelection) return;
            Vector3 p = fuse.transform.position;
            if (p.y < fuseHome.y - 0.6f || (p - fuseHome).sqrMagnitude > 2.25f)
            {
                if (fuseBody != null)
                {
                    fuseBody.linearVelocity = Vector3.zero;
                    fuseBody.angularVelocity = Vector3.zero;
                    fuseBody.position = fuseHome;
                    fuseBody.rotation = fuseHomeRotation;
                }
                fuse.transform.SetPositionAndRotation(fuseHome, fuseHomeRotation);
            }
        }
    }
}
