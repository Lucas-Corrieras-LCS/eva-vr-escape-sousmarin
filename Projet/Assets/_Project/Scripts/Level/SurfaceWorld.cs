using UnityEngine;
using UnityEngine.Rendering;
using Abysses.Player;

namespace Abysses.Level
{
    /// <summary>
    /// Départ en surface et animation de plongée (client, aucune donnée réseau en plus : tout découle de la phase).
    ///
    /// À la surface : plateforme offshore au large d'une île, soleil, mer, ciel. Chaque cabine pend sous une potence.
    /// Première plongée (surface → étage 1), vue depuis les hublots :
    ///   0 s     le treuil se libère : secousse, claquement métallique, vibration forte des manettes ;
    ///   1–6 s   la cabine s'enfonce : le pont, puis la mer montent devant les hublots ; éclaboussure au passage de l'eau ;
    ///   6–9 s   sous l'eau : le bleu s'assombrit, les piliers de la plateforme défilent vers le haut ;
    ///   9 s     la surface est masquée et un « puits d'eau » entoure chaque cabine (le serveur charge l'étage derrière) ;
    ///   9–22 s  descente dans le noir, couleur de l'eau selon la profondeur, jusqu'à l'ouverture des portes.
    /// Descentes suivantes : le puits d'eau masque l'étage qui se décharge et celui qui se charge.
    /// Retour à la surface : remontée inverse (le puits se dissipe, la plateforme redescend, on émerge).
    ///
    /// Confort VR : la caméra n'est jamais déplacée ; c'est le monde extérieur, vu par les hublots, qui bouge.
    /// </summary>
    public class SurfaceWorld : MonoBehaviour
    {
        [Header("Monde de surface (déplacé verticalement pendant la plongée)")]
        [SerializeField] Transform world;
        [SerializeField] Light sun;
        [Tooltip("Câbles des potences : du treuil (qui monte avec le monde) au dôme de la cabine (fixe).")]
        [SerializeField] Transform[] cables;
        [SerializeField] Transform[] cableTops;
        [SerializeField] Vector3[] cableBottoms;
        [SerializeField] Renderer sea;

        [Header("Cabines")]
        [SerializeField] Renderer[] shrouds;
        [SerializeField] ParticleSystem[] splashes;

        [Header("Sons")]
        [SerializeField] AudioSource ambience;
        [SerializeField] AudioSource winch;
        [SerializeField] AudioSource splash;

        [Header("Ambiance de surface")]
        [SerializeField] Color sky = new Color(0.42f, 0.66f, 0.88f);
        [SerializeField] Color horizon = new Color(0.72f, 0.83f, 0.9f);
        [SerializeField] Color surfaceAmbient = new Color(0.5f, 0.56f, 0.62f);
        [SerializeField] float surfaceFarClip = 600f;
        [SerializeField] float underwaterFarClip = 40f;

        [Header("Plongée")]
        [Tooltip("Niveau de la mer (y) au repos ; le haut des hublots est à 1,88 m.")]
        [SerializeField] float seaLevel = -0.8f;
        [SerializeField] float windowTop = 1.88f;
        [SerializeField] float windowCenter = 1.5f;
        [Tooltip("Instant où la surface est masquée par le puits d'eau (doit précéder le chargement de l'étage par le serveur).")]
        [SerializeField] float hideTime = 9f;
        [SerializeField] float ascentDuration = 8f;

        /// <summary>Remontée en cours : les portes des cabines restent fermées.</summary>
        public static bool HoldDoors { get; private set; }

        enum Mode { Surface, FirstDive, Deep, Ascent, Hidden }

        Mode mode = Mode.Surface;
        float modeStart;
        float duration;
        int depthFrom;
        int depthTo;
        bool splashed;
        GamePhase lastPhase = GamePhase.Lobby;
        MaterialPropertyBlock block;
        Vector4 seaScroll;
        float baseSunIntensity;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            baseSunIntensity = sun != null ? sun.intensity : 1f;
            SetShrouds(false, Color.black);
            SetMode(Mode.Surface);
        }

