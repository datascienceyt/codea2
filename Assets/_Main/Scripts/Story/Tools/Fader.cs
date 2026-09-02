using UnityEngine;
using System.Collections;

public class Fader : MonoBehaviour, IStepAction
{
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float sphereRadius = 0.15f; // debe ser mayor al near clip plane de la cámara

    [Tooltip("Material del fundido. Asígnalo: si se deja vacío se recurre a Shader.Find, que " +
             "puede fallar en el build de Quest porque el shader se strippea si ningún " +
             "material del proyecto lo referencia. En editor funciona igual y no se nota.")]
    [SerializeField] private Material fadeMaterial;

    private Material mat;
    private Transform sphereTransform;
    private bool isFadedIn = false; // true = negro completo, false = transparente

    void Start()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("[Fader] No hay cámara con el tag MainCamera. Con OVRCameraRig " +
                           "el tag va en CenterEyeAnchor.", this);
            enabled = false;
            return;
        }

        Material template = ResolveMaterial();

        if (template == null)
        {
            Debug.LogError("[Fader] No se pudo resolver el material del fundido.", this);
            enabled = false;
            return;
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>()); // no queremos física ni raycasts

        sphere.transform.SetParent(cam.transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * sphereRadius * 2f; // *2 porque el radio base es 0.5

        mat = new Material(template); // instancia propia: no tocar el asset compartido
        mat.color = Color.black;

        sphere.GetComponent<Renderer>().material = mat;
        sphere.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sphere.GetComponent<Renderer>().receiveShadows = false;

        sphereTransform = sphere.transform;

        SetAlpha(0f);
    }

    IEnumerator FadeIn() // transparente -> negro
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        SetAlpha(1f);
        isFadedIn = true;
    }

    IEnumerator FadeOut() // negro -> transparente
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        SetAlpha(0f);
        isFadedIn = false;
    }

    /// <summary>
    /// Material asignado en el inspector, o Sprites/Default como último recurso: unlit,
    /// Cull Off (se ve desde dentro de la esfera) y con soporte de alpha.
    /// </summary>
    Material ResolveMaterial()
    {
        if (fadeMaterial != null) return fadeMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }

    void SetAlpha(float a)
    {
        if (mat == null) return;

        Color c = mat.color;
        c.a = a;
        mat.color = c;
    }

    /// <summary>
    /// Si Start() no pudo montar la esfera (sin cámara o sin material), este componente no
    /// puede fundir. Importa comprobarlo porque el Director llama a Execute() directamente,
    /// sin pasar por el 'enabled' del MonoBehaviour.
    /// </summary>
    bool IsReady => mat != null;

    public void TriggerFadeIn()
    {
        if (IsReady) StartCoroutine(FadeIn());
    }

    public void TriggerFadeOut()
    {
        if (IsReady) StartCoroutine(FadeOut());
    }

    public IEnumerator Execute()
    {
        // Sin este corte, un Fader mal configurado colgaba el paso del Director con un NRE
        // en vez de dejar pasar la narrativa.
        if (!IsReady)
        {
            Debug.LogWarning("[Fader] Sin inicializar; se salta el fundido.", this);
            yield break;
        }

        yield return isFadedIn ? FadeOut() : FadeIn();
    }
}