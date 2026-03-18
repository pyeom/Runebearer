using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private Camera mainCamera;

    private Queue<Vector3> pathQueue = new Queue<Vector3>();
    private Vector3 currentWaypoint;
    private bool isMoving = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int mask = groundLayer.value == 0 ? ~0 : (int)groundLayer;

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, mask))
            return;

        Vector2Int startCell = WorldToCell(transform.position);
        Vector2Int endCell   = WorldToCell(hit.collider.transform.position);

        if (startCell == endCell) return;

        var walkable = GridGenerator.Instance?.WalkableTiles;
        if (walkable == null) return;

        List<Vector2Int> path = Pathfinder.FindPath(startCell, endCell, walkable);
        if (path == null || path.Count == 0) return;

        pathQueue.Clear();
        isMoving = false;

        float playerY = transform.position.y;
        foreach (var cell in path)
            pathQueue.Enqueue(new Vector3(cell.x, playerY, cell.y));

        AdvanceToNextWaypoint();
    }

    void FixedUpdate()
    {
        if (!isMoving) return;

        float dist = Vector3.Distance(rb.position, currentWaypoint);
        if (dist < 0.05f)
        {
            rb.MovePosition(currentWaypoint);
            AdvanceToNextWaypoint();
            return;
        }

        Vector3 dir = (currentWaypoint - rb.position).normalized;
        rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
    }

    void AdvanceToNextWaypoint()
    {
        if (pathQueue.Count == 0)
        {
            isMoving = false;
            return;
        }

        currentWaypoint = pathQueue.Dequeue();
        isMoving = true;

        Vector3 dir = currentWaypoint - transform.position;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private Vector2Int WorldToCell(Vector3 pos)
    {
        return new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
    }
}