        void OnEnable() => GameStateManager.PhaseChanged += OnPhaseChanged;
        void OnDisable()
        {
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            HoldDoors = false;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            GameStateManager state = GameStateManager.Instance;
            if (state == null) return;

            if (phase == GamePhase.Descending)
            {
                depthFrom = state.DepthOf(state.Floor.Value);
                depthTo = state.DepthOf(state.Floor.Value + 1);
                duration = state.DescentDurationFor(state.Floor.Value + 1);
                SetMode(state.Floor.Value == 0 ? Mode.FirstDive : Mode.Deep);
            }
            else if (phase == GamePhase.Lobby)
            {
                // Retour d'un étage (fin de partie, relance) : on remonte. Connexion à la surface : rien à animer.
                SetMode(lastPhase != GamePhase.Lobby ? Mode.Ascent : Mode.Surface);
            }
            else
            {
                SetMode(Mode.Hidden);
            }
            lastPhase = phase;
        }

        void SetMode(Mode next)
        {
            mode = next;
            modeStart = Time.unscaledTime;
            splashed = false;
            HoldDoors = next == Mode.Ascent;
            bool showWorld = next == Mode.Surface || next == Mode.FirstDive || next == Mode.Ascent;
            if (world != null) world.gameObject.SetActive(showWorld);
            for (int i = 0; i < cables.Length; i++)
                if (cables[i] != null) cables[i].gameObject.SetActive(showWorld);
            if (sun != null) sun.enabled = showWorld;

            switch (next)
            {
                case Mode.FirstDive:
                    if (winch != null) winch.Play();
                    LocalRig.Pulse(0.9f, 0.35f);
                    break;
                case Mode.Deep:
                    SetShrouds(true, DepthColor(depthFrom));
                    break;
                case Mode.Ascent:
                    SetShrouds(true, DepthColor(30f));
                    break;
                case Mode.Surface:
                case Mode.Hidden:
                    SetShrouds(false, Color.black);
                    break;
            }
            if (next == Mode.Surface) Place(0f);
        }

