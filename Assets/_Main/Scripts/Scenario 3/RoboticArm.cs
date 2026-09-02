using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Agente del Escenario 3. Gira entre posiciones fijas (ArmSlot) y recoge o suelta objetos
/// de la pila que tenga delante.
///
/// Es el equivalente de Bot en el Escenario 1: los bloques no le hablan por eventos, le
/// llaman a sus corrutinas y esperan a que terminen.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RoboticArm : MonoBehaviour
{
    [Header("Posiciones")]
    [Tooltip("En orden circular: girar a la derecha avanza en este array.")]
    [SerializeField] private ArmSlot[] slots;
    [SerializeField] private int startSlotIndex = 0;

    [Header("Movimiento")]
    [Tooltip("Transform que rota. Si queda vacío rota este mismo objeto.")]
    [SerializeField] private Transform pivot;

    [Tooltip("Dónde se coloca el objeto mientras el brazo lo lleva.")]
    [SerializeField] private Transform holdPoint;

    [SerializeField] private float rotateSpeed = 90f;

    [Tooltip("Pausa tras recoger o soltar, para que la acción se lea en VR.")]
    [SerializeField] private float actionPause = 0.25f;

    [Header("Pruebas")]
    [Tooltip("Repeticiones que ejecuta 'Probar/Repetir ciclo N veces'. Solo para depurar.")]
    [SerializeField] private int debugCycleCount = 4;

    [Header("Sonidos")]
    [SerializeField] private AudioClip pickSound;
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private AudioClip rotateSound;

    private AudioSource audioSource;
    private Transform held;
    private int slotIndex;
    private bool isBusy;

    /// <summary>Tras cada acción completada. Lo escucha Scenario3Controller para revisar la meta.</summary>
    public event Action OnActionCompleted;

    /// <summary>Recoger con las manos llenas, soltar vacío, o recoger de una pila vacía.</summary>
    public event Action OnInvalidAction;

    public bool IsHolding => held != null;
    public ArmSlot CurrentSlot => HasSlots ? slots[slotIndex] : null;

    private bool HasSlots => slots != null && slots.Length > 0;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (pivot == null) pivot = transform;
        if (holdPoint == null) holdPoint = transform;

        slotIndex = HasSlots ? Mathf.Clamp(startSlotIndex, 0, slots.Length - 1) : 0;
    }

    private void Start()
    {
        if (CurrentSlot != null)
            pivot.localRotation = Quaternion.Euler(0f, CurrentSlot.Yaw, 0f);
    }

    public IEnumerator Pick()
    {
        if (isBusy) yield break;

        ArmSlot slot = CurrentSlot;

        // Recoger con el brazo ocupado, o de un sitio donde no queda nada, es un error de
        // lógica del programa: se avisa y se sigue, no se aborta la secuencia.
        if (held != null || slot == null || slot.IsEmpty)
        {
            OnInvalidAction?.Invoke();
            yield break;
        }

        isBusy = true;

        held = slot.Take();
        held.SetParent(holdPoint);
        held.localPosition = Vector3.zero;
        held.localRotation = Quaternion.identity;

        Play(pickSound);
        yield return new WaitForSeconds(actionPause);

        isBusy = false;
        OnActionCompleted?.Invoke();
    }

    public IEnumerator Drop()
    {
        if (isBusy) yield break;

        ArmSlot slot = CurrentSlot;

        if (held == null || slot == null)
        {
            OnInvalidAction?.Invoke();
            yield break;
        }

        isBusy = true;

        slot.Put(held);
        held = null;

        Play(dropSound);
        yield return new WaitForSeconds(actionPause);

        isBusy = false;
        OnActionCompleted?.Invoke();
    }

    public IEnumerator RotateLeft() => RotateBy(-1);
    public IEnumerator RotateRight() => RotateBy(1);

    private IEnumerator RotateBy(int direction)
    {
        if (isBusy || !HasSlots) yield break;

        isBusy = true;

        // Módulo doble para que el índice no se salga por la izquierda con valores negativos.
        slotIndex = ((slotIndex + direction) % slots.Length + slots.Length) % slots.Length;

        Play(rotateSound);
        yield return RotateTowardsSlot(slots[slotIndex].Yaw, direction);

        isBusy = false;
        OnActionCompleted?.Invoke();
    }

    /// <summary>
    /// Gira hasta el ángulo del slot, pero SIEMPRE en el sentido pedido.
    ///
    /// Antes se usaba Quaternion.RotateTowards, que toma el camino más corto: con unos valores
    /// de yaw en cierto orden, "Girar Derecha" acababa girando visualmente a la izquierda. Un
    /// bloque que dice una cosa y hace otra rompe justo lo que el escenario enseña.
    /// </summary>
    private IEnumerator RotateTowardsSlot(float targetYaw, int direction)
    {
        float delta = Mathf.DeltaAngle(pivot.localEulerAngles.y, targetYaw);

        // Si el camino corto va en sentido contrario al pedido, se da la vuelta larga.
        if (direction > 0 && delta <= 0f) delta += 360f;
        if (direction < 0 && delta >= 0f) delta -= 360f;

        float total = Mathf.Abs(delta);
        float sign = Mathf.Sign(delta);
        float travelled = 0f;

        while (travelled < total)
        {
            float step = Mathf.Min(rotateSpeed * Time.deltaTime, total - travelled);

            pivot.Rotate(0f, step * sign, 0f, Space.Self);
            travelled += step;

            yield return null;
        }

        pivot.localRotation = Quaternion.Euler(0f, targetYaw, 0f);
    }

    /// <summary>Devuelve brazo y pilas al estado inicial. Para el botón de reinicio.</summary>
    [ContextMenu("Reset Arm")]
    public void ResetArm()
    {
        StopAllCoroutines();
        isBusy = false;
        held = null;

        if (!HasSlots) return;

        foreach (ArmSlot slot in slots)
            if (slot != null) slot.ResetSlot();

        slotIndex = Mathf.Clamp(startSlotIndex, 0, slots.Length - 1);
        pivot.localRotation = Quaternion.Euler(0f, slots[slotIndex].Yaw, 0f);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    // --- Controles de prueba (fuera del build) ---
    //
    // Permiten validar el brazo sin bloques, sin Director y sin visor: click derecho sobre
    // el componente en el inspector. Las acciones son corrutinas, así que hace falta estar
    // en Play Mode.

    [ContextMenu("Probar/Recoger")]
    private void DebugPick() => RunDebug(Pick());

    [ContextMenu("Probar/Soltar")]
    private void DebugDrop() => RunDebug(Drop());

    [ContextMenu("Probar/Girar Izquierda")]
    private void DebugRotateLeft() => RunDebug(RotateLeft());

    [ContextMenu("Probar/Girar Derecha")]
    private void DebugRotateRight() => RunDebug(RotateRight());

    /// <summary>
    /// El ciclo del GDD de un tirón: Recoger, Girar, Soltar, Girar. Es lo que el bloque
    /// Repetir tendrá que ejecutar N veces, así que sirve para comprobar de una sola vez
    /// que las cuatro acciones encajan entre sí.
    /// </summary>
    [ContextMenu("Probar/Ciclo completo")]
    private void DebugCycle() => RunDebug(DebugCycleRoutine(1));

    /// <summary>
    /// Repite el ciclo tantas veces como diga debugCycleCount. Es lo mismo que hará el
    /// SocketRow con sus repeticiones, así que sirve para comprobar si el nivel se resuelve
    /// con ese número antes de montar un solo bloque.
    /// </summary>
    [ContextMenu("Probar/Repetir ciclo N veces")]
    public void DebugRepeatCycle() => RunDebug(DebugCycleRoutine(debugCycleCount));

    private IEnumerator DebugCycleRoutine(int times)
    {
        for (int i = 0; i < Mathf.Max(1, times); i++)
        {
            Debug.Log($"[RoboticArm] Ciclo {i + 1}/{times}", this);

            yield return Pick();
            yield return RotateRight();
            yield return Drop();
            yield return RotateLeft();
        }
    }

    [ContextMenu("Probar/Estado")]
    private void DebugState()
    {
        if (!HasSlots)
        {
            Debug.LogWarning($"[RoboticArm] '{name}' no tiene posiciones asignadas.", this);
            return;
        }

        System.Text.StringBuilder report = new System.Text.StringBuilder();

        report.AppendLine($"[RoboticArm] {name}");
        report.AppendLine($"  Posición actual: {slotIndex} · {slots[slotIndex].name}");
        report.AppendLine($"  Sosteniendo: {(held != null ? held.name : "nada")}");
        report.AppendLine($"  Ocupado: {isBusy}");

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) { report.AppendLine($"  [{i}] (vacío en el inspector)"); continue; }

            string marker = i == slotIndex ? "→" : " ";
            report.AppendLine($"  {marker} [{i}] {slots[i].name}: {slots[i].Count} objeto(s)");
        }

        Debug.Log(report.ToString(), this);
    }

    private void RunDebug(IEnumerator routine)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[RoboticArm] Entra en Play Mode: las acciones son corrutinas.", this);
            return;
        }

        StartCoroutine(routine);
    }
}
