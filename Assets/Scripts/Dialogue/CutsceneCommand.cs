using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

namespace ProjectT.Dialogue
{
    // <<Cutscene id>>: Resources/Cutscenes/{id} 이미지를 대사창 뒤에 띄우고 대사는 그대로 진행한다.
    // <<CutsceneEnd>> 또는 대화 종료 때 닫는다.
    [RequireComponent(typeof(DialogueRunner))]
    public class CutsceneCommand : MonoBehaviour
    {
        const string ShowCommandName = "Cutscene";
        const string EndCommandName = "CutsceneEnd";
        const string ResourceFolder = "Cutscenes/";

        [SerializeField] GameObject cutsceneRoot;
        [SerializeField] Image cutsceneImage;

        DialogueRunner runner;

        void Awake()
        {
            runner = GetComponent<DialogueRunner>();
            cutsceneRoot.SetActive(false);
        }

        void OnEnable()
        {
            runner.AddCommandHandler<string>(ShowCommandName, Show);
            runner.AddCommandHandler(EndCommandName, Hide);
            runner.onDialogueComplete?.AddListener(Hide);
        }

        void OnDisable()
        {
            runner.RemoveCommandHandler(ShowCommandName);
            runner.RemoveCommandHandler(EndCommandName);
            runner.onDialogueComplete?.RemoveListener(Hide);
            Hide();
        }

        void Show(string id)
        {
            Sprite sprite = Resources.Load<Sprite>(ResourceFolder + id);
            if (sprite == null)
            {
                Debug.LogWarning($"[Cutscene] '{ResourceFolder}{id}' 이미지가 없어 건너뛴다.", this);
                return;
            }

            cutsceneImage.sprite = sprite;
            cutsceneRoot.SetActive(true);
        }

        void Hide()
        {
            if (cutsceneRoot != null)
                cutsceneRoot.SetActive(false);
        }
    }
}
