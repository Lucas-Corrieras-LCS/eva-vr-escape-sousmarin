using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using Abysses.Core;

namespace Abysses.Player
{
    /// <summary>
    /// Point d'accès unique au rig XR local (non réseau) : tête, mains, calibration sur le quadrant, vibrations.
    /// À placer sur le XR Origin.
    /// </summary>
    [RequireComponent(typeof(XROrigin))]
    public class LocalRig : MonoBehaviour
    {
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;

        XROrigin origin;
        HapticImpulsePlayer[] haptics;

        public static LocalRig Instance { get; private set; }

        public Transform Head => origin.Camera.transform;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public int Quadrant { get; private set; } = -1;

        /// <summary>Le joueur local vient de recevoir son quadrant.</summary>
        public static event Action<int> QuadrantAssigned;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Abysses] Plusieurs LocalRig dans la scène, le second est ignoré.", this);
                return;
            }
            Instance = this;
            origin = GetComponent<XROrigin>();
            haptics = GetComponentsInChildren<HapticImpulsePlayer>(true);
        }

        /// <summary>Vibration des deux manettes (retour d'événement de jeu : erreur, réussite, descente…).</summary>
        public static void Pulse(float amplitude, float duration)
        {
            if (Instance == null) return;
            HapticImpulsePlayer[] players = Instance.haptics;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].isActiveAndEnabled) players[i].SendHapticImpulse(amplitude, duration);
        }

        /// <summary>Vrai si le quadrant donné est celui du joueur local (-1 = concerne tout le monde).</summary>
        public static bool IsLocalOrShared(int quadrant)
        {
            return quadrant < 0 || (Instance != null && Instance.Quadrant == quadrant);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Associe le joueur local à son quadrant et recale l'origine XR dessus.</summary>
        public void AssignQuadrant(int quadrant)
        {
            if (!PlayArea.IsValid(quadrant)) return;
            Quadrant = quadrant;
            Recenter();
            QuadrantAssigned?.Invoke(quadrant);
        }

        /// <summary>
        /// Calibration : le joueur se tient au centre physique de son quadrant, face au fond de la piste.
        /// On fait coïncider sa position et son orientation réelles avec le centre virtuel du quadrant.
        /// </summary>
        public void Recenter()
        {
            if (!PlayArea.IsValid(Quadrant)) return;
            Vector3 center = PlayArea.GetCenter(Quadrant);
            origin.MatchOriginUpCameraForward(Vector3.up, PlayArea.CalibrationForward);
            Vector3 cameraPosition = origin.Camera.transform.position;
            origin.MoveCameraToWorldLocation(new Vector3(center.x, cameraPosition.y, center.z));
        }
    }
}
