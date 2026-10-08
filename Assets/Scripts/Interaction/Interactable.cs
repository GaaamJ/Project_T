using UnityEngine;
using Yarn.Unity;
using ProjectT.Data;
using ProjectT.Dialogue;

namespace ProjectT.Interaction
{
    public class Interactable : MonoBehaviour, IInteractable
    {
        [SerializeField] ObjectData data;

        public bool CanInteract => data != null && isActiveAndEnabled;

        void Awake()
        {
            if (data == null)
                Debug.LogWarning($"[Interactable] '{name}': ObjectData가 비어 있어 조사 대상에서 제외한다.", this);
        }

        public void Interact(InteractContext context)
        {
            switch (data.Kind)
            {
                case ObjectKind.Item:
                    Debug.Log($"[Interactable] 아이템 조사: {data.Id}", this);
                    break;
                case ObjectKind.Basic:
                    // await 없이 시작해야 Play가 같은 콜백 안에서 입력 잠금을 건다. 예외는 Forget()이 로그로 남긴다.
                    PlayBasic(context).Forget();
                    break;
                default:
                    Debug.LogWarning($"[Interactable] '{name}': 처리하지 않는 종류 {data.Kind}", this);
                    break;
            }
        }

        async YarnTask PlayBasic(InteractContext context)
        {
            if (context.Dialogue == null)
            {
                Debug.LogWarning($"[Interactable] '{name}': DialogueService가 없어 대사를 재생하지 못했다.", this);
                return;
            }

            string id = data.Id;
            string node = data.DialogueNode;
            if (context.Investigated.Contains(id))
            {
                if (string.IsNullOrWhiteSpace(data.RecheckDialogueNode))
                    Debug.LogWarning($"[Interactable] '{id}': 재조사 대사 노드가 비어 있어 첫 대사를 반복한다.", this);
                else
                    node = data.RecheckDialogueNode;
            }

            DialogueResult result = await context.Dialogue.Play(node);

            // 대사 중 이 오브젝트가 파괴될 수 있으므로 await 뒤에는 미리 받아 둔 값만 쓴다.
            if (result == DialogueResult.Completed)
                context.Investigated.Add(id);
        }
    }
}
