using UnityEngine;
using TMPro;
using ProjectT.Thread;

namespace ProjectT.UI
{
    // 플레이어가 보유한 실 버프의 타입과 잔여 시간을 표시한다.
    // 표시 형식: "{타입}: {잔여시간(0.0s)}"
    // 버프가 없을 때는 패널 자체를 숨긴다(별도 "없음" 텍스트 대신).
    //
    // ThreadBuffHolder 는 획득/교체/만료/소모 이벤트를 이미 노출하고 있지만,
    // 승인된 방침(Option A)에 따라 holder 를 수정하지 않고 Update 에서 HasBuff 를 폴링한다.
    // - false → true 전이 시점에 패널을 켠다(획득/교체 모두 이 전이를 발생시킴).
    // - 표시 중에는 매 프레임 CurrentType/RemainingTime 을 텍스트에 반영한다.
    // - true → false 전이 시점에 패널을 끈다.
    //
    // 이 정책의 부작용: Acquire 로 타입이 교체될 때 HasBuff 는 true 를 유지하므로
    // "패널을 껐다 켜는" 애니메이션 없이 텍스트만 갱신된다(승인된 의도된 동작).
    public class BuffPanelUI : MonoBehaviour
    {
        [Tooltip("참조할 실 버프 홀더. 인스펙터에서 직접 연결.")]
        [SerializeField] ThreadBuffHolder buffHolder;

        [Tooltip("버프 상태 표시 텍스트. 인스펙터에서 직접 연결.")]
        [SerializeField] TMP_Text label;

        [Tooltip("버프 유무에 따라 활성/비활성될 패널 루트. 비워두면 이 컴포넌트가 붙은 GameObject 를 대상으로 삼는다.")]
        [SerializeField] GameObject panelRoot;

        // 이전 프레임의 HasBuff — 전이 감지(false→true, true→false)에 사용.
        bool prevHasBuff;

        void Awake()
        {
            // 인스펙터에서 별도 패널 루트를 지정하지 않은 경우 이 컴포넌트가 붙은 오브젝트를 사용.
            // (UI 를 컴포넌트와 같은 GameObject 에 두는 흔한 구성 대응)
            if (panelRoot == null) panelRoot = gameObject;
        }

        void OnEnable()
        {
            // 초기 상태 즉시 반영 — 씬 시작 시 이미 버프가 있는 경우(디버그 등)에도 표시가 맞게 나오도록.
            if (buffHolder == null)
            {
                Debug.LogWarning("[BuffPanelUI] buffHolder 참조가 비어 있습니다.");
                SetPanelActive(false);
                return;
            }

            prevHasBuff = buffHolder.HasBuff;
            SetPanelActive(prevHasBuff);
            if (prevHasBuff) UpdateLabel();
        }

        void Update()
        {
            if (buffHolder == null) return;

            bool has = buffHolder.HasBuff;

            // 전이 감지: false → true (획득/교체 시작)
            if (has && !prevHasBuff)
            {
                SetPanelActive(true);
            }
            // 전이 감지: true → false (만료/소모)
            else if (!has && prevHasBuff)
            {
                SetPanelActive(false);
            }

            // 표시 중에는 매 프레임 갱신 — 잔여시간이 계속 변하고,
            // 교체(Replace) 케이스에서 CurrentType 도 즉시 반영되어야 하기 때문.
            if (has) UpdateLabel();

            prevHasBuff = has;
        }

        void SetPanelActive(bool active)
        {
            if (panelRoot != null && panelRoot.activeSelf != active)
                panelRoot.SetActive(active);
        }

        void UpdateLabel()
        {
            if (label == null) return;
            // "{Type}: {seconds}s" — 잔여시간은 소수점 1자리(0.1s 단위)로 표시.
            // ThreadBuffHolder.RemainingTime 이 음수로 내려가진 않지만(만료 처리) 안전상 Clamp.
            float remain = Mathf.Max(0f, buffHolder.RemainingTime);
            label.text = $"{buffHolder.CurrentType}: {remain:0.0}s";
        }
    }
}
