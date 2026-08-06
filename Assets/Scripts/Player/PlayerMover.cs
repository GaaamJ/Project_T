using UnityEngine;
using UnityEngine.InputSystem;

// mover for player
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private InputActionReference moveAction;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        var dir = moveAction.action.ReadValue<Vector2>();
        rb.MovePosition(rb.position + moveSpeed * Time.fixedDeltaTime * dir);
    }
}