using UnityEngine;
using System.Collections;

public class Fader : MonoBehaviour, IStepAction
{
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float sphereRadius = 0.15f; // debe ser mayor al near clip plane de la cámara

    private Material mat;
    private Transform sphereTransform;
    private bool isFadedIn = false; // true = negro completo, false = transparente

    void Start()
    {
        Camera cam = Camera.main;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>()); // no queremos física ni raycasts

        sphere.transform.SetParent(cam.transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * sphereRadius * 2f; // *2 porque el radio base es 0.5

        mat = new Material(Shader.Find("Sprites/Default")); // unlit, Cull Off (se ve desde adentro), soporta alpha
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

    void SetAlpha(float a)
    {
        Color c = mat.color;
        c.a = a;
        mat.color = c;
    }

    public void TriggerFadeIn() => StartCoroutine(FadeIn());
    public void TriggerFadeOut() => StartCoroutine(FadeOut());

    public IEnumerator Execute()
    {
        yield return isFadedIn ? FadeOut() : FadeIn();
    }
}