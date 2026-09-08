using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
public class SpaceTrigger : MonoBehaviour
{
    void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[SpaceTrigger] {gameObject.name}: isTrigger이 false입니다. 자동으로 true로 설정합니다.");
            col.isTrigger = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        string id = $"{SceneManager.GetActiveScene().name}_{gameObject.name}";
        SaveManager.Instance.MarkVisited(id);
    }
}
