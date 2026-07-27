using System.Collections;
using UnityEngine;

[RequireComponent(typeof(VRGrabEvents))]
public abstract class BlockNode : MonoBehaviour
{
    public BlockNode Previous;
    public BlockNode Next;

    public Transform plugPoint;
    public Transform socketPoint;
    public float snapDistance = 0.05f;

    VRGrabEvents grabEvents;
    BlockNode candidate;
    bool isGrabbed;

    public abstract IEnumerator Execute();

    protected virtual void Awake()
    {
        grabEvents = GetComponent<VRGrabEvents>();
        grabEvents.onGrabbed.AddListener(OnGrabbed);
        grabEvents.onReleased.AddListener(OnReleased);
    }

    void OnGrabbed()
    {
        isGrabbed = true;
        if (Previous != null)
        {
            Previous.Next = null;
            Previous = null;
        }
        transform.SetParent(null);
    }

    void Update()
    {
        if (isGrabbed)
            candidate = FindNearestFreeSocket();
    }

    void OnReleased()
    {
        isGrabbed = false;
        if (candidate != null) AttachTo(candidate);
        candidate = null;
    }

    BlockNode FindNearestFreeSocket()
    {
        BlockNode best = null;
        float bestDist = snapDistance;

        foreach (var other in FindObjectsByType<BlockNode>())
        {
            if (other == this) continue;
            if (other.Next != null) continue;
            if (IsDescendantOf(other, this)) continue;

            float dist = Vector3.Distance(plugPoint.position, other.socketPoint.position);
            if (dist < bestDist) { best = other; bestDist = dist; }
        }
        return best;
    }

    void AttachTo(BlockNode parent)
    {
        transform.position += parent.socketPoint.position - plugPoint.position;
        transform.rotation = parent.socketPoint.rotation;
        transform.SetParent(parent.transform);

        parent.Next = this;
        Previous = parent;
    }

    bool IsDescendantOf(BlockNode node, BlockNode root)
    {
        var current = root.Next;
        while (current != null)
        {
            if (current == node) return true;
            current = current.Next;
        }
        return false;
    }
}