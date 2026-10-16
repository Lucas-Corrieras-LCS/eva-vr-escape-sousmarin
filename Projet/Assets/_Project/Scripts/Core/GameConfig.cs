using UnityEngine;

namespace Abysses.Core
{
    /// <summary>
    /// Réglages globaux du jeu. Un seul asset, chargé depuis Resources/GameConfig.
    /// </summary>
    [CreateAssetMenu(menuName = "Abysses/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Déplacement")]
        [Tooltip("Déplacement au joystick pour tester sans marcher. Forcé à false dans un build EVA (define ABYSSES_PROD).")]
        [SerializeField] bool allowJoystickLocomotion = true;

        [Header("Réseau")]
        [Tooltip("Nom de la session Relay : tous les casques qui rejoignent ce nom jouent ensemble.")]
        [SerializeField] string sessionName = "abysses-eva";
        [SerializeField, Range(1, 4)] int maxPlayers = 4;
        [SerializeField] ushort lanPort = 7777;
        [SerializeField] ushort lanDiscoveryPort = 47777;
        [Tooltip("Fréquence de synchronisation réseau (Hz). 30 suffit pour des avatars VR interpolés.")]
        [SerializeField, Range(10, 60)] int networkTickRate = 30;
        [SerializeField, Min(1f)] float lanJoinTimeout = 10f;

        static GameConfig instance;

        public static GameConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GameConfig>("GameConfig");
                    if (instance == null)
                    {
                        Debug.LogError("[Abysses] GameConfig introuvable dans Resources : valeurs par défaut utilisées.");
                        instance = CreateInstance<GameConfig>();
                    }
                }
                return instance;
            }
        }

        public bool AllowJoystickLocomotion
        {
            get
            {
#if ABYSSES_PROD
                return false;
#else
                return allowJoystickLocomotion;
#endif
            }
        }

        public string SessionName => sessionName;
        public int MaxPlayers => maxPlayers;
        public ushort LanPort => lanPort;
        public ushort LanDiscoveryPort => lanDiscoveryPort;
        public int NetworkTickRate => networkTickRate;
        public float LanJoinTimeout => lanJoinTimeout;
    }
}
