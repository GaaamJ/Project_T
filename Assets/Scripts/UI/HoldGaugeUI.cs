using UnityEngine;
using UnityEngine.UI;
using ProjectT.Player;

namespace ProjectT.UI
{
    // Coordinate 근처에서 플레이어가 Interact 를 Hold 시작하면 게이지를 표시한다.
    // 이 컴포넌트는 각 Coordinate GameObject 본체에 부착된다(승인된 방침, 프리팹 없이 개별 씬 편집).
    //
    // 표시 조건: PlayerInteract.OnInteractHoldStarted 발생 && 이 Coordinate 가 현재 DetectZone.CurrentTarget 이어야 함.
    // - CurrentTarget 이 자신이 아니면 게이지를 띄우지 않는다(다른 Coordinate 근처에서 Hold 한 경우).
    // 종료 조건: PlayerInteract.OnInteractHoldCanceled / OnInteractHoldPerformed 어느 쪽이든 게이지를 감춘다.
    //
    // 홀드 길이(holdDuration):
    // - Input System 의 Hold 인터랙션 시간과 별개로 관리한다(승인된 방침).
    // - 두 값이 어긋나면 게이지가 100% 도달 전/후로 상호작용이 완료될 수 있음(인지된 트레이드오프).
    public class HoldGaugeUI : MonoBehaviour
    {
        [Tooltip("플레이어의 상호작용 컴포넌트. 인스펙터에서 직접 연결(자동 검색 폴백 없음).")]
        [SerializeField] PlayerInteract playerInteract;

        [Tooltip("게이지를 그릴 Slider (Unity 기본 Slider, interactable=false 권장).")]
        [SerializeField] Slider slider;

        [Tooltip("Slider 를 포함해 표시/숨김할 루트 오브젝트. 비워두면 slider 의 GameObject 를 사용.")]
        [SerializeField] GameObject gaugeRoot;

        [Tooltip("게이지가 가득 차기까지 걸리는 시간(초). Input System 의 Hold 시간과 별도로 관리.")]
        [SerializeField] float holdDuration = 0.4f;

        // 캐시: 매 프레임 GetComponent 를 피하기 위함(성능보다는 명시성 목적).
        Coordinate.Coordinate coordinate;

        // Hold 진행 상태.
        bool isHolding;
        float elapsed;

        void Awake()
        {
            // 이 컴포넌트는 Coordinate GameObject 본체에 부착되는 것을 전제로 설계됨 —
            // GetComponent 로 자신이 붙은 Coordinate 를 찾아 CurrentTarget 비교에 사용.
            coordinate = GetComponent<Coordinate.Coordinate>();
            if (coordinate == null)
                Debug.LogWarning($"[HoldGaugeUI] {name}: 같은 GameObject 에 Coordinate 컴포넌트가 없습니다.");

            if (gaugeRoot == null && slider != null)
                gaugeRoot = slider.gameObject;
        }

        void OnEnable()
        {
            if (playerInteract == null)
            {
                Debug.LogWarning($"[HoldGaugeUI] {name}: playerInteract 참조가 비어 있습니다.");
            }
            else
            {
                playerInteract.OnInteractHoldStarted += OnHoldStarted;
                playerInteract.OnInteractHoldCanceled += OnHoldEnded;
                playerInteract.OnInteractHoldPerformed += OnHoldEnded;
            }

            // 초기 상태는 항상 숨김 — 씬 편집 시 실수로 켜져 있어도 강제로 꺼서 일관성 보장.
            HideGauge();
        }

        void OnDisable()
        {
            if (playerInteract != null)
            {
                playerInteract.OnInteractHoldStarted -= OnHoldStarted;
                playerInteract.OnInteractHoldCanceled -= OnHoldEnded;
                playerInteract.OnInteractHoldPerformed -= OnHoldEnded;
            }

            HideGauge();
        }

        void OnHoldStarted()
        {
            // 이 Coordinate 가 실제 감지 대상인지 확인 — 다른 Coordinate 근처에서 Hold 시작한 경우는 무시.
            if (playerInteract == null || playerInteract.DetectZone == null) return;
            if (playerInteract.DetectZone.CurrentTarget != coordinate) return;

            isHolding = true;
            elapsed = 0f;
            ShowGauge();
            UpdateGaugeValue();
        }

        void OnHoldEnded()
        {
            // Started 를 우리가 무시한 경우엔 isHolding 이 false 라서 아래 HideGauge 만 no-op 로 처리됨.
            isHolding = false;
            elapsed = 0f;
            HideGauge();
        }

        void Update()
        {
            if (!isHolding) return;

            elapsed += Time.deltaTime;
            UpdateGaugeValue();

            // holdDuration 에 도달하면 스스로 게이지를 감춘다 —
            // Input System 의 performed 이벤트가 도착할 때까지 기다리지 않고 시각적으로 즉시 완료 표시.
            // (performed 이벤트도 뒤이어 도착해 OnHoldEnded 를 호출하므로 상태가 이중으로 정리됨)
            if (elapsed >= holdDuration)
            {
                isHolding = false;
                HideGauge();
            }
        }

        void UpdateGaugeValue()
        {
            if (slider == null) return;

            // Slider 의 min/max 는 기본값(0~1) 을 가정. holdDuration 대비 진행률로 매핑.
            float t = holdDuration > 0f ? Mathf.Clamp01(elapsed / holdDuration) : 1f;
            slider.value = t;
        }

        void ShowGauge()
        {
            if (gaugeRoot != null && !gaugeRoot.activeSelf)
                gaugeRoot.SetActive(true);
        }

        void HideGauge()
        {
            if (gaugeRoot != null && gaugeRoot.activeSelf)
                gaugeRoot.SetActive(false);
        }
    }
}
