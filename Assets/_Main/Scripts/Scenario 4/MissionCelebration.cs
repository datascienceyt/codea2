using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// El premio por terminar la experiencia (09/11/2026). Antes, al completar el panel de la Sala 4
/// solo sonaban los motores y, tras su fundido, la escena se quedaba vacía. Ahora, a la vez que
/// arrancan los motores:
///
///   · una fanfarria de victoria y vítores
///   · dos cañones de confeti a los lados del jugador
///   · el rótulo "¡MISIÓN CUMPLIDA!" delante de él
///   · al acabar la fanfarria, música triunfal para el regreso a la Tierra. Sobrevive al cambio a
///     Setup y termina allí con su propio final, en vez de cortarse al cargar
///
/// Escucha el OnScenarioFinished del Scenario4Controller de su mismo objeto: no hay que cablear
/// nada. Todo se monta en tiempo de ejecución, sin piezas en la escena.
/// </summary>
[RequireComponent(typeof(Scenario4Controller))]
public class MissionCelebration : MonoBehaviour
{
    [Header("Sonido")]
    [Tooltip("Fanfarria de victoria, al completar el panel.")]
    [SerializeField] private AudioClip fanfare;
    [SerializeField, Range(0f, 1f)] private float fanfareVolume = 0.8f;

    [Tooltip("Vítores de la tripulación, a la vez que la fanfarria.")]
    [SerializeField] private AudioClip cheer;
    [SerializeField, Range(0f, 1f)] private float cheerVolume = 0.5f;

    [Tooltip("Música del regreso a casa. Entra al acabar la fanfarria y sigue en Setup hasta su final.")]
    [SerializeField] private AudioClip music;

    [Tooltip("Por debajo de la narración, que habla durante el regreso.")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.4f;

    [Header("Confeti")]
    [Tooltip("Material de partícula sin textura (URP Particles/Unlit). Sin él no hay confeti: " +
             "Shader.Find falla en el APK con un shader que nadie referencia.")]
    [SerializeField] private Material confettiMaterial;

    [Tooltip("Papelitos por cañón y disparo. Hay dos cañones y dos disparos.")]
    [SerializeField] private int confettiPerBurst = 120;

    [Header("Rótulo")]
    [SerializeField] private string title = "¡MISIÓN CUMPLIDA!";
    [SerializeField] private string subtitle = "Volvemos a casa";
    [SerializeField] private float titleSeconds = 7f;

    private static readonly Color Gold = new Color(1f, 0.82f, 0.2f);

    private static readonly Color[] ConfettiColors =
    {
        new Color(1f, 0.25f, 0.3f), new Color(1f, 0.8f, 0.15f), new Color(0.3f, 0.9f, 0.4f),
        new Color(0.25f, 0.75f, 1f), new Color(0.75f, 0.4f, 1f), new Color(1f, 0.55f, 0.15f),
    };

    private Scenario4Controller controller;
    private AudioSource source;
    private bool played;

    private void Awake()
    {
        controller = GetComponent<Scenario4Controller>();
        controller.OnScenarioFinished.AddListener(Play);
    }

    private void OnDestroy()
    {
        if (controller != null) controller.OnScenarioFinished.RemoveListener(Play);
    }

    [ContextMenu("Probar celebración")]
    public void Play()
    {
        if (played) return;

        played = true;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Debug.Log("[Final] Celebración: fanfarria, vítores, confeti y rótulo.", this);

        Camera view = Camera.main;
        Transform head = view != null ? view.transform : transform;

        PlayOneShot(fanfare, fanfareVolume);
        PlayOneShot(cheer, cheerVolume);
        SpawnConfetti(head);
        StartCoroutine(ShowTitle(head));

        if (music == null) yield break;

        yield return new WaitForSeconds(fanfare != null ? fanfare.length : 0f);
        PlayMusic();
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null) return;

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        source.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// En su propio objeto, que sobrevive al cambio a Setup: la música se oye entera, con su
    /// final, mientras el supervisor recoge el visor. Si se carga otra escena que no sea Setup
    /// (una sesión nueva), se corta.
    /// </summary>
    private void PlayMusic()
    {
        GameObject holder = new GameObject("Música del regreso");
        DontDestroyOnLoad(holder);

        AudioSource player = holder.AddComponent<AudioSource>();
        player.clip = music;
        player.volume = musicVolume;
        player.spatialBlend = 0f;
        player.Play();

        Destroy(holder, music.length + 0.5f);

        // La primera carga es la vuelta a Setup: ahí sigue sonando. Cualquier otra la corta.
        int loads = 0;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = null;
        onLoaded = (scene, mode) =>
        {
            bool cut = ++loads > 1 || scene.name != "Setup";
            if (cut && holder != null) Destroy(holder);
            if (cut || holder == null) SceneManager.sceneLoaded -= onLoaded;
        };
        SceneManager.sceneLoaded += onLoaded;
    }

    // ── Confeti ────────────────────────────────────────────────────────────────

    private void SpawnConfetti(Transform head)
    {
        if (confettiMaterial == null)
        {
            Debug.LogWarning("[Final] Sin material de confeti: no se lanza.", this);
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        // A los lados del jugador, a la altura de la cintura (no por delante: ahí está la mesa del
        // panel), disparando hacia arriba, adelante y adentro: el confeti sube por delante y le
        // cae alrededor.
        Vector3 basePoint = head.position + forward * 0.2f + Vector3.down * 0.8f;

        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 position = basePoint + right * (0.9f * side);
            Vector3 direction = (Vector3.up * 2.2f - right * (0.6f * side) + forward * 0.6f).normalized;
            Cannon(position, direction);
        }
    }

    private void Cannon(Vector3 position, Vector3 direction)
    {
        GameObject cannon = new GameObject("Confeti");
        cannon.SetActive(false);
        cannon.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));

