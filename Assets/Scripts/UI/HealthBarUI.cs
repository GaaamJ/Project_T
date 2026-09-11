using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ProjectT.Player;
using ProjectT.Stage;

namespace ProjectT.UI
{
    // 체력 UI. 배경(어두운 회색) 위에 막대(빨강/흰색)를 얹은 A안 구성.
    // 왜 별도 컴포넌트인가:
    // - HealthSystem/PlayerRecovery/StageManager 이벤트를 한곳에서 조합해야 하고,
    //   집중 회복 진행 표현(막대 채우기, 색 전환, 페이드) 상태 머신이 UI 쪽에 전담되어야 하기 때문.
    // - 세 이벤트 소스는 순서 보장이 없어 한쪽 컴포넌트에서 통합 관리하는 편이 상태 일관성 유지에 유리.
    //
    // 상태 요약(내부):
    //  - Hidden           : 최대 체력·페이드 완료 상태. CanvasGroup.alpha = 0
    //  - Visible          : 체력 < 최대. 막대 = 실제 체력 비율(50% 또는 5%), 색 = 빨강
    //  - Focusing         : 회복 집중 중. 막대 = 0.5 + 0.5 * (progress). 1초 지나면 빨강→흰색
    //  - Cancelling       : 집중 취소 후 실제 체력 비율로 되돌리는 짧은 애니(0.2초)
    //  - FadingOut        : 회복 완료 후 1.5초 페이드 아웃
    //
    // 스펙 결정 사항(2025-09-11 사용자 확정):
    //  1) 막대 채우기 곡선: 선형(0.5 + 0.5*t), 이징 없음
    //  2) 체력 0에서도 집중 시작 가능(빨강→1초→흰색 동일)
    //  3) 취소 시 색은 즉시 빨강 스냅
    //  4) 페이드아웃 중 재피격: 코루틴 중단·alpha=1 즉시 복구, 50%+빨강 스냅
    //  5) 축소 애니 중 재피격: 코루틴 중단, 새 목표(5%)로 재보간, 색은 빨강 유지
    //  6) OnHealthChanged/OnDamaged 둘 다 구독, 순서 무의존
    //  7) 회복 완료 직후 OnFocusCompleted로 1.5초 페이드 시작. 사이에 오는 OnHealthChanged(2,2)는 페이드 지속
    //  8) OnStageFailed 구독 → 진행 중 코루틴 모두 중단 + 즉시 Hidden 상태 강제
    //  9) 임시 수치는 모두 인스펙터 SerializeField로 노출
    // 10) 테두리 표현: A안(배경 + 막대). 폴리싱 세션에서 프레임 스프라이트로 교체 예정
    public class HealthBarUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("체력 상태 소스. 인스펙터에서 직접 연결.")]
        [SerializeField] HealthSystem healthSystem;
        [Tooltip("V 홀드 회복 집중 상태 소스. 인스펙터에서 직접 연결.")]
        [SerializeField] PlayerRecovery playerRecovery;
        [Tooltip("스테이지 실패 시 UI를 강제 초기화하기 위한 참조. 인스펙터에서 직접 연결.")]
        [SerializeField] StageManager stageManager;

        [Header("UI Elements")]
        [Tooltip("페이드 전체를 제어할 CanvasGroup. 이 컴포넌트가 붙은 오브젝트에 있어야 한다.")]
        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("내부 막대 Image. 색상 전환 대상.")]
        [SerializeField] Image fillImage;
        [Tooltip("내부 막대의 RectTransform. anchor stretch + pivot(0, 0.5) 상태에서 offsetMax.x 로 길이를 조절한다.")]
        [SerializeField] RectTransform fillRect;
        [Tooltip("막대가 놓이는 부모(Background 내부) 영역. width를 참조해 fillRect의 offset을 계산한다.")]
        [SerializeField] RectTransform fillTrackRect;

        [Header("Colors")]
        [Tooltip("피격/체력 부족/취소 상태 막대 색. 기획서 임시 수치.")]
        [SerializeField] Color damagedColor = new Color(1f, 0.25f, 0.25f, 1f);
        [Tooltip("집중 회복 1초 경과 후 전환되는 막대 색.")]
        [SerializeField] Color focusColor = Color.white;

        [Header("Fill Ratios")]
        [Tooltip("체력 최대일 때 막대 비율.")]
        [SerializeField] float fullFill = 1f;
        [Tooltip("체력 1일 때 막대 비율.")]
        [SerializeField] float halfFill = 0.5f;
        [Tooltip("체력 0일 때 남길 최소 막대 비율. 기획서 임시 수치.")]
        [SerializeField] float deadFill = 0.05f;

        [Header("Timings")]
        [Tooltip("집중 시작 후 막대가 흰색으로 바뀌기까지 걸리는 시간(초). 기획서 규칙.")]
        [SerializeField] float focusColorSwitchTime = 1f;
        [Tooltip("집중 취소 시 막대가 실제 체력 비율로 되돌아가는 시간(초). 기획서 임시 수치.")]
        [SerializeField] float cancelReturnDuration = 0.2f;
        [Tooltip("회복 완료 후 UI가 사라지는 페이드 지속 시간(초). 기획서 임시 수치.")]
        [SerializeField] float fadeOutDuration = 1.5f;

        Coroutine _activeCoroutine;
        // 페이드/축소 도중 피격이 들어오면 어떤 코루틴이 실행 중인지 알아야 정확한 스냅 로직을 적용할 수 있어
        // 상태 태그를 별도로 두 개(kind/isFocusing) 유지.
        // - _isFocusing: PlayerRecovery.OnFocusStarted~End 사이의 논리 상태
        // - _isFadingOut: 회복 완료 직후 1.5초 페이드 중임을 표시(재피격 시 alpha 강제 복구용)
        bool _isFocusing;
        bool _isFadingOut;

        void Awake()
        {
            // 참조 자동 검색 폴백을 두지 않는다 — 인스펙터에서 명시적으로 연결하는 프로젝트 방침.
            if (canvasGroup == null)
                Debug.LogWarning($"[HealthBarUI] {name}: canvasGroup 참조가 비어 있습니다.");
            if (fillImage == null)
                Debug.LogWarning($"[HealthBarUI] {name}: fillImage 참조가 비어 있습니다.");
            if (fillRect == null)
                Debug.LogWarning($"[HealthBarUI] {name}: fillRect 참조가 비어 있습니다.");
            if (fillTrackRect == null)
                Debug.LogWarning($"[HealthBarUI] {name}: fillTrackRect 참조가 비어 있습니다.");
        }

        void OnEnable()
        {
            if (healthSystem != null)
            {
                healthSystem.OnHealthChanged += HandleHealthChanged;
                healthSystem.OnDamaged += HandleDamaged;
            }
            else
            {
                Debug.LogWarning($"[HealthBarUI] {name}: healthSystem 참조가 비어 있습니다.");
            }

            if (playerRecovery != null)
            {
                playerRecovery.OnFocusStarted += HandleFocusStarted;
                playerRecovery.OnFocusProgress += HandleFocusProgress;
                playerRecovery.OnFocusCanceled += HandleFocusCanceled;
                playerRecovery.OnFocusCompleted += HandleFocusCompleted;
            }
            else
            {
                Debug.LogWarning($"[HealthBarUI] {name}: playerRecovery 참조가 비어 있습니다.");
            }

            if (stageManager != null)
                stageManager.OnStageFailed += HandleStageFailed;

            // 초기 표시 강제 동기화 — Awake/Start 순서 무의존이 되도록 여기서 즉시 반영.
            // healthSystem.Start()의 OnHealthChanged가 Awake 이후 발화되지만, 놓치는 케이스 대비.
            SyncToCurrentHealth();
        }

        void OnDisable()
        {
            if (healthSystem != null)
            {
                healthSystem.OnHealthChanged -= HandleHealthChanged;
                healthSystem.OnDamaged -= HandleDamaged;
            }

            if (playerRecovery != null)
            {
                playerRecovery.OnFocusStarted -= HandleFocusStarted;
                playerRecovery.OnFocusProgress -= HandleFocusProgress;
                playerRecovery.OnFocusCanceled -= HandleFocusCanceled;
                playerRecovery.OnFocusCompleted -= HandleFocusCompleted;
            }

            if (stageManager != null)
                stageManager.OnStageFailed -= HandleStageFailed;

            StopActiveCoroutine();
        }

        // ────────────────────────────────────────────────
        // 이벤트 핸들러
        // ────────────────────────────────────────────────

        // OnHealthChanged는 피격/회복/리셋 모두에서 발화된다.
        // - 피격: OnDamaged가 함께 오므로 표시 로직은 HandleDamaged에서도 방어적으로 수행
        // - 회복 완료: OnFocusCompleted가 먼저 오면서 페이드를 시작한 상태 → 이 이벤트로 인해
        //   페이드가 취소되지 않도록 _isFadingOut 플래그로 우회
        // - ResetHealth: 페이드 상태와 무관하게 즉시 Hidden 강제 (HandleStageFailed에서 함께 처리됨)
        void HandleHealthChanged(int current, int max)
        {
            // 회복 완료로 페이드 진행 중일 때 오는 (max, max)는 무시.
            // 페이드가 자연스럽게 끝나고 Hidden으로 도달할 것.
            if (_isFadingOut) return;

            // 집중 중이면 진행 표현이 fillImage를 제어 중이므로 개입하지 않는다 —
            // OnFocusCanceled/Completed 경로에서 정리된다.
            if (_isFocusing) return;

            SyncToCurrentHealth();
        }

        // 피격 이벤트: 페이드/축소 코루틴을 중단하고 즉시 표시 상태로 스냅한다.
        // OnHealthChanged와 순서가 뒤바뀌어도 결과가 같도록 여기서도 SyncToCurrentHealth를 호출.
        void HandleDamaged()
        {
            // 페이드아웃 중 재피격: 코루틴 중단, alpha=1 즉시 복구
            // 축소 애니 중 재피격: 코루틴 중단(새 목표는 SyncToCurrentHealth가 즉시 스냅으로 처리)
            StopActiveCoroutine();
            _isFadingOut = false;
            // 피격은 집중 중단 신호이기도 하지만, PlayerRecovery가 별도로 CancelFocus를 호출하므로
            // 여기서는 _isFocusing만 false로 리셋 (콜백 순서로 인해 이 시점에 이미 false일 수도 있음)
            _isFocusing = false;

            if (canvasGroup != null) canvasGroup.alpha = 1f;
            SyncToCurrentHealth();
        }

        void HandleFocusStarted()
        {
            _isFocusing = true;
            _isFadingOut = false;
            StopActiveCoroutine();

            // 페이드 도중 재집중 시작 시나리오는 실제로는 회복 완료 → 최대 체력이라 집중이 시작될 수 없음.
            // 하지만 상태 안전을 위해 alpha 강제 복구.
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            SetFillColor(damagedColor);
            SetFill(halfFill);
        }

        // progress는 0~1로 정규화된 값. 스펙: 선형 매핑 0.5 + 0.5*t.
        // 색 전환은 실제 시간 축(focusColorSwitchTime)에 따르며, PlayerRecovery.Progress로 역산.
        void HandleFocusProgress(float progress)
        {
            if (!_isFocusing) return;
            float clamped = Mathf.Clamp01(progress);

            SetFill(halfFill + (fullFill - halfFill) * clamped);

            // 색 전환 판정: PlayerRecovery에는 focusDuration이 인스펙터 값으로 있지만
            // UI 쪽에서 별도로 알 필요 없이, playerRecovery.Progress(경과 초)를 그대로 사용해 판정.
            // Progress는 초 단위 실제 경과값(PlayerRecovery의 _progress).
            if (playerRecovery != null && playerRecovery.Progress >= focusColorSwitchTime)
                SetFillColor(focusColor);
            else
                SetFillColor(damagedColor);
        }

        void HandleFocusCanceled()
        {
            if (!_isFocusing) return;
            _isFocusing = false;

            // 색은 즉시 빨강 스냅(결정 3). 막대 길이만 0.2초에 걸쳐 되돌린다.
            SetFillColor(damagedColor);

            StopActiveCoroutine();
            _activeCoroutine = StartCoroutine(CancelReturnRoutine());
        }

        void HandleFocusCompleted()
        {
            _isFocusing = false;

            // Heal(1)이 이 이벤트 직후 발화되면서 OnHealthChanged(2, 2)가 온다.
            // 그때 _isFadingOut=true이므로 무시되고 페이드가 유지된다(결정 7).
            _isFadingOut = true;
            SetFill(fullFill);
            SetFillColor(focusColor);

            StopActiveCoroutine();
            _activeCoroutine = StartCoroutine(FadeOutRoutine());
        }

        // 스테이지 실패 시: 진행 중 코루틴 모두 중단하고 Hidden으로 강제.
        // OnStageFailed는 HealthSystem.ResetHealth 이전에 발화될 수도 있고 이후일 수도 있어
        // (StageManager.PerformReset 순서상 OnStageFailed → ResetHealth), 여기서 상태를 확정적으로 리셋한다.
        void HandleStageFailed(int _)
        {
            StopActiveCoroutine();
            _isFocusing = false;
            _isFadingOut = false;
            SetFill(fullFill);
            SetFillColor(focusColor);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        // ────────────────────────────────────────────────
        // 헬퍼
        // ────────────────────────────────────────────────

        // 실제 체력에 맞는 막대 길이/색/표시 상태로 즉시 스냅.
        // 집중/페이드 중이 아닌 정적 상태에서 사용.
        void SyncToCurrentHealth()
        {
            if (healthSystem == null) return;

            int cur = healthSystem.CurrentHealth;
            int max = healthSystem.MaxHealth;

            if (cur >= max)
            {
                // 최대 체력 → 숨김.
                if (canvasGroup != null) canvasGroup.alpha = 0f;
                SetFill(fullFill);
                SetFillColor(focusColor);
                return;
            }

            // 표시 상태. alpha=1 강제(페이드 도중 재피격 등에서 복구).
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            if (cur <= 0)
                SetFill(deadFill);
            else
                SetFill(halfFill);
            SetFillColor(damagedColor);
        }

        // fill 값(0~1)을 RectTransform 너비로 반영.
        // fillRect는 anchor stretch(0,0)~(1,1), pivot(0, 0.5), offsetMin=(0,0) 상태를 전제로 한다.
        // 왜 fillAmount 대신 offsetMax를 조작하는가:
        // - 프로젝트가 별도 UI 스프라이트를 사용하지 않아 Image.sprite=null 상태다.
        //   이 상태에서 Image.type=Filled의 fillAmount는 정상 동작하지 않는다.
        // - RectTransform width 조작은 스프라이트 유무와 무관하게 동작해 스타일 교체(폴리싱)에도 영향이 없다.
        void SetFill(float value)
        {
            if (fillRect == null || fillTrackRect == null) return;
            float clamped = Mathf.Clamp01(value);
            float trackWidth = fillTrackRect.rect.width;
            // 오른쪽 끝을 부모 오른쪽 기준 (-1) * (trackWidth * (1-fill))만큼 당겨서 폭을 만든다.
            // offsetMin.x = 0 유지, offsetMax.x = -(1-fill) * trackWidth
            var offsetMin = fillRect.offsetMin;
            var offsetMax = fillRect.offsetMax;
            offsetMin.x = 0f;
            offsetMax.x = -(1f - clamped) * trackWidth;
            fillRect.offsetMin = offsetMin;
            fillRect.offsetMax = offsetMax;
        }

        void SetFillColor(Color c)
        {
            if (fillImage == null) return;
            fillImage.color = c;
        }

        void StopActiveCoroutine()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
        }

        // ────────────────────────────────────────────────
        // 코루틴
        // ────────────────────────────────────────────────

        // 집중 취소 시 막대가 실제 체력 비율(halfFill 또는 deadFill)로 되돌아가는 짧은 애니.
        // 색은 이미 damagedColor로 스냅된 상태.
        // 도중 재피격 시 HandleDamaged에서 StopActiveCoroutine + SyncToCurrentHealth로 즉시 스냅됨.
        IEnumerator CancelReturnRoutine()
        {
            if (fillImage == null || healthSystem == null)
            {
                _activeCoroutine = null;
                yield break;
            }

            // fillImage.fillAmount는 Image.type=Filled 전제이지만 이 프로젝트는 offsetMax로 폭을 조작하므로
            // 실제 현재 fill 비율은 SetFill()의 역수식으로 계산해야 한다.
            // SetFill()에서 offsetMax.x = -(1 - fill) * trackWidth 이므로
            // fill = 1 + offsetMax.x / trackWidth  (offsetMax.x는 0 이하)
            float trackWidth = fillTrackRect != null ? fillTrackRect.rect.width : 0f;
            float startFill = trackWidth > 0f ? 1f + fillRect.offsetMax.x / trackWidth : 0f;
            // 취소 시점의 실제 체력 비율. 체력 0이면 deadFill, 1이면 halfFill.
            float targetFill = healthSystem.CurrentHealth <= 0 ? deadFill : halfFill;

            float elapsed = 0f;
            while (elapsed < cancelReturnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / cancelReturnDuration);
                SetFill(Mathf.Lerp(startFill, targetFill, t));
                yield return null;
            }
            SetFill(targetFill);
            _activeCoroutine = null;
        }

        // 회복 완료 직후 1.5초에 걸쳐 alpha 1 → 0 페이드.
        // 도중 재피격 시 HandleDamaged에서 StopActiveCoroutine + alpha=1 강제 + SyncToCurrentHealth 로 즉시 스냅.
        IEnumerator FadeOutRoutine()
        {
            if (canvasGroup == null)
            {
                _isFadingOut = false;
                _activeCoroutine = null;
                yield break;
            }

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            _isFadingOut = false;
            _activeCoroutine = null;
        }
    }
}
