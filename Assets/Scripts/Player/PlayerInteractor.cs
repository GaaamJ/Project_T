using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private InputActionReference interactAction;
    private readonly List<IInteractable> interactables = new();
    private IDelayInteractable heldTarget;
    private float holdTimer = 0f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // interactable을 감지하면 List에 추가
        collision.TryGetComponent<IInteractable>(out var interactable);
        if (interactable != null)
        {
            interactables.Add(interactable);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // interactable이 범위를 벗어나면 List에서 제거
        collision.TryGetComponent<IInteractable>(out var interactable);
        if (interactable != null)
        {
            interactables.Remove(interactable);
        }
    }

    private void Update()
    {
        var closest = GetClosestInteractable();

        if (interactAction.action.WasPressedThisFrame())
        {
            if (closest is IInstantInteractable instant)
            {
                instant.Interact();
            }
            else if (closest is IDelayInteractable delayable)
            {
                heldTarget = delayable;   // 이 시점에만 확정, 잠금
                holdTimer = 0f;
            }
        }

        if (heldTarget != null && interactAction.action.IsPressed())
        {
            if (closest != heldTarget)   // 대상이 바뀌면 취소
            {
                heldTarget = null;
                holdTimer = 0f;
            }
            else
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= heldTarget.HoldDuration)
                {
                    heldTarget.Interact();
                    heldTarget = null;
                }
            }
        }

        if (interactAction.action.WasReleasedThisFrame())
        {
            heldTarget = null;
            holdTimer = 0f;
        }
    }

    public IInteractable GetClosestInteractable()
    {
        // 여러 개 겹쳐 있을 때 제일 가까운 것 선택
        return interactables
            .OrderBy(i => Vector2.Distance(transform.position, ((MonoBehaviour)i).transform.position)) // 거리순 정렬
            .FirstOrDefault(); // 가장 가까운 대상 반환, 없으면 null
    }
}