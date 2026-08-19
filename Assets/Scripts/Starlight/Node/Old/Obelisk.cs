using UnityEngine;

public class Obelisk : MonoBehaviour, IDelayInteractable
{
    private NodeManager myManager;

    public float HoldDuration => .7f;
    public bool IsSelected => false; // 항상 상호작용 가능

    public void SetManager(NodeManager manager)
    {
        myManager = manager;
    }

    public void Interact()
    {
        var success = myManager.SubmitSymbol();

        if (success)
        {
            Debug.Log("상징 확정 성공!");
            // TODO: 성공 시 다른 오브젝트 활성화 등
        }
        else
        {
            Debug.Log("상징 확정 실패...");
            // TODO: 실패 피드백 (지금은 기획서상 "아무 일도 안 일어나거나 페널티" 중 뭐로 할지)
        }
    }
}