        void LateUpdate()
        {
            // Partie quittée (retour au menu hors ligne) : on revient à la surface.
            GameStateManager state = GameStateManager.Instance;
            if (mode != Mode.Surface && (state == null || !state.IsSpawned))
            {
                lastPhase = GamePhase.Lobby;
                SetMode(Mode.Surface);
            }
            float t = Time.unscaledTime - modeStart;
            switch (mode)
            {
                case Mode.Surface:
                    ApplyAtmosphere(0f);
                    break;
                case Mode.FirstDive:
                    UpdateFirstDive(t);
                    break;
                case Mode.Deep:
                    float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(1f, duration));
                    SetShrouds(true, DepthColor(Mathf.Lerp(depthFrom, depthTo, k)));
                    break;
                case Mode.Ascent:
                    UpdateAscent(t);
                    break;
            }
            ScrollSea();
        }

        void UpdateFirstDive(float t)
        {
            // Enfoncement (m) : petit saut quand le treuil lâche, palier, puis descente qui accélère.
            float drop;
            if (t < 0.25f) drop = Mathf.Lerp(0f, 0.3f, t / 0.25f);
            else if (t < 1.2f) drop = 0.3f + Mathf.Sin((t - 0.25f) * 9f) * 0.04f * (1.2f - t);
            else drop = 0.3f + Mathf.Pow(t - 1.2f, 1.7f) * 0.35f;

            // Secousse du monde vu par les hublots (jamais de la caméra).
            float shake = t < 0.6f ? 0.035f * (1f - t / 0.6f) : 0.006f;
            Vector3 jitter = new Vector3(Mathf.Sin(t * 37f), Mathf.Sin(t * 23f + 1.3f), Mathf.Sin(t * 29f + 2.1f)) * shake;
            Place(drop, jitter);

            float water = seaLevel + drop;
            if (!splashed && water >= windowCenter)
            {
                splashed = true;
                if (splash != null) splash.Play();
                for (int i = 0; i < splashes.Length; i++)
                    if (splashes[i] != null) splashes[i].Play();
                LocalRig.Pulse(0.55f, 0.25f);
            }

            // Sous l'eau dès que la mer dépasse le haut des hublots : l'eau s'assombrit avec la profondeur visible.
            float under = water - windowTop;
            ApplyAtmosphere(under);
            if (ambience != null) ambience.volume = Mathf.Clamp01(1f - under * 0.8f) * 0.6f;

            if (t >= hideTime)
            {
                SetMode(Mode.Deep, true);
                SetShrouds(true, DepthColor(UnderwaterDepth(t)));
            }
        }

        /// <summary>Bascule en descente profonde en gardant l'horloge de la plongée (même timeline pour la couleur).</summary>
        void SetMode(Mode next, bool keepTime)
        {
            float start = modeStart;
            SetMode(next);
            if (keepTime) modeStart = start;
        }

        float UnderwaterDepth(float t)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(1f, duration));
            return Mathf.Max(15f, Mathf.Lerp(depthFrom, depthTo, k));
        }

        void UpdateAscent(float t)
        {
            // Le puits se dissipe vite (l'étage est déjà déchargé), puis la plateforme redescend : on émerge.
            if (t > 1f) SetShrouds(false, Color.black);
            float k = Mathf.Clamp01(t / ascentDuration);
            float drop = Mathf.Lerp(16f, 0f, 1f - Mathf.Pow(1f - k, 2.2f));
            Place(drop);
            float water = seaLevel + drop;
            if (!splashed && water <= windowCenter)
            {
                splashed = true;
                if (splash != null) splash.Play();
                LocalRig.Pulse(0.5f, 0.2f);
            }
            ApplyAtmosphere(water - windowTop);
            if (ambience != null) ambience.volume = Mathf.Clamp01(1f - (water - windowTop) * 0.8f) * 0.6f;
            if (t >= ascentDuration) SetMode(Mode.Surface);
        }

        void Place(float drop, Vector3 jitter = default)
        {
            if (world != null) world.position = new Vector3(0f, drop, 0f) + jitter;
            for (int i = 0; i < cables.Length && i < cableTops.Length && i < cableBottoms.Length; i++)
            {
                if (cables[i] == null || cableTops[i] == null) continue;
                Vector3 top = cableTops[i].position, bottom = cableBottoms[i];
                cables[i].position = (top + bottom) * 0.5f;
                Vector3 scale = cables[i].localScale;
                scale.y = Mathf.Max(0.01f, (top.y - bottom.y) * 0.5f);
                cables[i].localScale = scale;
            }
        }

        /// <summary><paramref name="underwater"/> : hauteur d'eau au-dessus des hublots (m), négative à l'air libre.</summary>
        void ApplyAtmosphere(float underwater)
        {
            Camera main = Camera.main;
            if (underwater <= 0f)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 80f;
                RenderSettings.fogEndDistance = 520f;
                RenderSettings.fogColor = horizon;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = surfaceAmbient;
                if (sun != null) sun.intensity = baseSunIntensity;
                if (main != null)
                {
                    main.backgroundColor = sky;
                    main.farClipPlane = surfaceFarClip;
                }
                return;
            }
            Color water = DepthColor(underwater * 2.5f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = water;
            RenderSettings.fogDensity = Mathf.Lerp(0.05f, 0.14f, Mathf.Clamp01(underwater / 10f));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.2f, 0.42f, 0.5f), new Color(0.05f, 0.12f, 0.15f), Mathf.Clamp01(underwater / 10f));
            if (sun != null) sun.intensity = baseSunIntensity * Mathf.Clamp01(1f - underwater / 12f);
            if (main != null)
            {
                main.backgroundColor = water;
                main.farClipPlane = Mathf.Lerp(surfaceFarClip, underwaterFarClip, Mathf.Clamp01(underwater / 3f));
            }
        }

        /// <summary>Couleur de l'eau selon la profondeur (m) : turquoise sous la surface, noir dans les abysses.</summary>
        public static Color DepthColor(float depth)
        {
            if (depth < 30f) return Color.Lerp(new Color(0.14f, 0.52f, 0.56f), new Color(0.05f, 0.3f, 0.38f), depth / 30f);
            if (depth < 200f) return Color.Lerp(new Color(0.05f, 0.3f, 0.38f), new Color(0.02f, 0.09f, 0.11f), (depth - 30f) / 170f);
            if (depth < 1500f) return Color.Lerp(new Color(0.02f, 0.09f, 0.11f), new Color(0.01f, 0.03f, 0.05f), (depth - 200f) / 1300f);
            return Color.Lerp(new Color(0.01f, 0.03f, 0.05f), new Color(0.006f, 0.008f, 0.02f), Mathf.Clamp01((depth - 1500f) / 2500f));
        }

        void SetShrouds(bool visible, Color color)
        {
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < shrouds.Length; i++)
            {
                Renderer r = shrouds[i];
                if (r == null) continue;
                if (r.enabled != visible) r.enabled = visible;
                if (!visible) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        void ScrollSea()
        {
            if (sea == null || !sea.gameObject.activeInHierarchy) return;
            float t = Time.time;
            seaScroll = new Vector4(60f, 60f, t * 0.011f, t * 0.007f);
            sea.GetPropertyBlock(block);
            block.SetVector(BaseMapStId, seaScroll);
            sea.SetPropertyBlock(block);
        }
    }
}
