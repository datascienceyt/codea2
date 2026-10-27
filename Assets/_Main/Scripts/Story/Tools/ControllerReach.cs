using System.Collections;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Que el mando entero sirva para tocar y agarrar, no solo la bolita blanca de su punta
/// (feedback del 27/10/2026: los niños tocan con el mando como si fuera el objeto entero).
///
/// En los mandos, el punto de pulsación de Meta (PokeInteractor) es una esfera de 0,5 cm en la
/// punta, y el de agarre (GrabInteractor) una de 5 cm. Aquí, en la escena de juego:
///   · la pulsación pasa al centro del modelo del mando, con un radio que lo cubre casi entero
///   · la esfera de agarre se centra también ahí y crece hasta cubrirlo
///   · se oculta la bolita blanca, que ya no marca dónde se toca
///
/// No toca el rig de Meta: se monta solo al cargar la escena y ajusta en tiempo de ejecución,
/// con el centro sacado del propio modelo del mando (sus renderers). Solo los mandos; las manos
/// siguen igual.
/// </summary>
public class ControllerReach : MonoBehaviour
{
    // Solo en el juego: en Setup el teclado del PIN tiene teclas pequeñas y juntas, y una esfera
    // grande pulsaría la de al lado.
    private const string GameScene = "Juego";

    // Radio de pulsación como fracción de la mitad del lado mayor del mando, con topes en metros.
    private const float PokeRadiusFactor = 0.7f;
    private const float MinPokeRadius = 0.02f;
    private const float MaxPokeRadius = 0.05f;

    // La esfera de agarre nunca encoge por debajo de la que trae Meta.
    private const float MaxGrabRadius = 0.08f;

    // Segundos que se espera a que aparezca el modelo del mando (se carga al detectarse).
    private const float WaitForModelSeconds = 30f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameScene || FindAnyObjectByType<ControllerReach>() != null) return;

        GameObject host = new GameObject(nameof(ControllerReach));
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<ControllerReach>();
    }

    private IEnumerator Start()
    {
        OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogWarning("[Mando] No hay OVRCameraRig: el área de interacción se queda como la de Meta.", this);
            yield break;
        }

        // Los anclajes del rig se crean en su Awake; un frame de margen.
        yield return null;

        StartCoroutine(Adjust(rig, Handedness.Left, rig.leftControllerAnchor));
        StartCoroutine(Adjust(rig, Handedness.Right, rig.rightControllerAnchor));
    }

    private IEnumerator Adjust(OVRCameraRig rig, Handedness side, Transform anchor)
    {
        if (anchor == null) yield break;

        // El modelo del mando aparece cuando el visor lo detecta: se espera a que tenga tamaño.
        Bounds bounds = default;
        for (float t = 0f; t < WaitForModelSeconds; t += Time.deltaTime)
        {
            if (TryModelBounds(anchor, out bounds)) break;
            yield return null;
        }

        if (bounds.size == Vector3.zero)
        {
            Debug.LogWarning($"[Mando] Sin modelo visible del mando {side}: se queda el área de Meta.", this);
            yield break;
        }

        float half = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        float pokeRadius = Mathf.Clamp(half * PokeRadiusFactor, MinPokeRadius, MaxPokeRadius);

        int pokes = 0, grabs = 0;

        foreach (PokeInteractor poke in FindObjectsByType<PokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsControllerSide(poke, side)) continue;

            Transform point = PointOf(poke);
            if (point == null) continue;

            // Se oculta la bolita antes de moverla: ya no marca el sitio que toca.
            foreach (Renderer r in point.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            point.position = bounds.center;
            poke.InjectRadius(pokeRadius);
            pokes++;
        }

        foreach (GrabInteractor grab in FindObjectsByType<GrabInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsControllerSide(grab, side) || grab.Rigidbody == null) continue;

            foreach (SphereCollider sphere in grab.Rigidbody.GetComponentsInChildren<SphereCollider>(true))
            {
                Transform t = sphere.transform;
                float scale = Mathf.Max(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);
                if (scale <= 0f) continue;

                sphere.center = t.InverseTransformPoint(bounds.center);
                sphere.radius = Mathf.Max(sphere.radius, Mathf.Min(half, MaxGrabRadius) / scale);
                grabs++;
            }
        }

        Debug.Log($"[Mando] {side}: pulsación en el centro del mando con radio {pokeRadius * 100f:0.#} cm " +
                  $"({pokes} interactor(es)), agarre ampliado ({grabs} esfera(s)).", this);
    }

    /// <summary>Caja de los renderers del modelo del mando, sin los visuales de los interactores.</summary>
    private static bool TryModelBounds(Transform anchor, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (Renderer r in anchor.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (r.GetComponentInParent<IInteractor>() != null) continue;
            if (r.bounds.size == Vector3.zero) continue;

            if (!any) bounds = r.bounds;
            else bounds.Encapsulate(r.bounds);
            any = true;
        }

        return any;
    }

    /// <summary>
    /// Un interactor de mando (no de mano) de ese lado. El lado sale del Controller del que
    /// cuelga; si no hay, del nombre de la jerarquía ("Left" / "Right").
    /// </summary>
    private static bool IsControllerSide(Component interactor, Handedness side)
    {
        // Los de mando se llaman ControllerPokeInteractor / ControllerGrabInteractor en el rig de
        // Meta; los de las manos (también las que mueve el mando) empiezan por "Hand".
        if (!interactor.name.StartsWith("Controller")) return false;

        string path = PathOf(interactor.transform);

        Controller controller = interactor.GetComponentInParent<Controller>(true);
        if (controller != null) return controller.Handedness == side;

        return path.Contains(side == Handedness.Left ? "Left" : "Right");
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    // El PokeInteractor no expone su punto; Origin es su posición, pero hace falta el Transform.
    private static Transform PointOf(PokeInteractor poke)
    {
        var field = typeof(PokeInteractor).GetField("_pointTransform",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field?.GetValue(poke) as Transform;
    }
}
