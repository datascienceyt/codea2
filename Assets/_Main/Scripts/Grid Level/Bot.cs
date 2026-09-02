using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum Direction { Up = 0, Right = 1, Down = 2, Left = 3 }
public enum Rotation { Left = -1, Right = 1 }

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class Bot : MonoBehaviour
{
    public Vector2 botPos;
    private Vector2 tmpPos;
    public Direction direction;
    [HideInInspector] public LevelManager levelManager;
    bool isMoving, isRotating;

    public float moveSpeed = 2;
    public float rotateSpeed = 90;

    [Header("Sonidos")]
    public AudioClip moveSound;
    public AudioClip useSound;

    private Rigidbody rb;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        StartCoroutine(RotateTo(direction));
    }

#if UNITY_EDITOR
    // Control de depuración, solo en editor: en Quest no hay teclado y Keyboard.current
    // es null, lo que lanzaría una NullReferenceException cada frame.
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.wKey.wasPressedThisFrame)
            StartCoroutine(MoveForward());
        if (keyboard.aKey.wasPressedThisFrame)
            StartCoroutine(RotateLeft());
        if (keyboard.dKey.wasPressedThisFrame)
            StartCoroutine(RotateRight());
        if (keyboard.spaceKey.wasPressedThisFrame)
            StartCoroutine(Use());
    }
#endif

    public IEnumerator MoveForward()
    {
        if (isMoving || isRotating) yield break;

        tmpPos = GetPositionInDirection(botPos, direction);

        if (!levelManager.ValidMovementInGrid(tmpPos))
            yield break;

        botPos = tmpPos;
        PlaySound(moveSound);
        yield return Move(tmpPos);
    }

    public IEnumerator RotateLeft() => RotateBy(Rotation.Left);
    public IEnumerator RotateRight() => RotateBy(Rotation.Right);

    IEnumerator RotateBy(Rotation rotation)
    {
        if (isMoving || isRotating) yield break;

        int dir = ((int)direction + (int)rotation + 4) % 4;
        yield return RotateTo((Direction)dir);
    }

    public IEnumerator Use()
    {
        if (isMoving || isRotating) yield break;

        Vector2 targetPos = GetPositionInDirection(botPos, direction);

        if (!levelManager.TryGetInteractable(targetPos, out IInteractable interactable))
        {
            TelemetryManager.Instance.RegisterLogicError(LogicErrorType.ComandoInvalido);
            yield break;
        }

        if (!interactable.CanInteract())
        {
            TelemetryManager.Instance.RegisterLogicError(LogicErrorType.ComandoInvalido);
            yield break;
        }

        PlaySound(useSound);
        yield return interactable.Interact();
    }

    Vector2 GetPositionInDirection(Vector2 origin, Direction dir)
    {
        Vector2 result = origin;
        switch (dir)
        {
            case Direction.Up: result.y -= 1; break;
            case Direction.Down: result.y += 1; break;
            case Direction.Left: result.x -= 1; break;
            case Direction.Right: result.x += 1; break;
        }
        return result;
    }

    IEnumerator Move(Vector2 destination)
    {
        int x = (int)destination.x;
        int y = (int)destination.y;

        GameObject tile = TileObjectAt(x, y);
        if (tile == null)
        {
            Debug.LogWarning($"[Bot] No hay objeto de casilla en ({x},{y}); se cancela el movimiento.");
            yield break;
        }

        Vector3 target = tile.transform.position;

        isMoving = true;

        // Se compara por distancia y no con 'transform.position != target'.
        //
        // El operador != de Vector3 usa una tolerancia fija, y aquí se leía transform.position
        // mientras se escribía con rb.MovePosition, que no se aplica hasta el siguiente paso
        // de física. Bastaba un desfase de coma flotante para que el bucle no cerrara nunca
        // y dejara isMoving bloqueado, congelando al bot para el resto de la partida.
        while (Vector3.Distance(rb.position, target) > 0.001f)
        {
            rb.MovePosition(Vector3.MoveTowards(rb.position, target, moveSpeed * Time.deltaTime));
            yield return null;
        }

        rb.MovePosition(target);
        isMoving = false;
    }

    GameObject TileObjectAt(int x, int y)
    {
        GameObject[][] objectGrid = levelManager != null ? levelManager.objectGrid : null;

        if (objectGrid == null) return null;
        if (y < 0 || y >= objectGrid.Length) return null;
        if (objectGrid[y] == null || x < 0 || x >= objectGrid[y].Length) return null;

        return objectGrid[y][x];
    }

    public IEnumerator RotateTo(Direction targetDirection)
    {
        if (isMoving) yield break;

        isRotating = true;
        direction = targetDirection;

        Quaternion targetRot = Quaternion.Euler(0, (int)targetDirection * 90f, 0);
        while (Quaternion.Angle(transform.localRotation, targetRot) > 0.1f)
        {
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRot, rotateSpeed * Time.deltaTime);
            yield return null;
        }
        transform.localRotation = targetRot;
        isRotating = false;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }
}
