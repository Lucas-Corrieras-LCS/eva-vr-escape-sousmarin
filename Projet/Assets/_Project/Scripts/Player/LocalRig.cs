using Unity.XR.CoreUtils;
using UnityEngine;
using Abysses.Core;

namespace Abysses.Player
{
    /// <summary>
    /// Point d'accès unique au rig XR local (non réseau) : tête, mains, calibration sur le quadrant.
    /// À placer sur le XR Origin.
    /// </summary>
    [RequireComponent(typeof(XROrigin))]
    public class LocalRig : MonoBehaviour
    {
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;

        XROrigin origin;

        public static LocalRig Instance { get; private set; }

        public Transform Head => origin.Camera.transform;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public int Quadrant { get; private set; } = -1;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Abysses] Plusieurs LocalRig dans la scène, le second est ignoré.", this);
                return;
            }
            Instance = this;
            origin = GetComponent<XROrigin>();
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