        ParticleSystem particles = cannon.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(Palette()) { mode = ParticleSystemGradientMode.RandomColor };
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = confettiPerBurst * 2;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)confettiPerBurst),
            new ParticleSystem.Burst(0.45f, (short)confettiPerBurst),
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.05f;

        // Papel: frena en el aire y cae despacio, dando vueltas y con algo de vaivén.
        ParticleSystem.LimitVelocityOverLifetimeModule limit = particles.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 10f;
        limit.drag = 1.4f;

        ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.6f;

        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        Gradient alpha = new Gradient();
        alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = alpha;

        ParticleSystemRenderer renderer = cannon.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = confettiMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        cannon.SetActive(true);
        particles.Play();
    }

    /// <summary>Colores puros, sin mezclas entre ellos: cada papelito toma uno.</summary>
    private static Gradient Palette()
    {
        GradientColorKey[] keys = new GradientColorKey[ConfettiColors.Length];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = new GradientColorKey(ConfettiColors[i], (i + 1f) / keys.Length);

        Gradient gradient = new Gradient { mode = GradientMode.Fixed };
        gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    // ── Rótulo ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A 1,1 m y casi a la altura de los ojos, por encima de la mesa del panel. Al completar, el
    /// niño mira hacia abajo, al panel: más arriba le quedaría fuera de la vista, y más lejos
    /// detrás del panel.
    /// </summary>
    private IEnumerator ShowTitle(Transform head)
    {
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        GameObject label = new GameObject("Rótulo: misión cumplida");
        label.transform.SetPositionAndRotation(head.position + forward * 1.1f + Vector3.up * 0.05f,
                                               Quaternion.LookRotation(forward));

        TextMeshPro text = label.AddComponent<TextMeshPro>();
        text.text = $"{title}\n<size=50%><color=#FFFFFF>{subtitle}</color></size>";
        text.fontSize = 1.1f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Gold;
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;

        // Aparece con un pequeño rebote y se va encogiendo.
        const float popSeconds = 0.45f;
        for (float t = 0f; t < popSeconds; t += Time.deltaTime)
        {
            float x = t / popSeconds;
            float overshoot = 1f + 0.25f * Mathf.Sin(x * Mathf.PI);
            label.transform.localScale = Vector3.one * (x * overshoot);
            yield return null;
        }

        label.transform.localScale = Vector3.one;

        Vector3 start = label.transform.position;
        for (float t = 0f; t < titleSeconds; t += Time.deltaTime)
        {
            label.transform.position = start + Vector3.up * (0.03f * Mathf.Sin(t * 2f));
            yield return null;
        }

        const float outSeconds = 0.35f;
        for (float t = 0f; t < outSeconds; t += Time.deltaTime)
        {
            label.transform.localScale = Vector3.one * (1f - t / outSeconds);
            yield return null;
        }

        Destroy(label);
    }
}
