using UnityEngine;

public class Node : MonoBehaviour, IDelayInteractable
{
    [SerializeField] private float holdDuration = 1f;
    public float HoldDuration => holdDuration;

    public void Interact()
    {
        Debug.Log("Node is interacted.");
    }
}