using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yarn.Unity;

namespace ProjectT.Dialogue
{
    // <<Cutscene id>>: Resources/Cutscenes/{id} 이미지를 띄우고 닫기 입력(Z)이 올 때까지 대사를 멈춘다.
    [RequireComponent(typeof(DialogueRunner))]
    public class CutsceneCommand : MonoBehaviour
    {
        const string CommandName = "Cutscene";
        const string ResourceFolder = "Cutscenes/";

        [SerializeField] GameObject cutsceneRoot;
        [SerializeField] Image cutsceneImage;
        [SerializeField] InputActionReference closeAction;

        DialogueRunner runner;

        void Awake()
        {
            runner = GetComponent<DialogueRunner>();
            cutsceneRoot.SetActive(false);
        }

        void OnEnable()
        {
            runner.AddCommandHandler<string>(CommandName, Show);
            // 액션 애셋을 여러 곳이 공유하므로 OnDisable에서 Disable()하지 않는다.
            closeAction.action.Enable();
        }

        void OnDisable()
        {
            runner.RemoveCommandHandler(CommandName);
            Hide();
        }

        async YarnTask Show(string id)
        {
            Sprite sprite = Resources.Load<Sprite>(ResourceFolder + id);
            if (sprite == null)
            {
                Debug.LogWarning($"[Cutscene] '{ResourceFolder}{id}' 이미지가 없어 건너뛴다.", this);
                return;
            }

            cutsceneImage.sprite = sprite;
            cutsceneRoot.SetActive(true);

            try
            {
                // 직전 대사를 넘긴 Z가 같은 프레임에 컷씬까지 닫지 않게 한 프레임 넘긴다.
                await YarnTask.Yield();
                await YarnTask.WaitUntil(
                    () => closeAction.action.WasPressedThisFrame() || !runner.IsDialogueRunning,
                    destroyCancellationToken);
                Hide();
                // 컷씬을 닫은 Z가 다음 대사까지 넘기지 않게 한 프레임 뒤에 재개한다.
                await YarnTask.Yield();
            }
            finally
            {
                Hide();
            }
        }

        void Hide()
        {
            if (cutsceneRoot != null)
                cutsceneRoot.SetActive(false);
        }
    }
}
