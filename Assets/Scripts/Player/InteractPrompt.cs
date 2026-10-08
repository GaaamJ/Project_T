using UnityEngine;
using ProjectT.Interaction;
using ProjectT.Session;

namespace ProjectT.Player
{
    public class InteractPrompt : MonoBehaviour
    {
        [SerializeField] DetectZone detectZone;
        [SerializeField] InputLock inputLock;
        [SerializeField] SpriteRenderer promptRenderer;

        bool shown;

        void Awake()
        {
            if (detectZone == null)
                Debug.LogError("[InteractPrompt] 'detectZone' 참조가 비어 있다. 씬에서 연결해야 한다.", this);
            if (inputLock == null)
                Debug.LogError("[InteractPrompt] 'inputLock' 참조가 비어 있다. 씬에서 연결해야 한다.", this);
            if (promptRenderer == null)
                Debug.LogError("[InteractPrompt] 'promptRenderer' 참조가 비어 있다. 씬에서 연결해야 한다.", this);
            if (detectZone == null || inputLock == null || promptRenderer == null)
                enabled = false;
        }

        void OnEnable()
        {
            Show(ShouldShow());
        }

        void OnDisable()
        {
            if (promptRenderer != null)
                Show(false);
        }

        // DetectZone.Update에서 바뀐 대상을 같은 프레임에 반영하려고 LateUpdate에서 읽는다.
        void LateUpdate()
        {
            bool visible = ShouldShow();
            if (visible != shown)
                Show(visible);
        }

        bool ShouldShow()
        {
            return detectZone.CurrentTarget.IsAlive() && !inputLock.IsLocked;
        }

        void Show(bool visible)
        {
            shown = visible;
            promptRenderer.enabled = visible;
        }
    }
}
