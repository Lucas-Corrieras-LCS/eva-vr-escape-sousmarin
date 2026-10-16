using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Abysses.Core;

namespace Abysses.Player
{
    /// <summary>
    /// Contrôle des déplacements artificiels (consigne C3).
    /// - Joystick (Move / Gravity) : actif seulement en debug, coupé en build EVA.
    /// - Téléportation, rotation au stick, escalade, saut, grab move : toujours coupés,
    ///   ils casseraient la correspondance entre la piste réelle et le monde virtuel.
    /// </summary>
    public class LocomotionGate : MonoBehaviour
    {
        [Tooltip("Déplacements autorisés uniquement en debug.")]
        [SerializeField] GameObject[] debugOnly;
        [Tooltip("Déplacements toujours interdits.")]
        [SerializeField] GameObject[] alwaysDisabled;
        [Tooltip("Gestionnaires d'entrées des manettes : le mode « déplacement continu » désactive les actions de téléportation.")]
        [SerializeField] ControllerInputActionManager[] controllers;

        void Awake()
        {
            bool allow = GameConfig.Instance.AllowJoystickLocomotion;
            for (int i = 0; i < debugOnly.Length; i++)
                if (debugOnly[i] != null) debugOnly[i].SetActive(allow);
            for (int i = 0; i < alwaysDisabled.Length; i++)
                if (alwaysDisabled[i] != null) alwaysDisabled[i].SetActive(false);
        }

        void Start()
        {
            // Après l'initialisation des manettes : plus aucune action de téléportation ni de rotation au stick.
            for (int i = 0; i < controllers.Length; i++)
                if (controllers[i] != null) controllers[i].smoothMotionEnabled = true;
        }
    }
}
