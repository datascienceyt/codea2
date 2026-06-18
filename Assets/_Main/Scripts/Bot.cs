using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum Direction { Up = 0, Right = 1, Down = 2, Left = 3 }
public enum Rotation { Left = 0, Right = 1 };

public class Bot : MonoBehaviour
{
    public Vector2 botPos;
    private Vector2 tmpPos;
    public Direction direction;
    [HideInInspector] public LevelManager levelManager;
    bool isMoving, isRotating;

    public float moveSpeed = 2;
    public float rotateSpeed = 90;

    private Rigidbody rb;
    void Awake() => rb = GetComponent<Rigidbody>();

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TryMove();
        }

        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            TryRotate(Rotation.Left);
        }
        if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            TryRotate(Rotation.Right);
        }
    }

    [ContextMenu("TryMove")]
    void TryMove()
    {
        if (isMoving || isRotating) return;

        tmpPos = botPos;

        switch (direction)
        {
            case Direction.Up:
                tmpPos.y -= 1;
                break;
            case Direction.Down:
                tmpPos.y += 1;
                break;
            case Direction.Left:
                tmpPos.x -= 1;
                break;
            case Direction.Right:
                tmpPos.x += 1;
                break;
        }

        //print("Trying to move from " + botPos + " to " + tmpPos);

        if(levelManager.ValidMovementInGrid(tmpPos))
        {
            botPos = tmpPos;
            StartCoroutine(Move(tmpPos));
        }
        else
        {
            tmpPos = botPos;
        }
    }

    IEnumerator Move(Vector2 destination)
    {
        isMoving = true;

        int x = (int)destination.x;
        int y = (int)destination.y;

        Vector3 target = levelManager.objectGrid[y][x].transform.position;

        while(transform.position != target)
        {
            rb.MovePosition(Vector3.MoveTowards(rb.position, target, moveSpeed * Time.deltaTime));
            yield return null;
        }
        
        isMoving = false;
        //print("Movement done");
    }

    void TryRotate(Rotation rotation)
    {
        if (isMoving || isRotating) return;

        StartCoroutine(Rotate(rotation));
    }

    IEnumerator Rotate(Rotation rotation)
    {
        isRotating = true;

        float angle = rotation == Rotation.Right ? 90f : -90f;
        float rotated = 0f;
        float step = rotateSpeed * Time.deltaTime; // grados por frame

        while (Mathf.Abs(rotated) < 90f)
        {
            float delta = Mathf.Min(step, 90f - Mathf.Abs(rotated));
            transform.Rotate(0, rotation == Rotation.Right ? delta : -delta, 0);
            rotated += delta;
            yield return null;
        }

        // Snap exacto al final
        int dir = (int)direction;
        dir += rotation == Rotation.Right ? 1 : -1;
        direction = (Direction)(((dir % 4) + 4) % 4);
        transform.rotation = Quaternion.Euler(0, (int)direction * 90f, 0);

        isRotating = false;
    }
}
