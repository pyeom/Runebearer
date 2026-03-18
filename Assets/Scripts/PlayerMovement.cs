using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private Vector3 targetPosition;
    private bool hasTarget = false;
    private Camera mainCamera;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
        targetPosition = transform.position;
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            int mask = groundLayer.value == 0 ? ~0 : (int)groundLayer;
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, mask))
            {
                Renderer tileRenderer = hit.collider.GetComponent<Renderer>();
                Vector3 tileCenter = tileRenderer != null
                    ? tileRenderer.bounds.center
                    : hit.collider.transform.position;
                targetPosition = new Vector3(tileCenter.x, transform.position.y, tileCenter.z);
                hasTarget = true;

                if (targetPosition != transform.position)
                    transform.rotation = Quaternion.LookRotation(targetPosition - transform.position);
            }
        }
    }

    void FixedUpdate()
    {
        if (!hasTarget) return;

        float dist = Vector3.Distance(rb.position, targetPosition);
        if (dist < 0.05f)
        {
            rb.MovePosition(targetPosition);
            hasTarget = false;
            return;
        }

        Vector3 dir = (targetPosition - rb.position).normalized;
        rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
    }
}
