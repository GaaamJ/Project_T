# UI

## 버프 및 상호작용 UI

플레이어에게 시각적인 정보를 전달하는 UI.
이번에는 실 버프, 상호작용 Hold 시 진척도가 차오르는 UI만을 제작한다.

플레이어 - 우미아의 관계를 구축하는 UI 외에는, 필수적인 정보만 최소한(버프, 디버프 등 메커니즘에 필요한 UI 2개)으로 전달한다.
플레이어가 우미아와 대화하며, 관계를 구축하는 것을 원하는 것이지, UI를 들여다 보며 게임을 하길 바라는 것은 아니기 때문이다.

## 동작

### 2a. 버프 표시

플레이어가 지금 가지고 있는 버프를 표시하기 위해,
플레이어가 실 버프를 가지고 있을 경우, 플레이어의 실 버프가 {ThreadBuffName}: {RemainedTime}으로 UI 좌중단에 표시된다.

Font는 Project에 Import되어 있는 Galmuri11을 사용.
각각 임시로, 붉은 실 / 푸른 실 / 금빛 실.

RemainedTime에서 소수점은 날린다.

2a1. Ref. Destiny raid의 버프 UI

### 2b. Hold 게이지

플레이어가 제대로 Interact하고 있다는 걸 보여주기 위해,
플레이어가 좌표에 실 버프를 Hold해 묶을 경우, World Space Canvas로 좌표 오브젝트 위에 Hold 게이지를 UI로 표시한다.
임시로 slider를 사용.

### 2c. Canvas 구조

현재 Canvas가 DialogueCanvas, CameraRenderCanvas 2가지가 있는데, 
Buff는 CameraRenderCanvas에서 관리. (이름 역시 UICanvas로 변경)
Interact는 별도의 World Space Canvas를 만들어서 관리.

## 조건

### 3a.

2a에서, 플레이어가 버프를 습득하고, 버프를 소비하기까지 UI 표시.

### 3b.

2b에서, 플레이어가 Interact 가능한 오브젝트 근처에서 Interact를 실행하고, 종료할 때까지 UI 표시.

## 목표

### 4a.

1. 임시 레이아웃을 만들고, 버프를 습득했을 때 2a가 표시되는지 체크.
2 - 1. 이후 버프가 시간 만료되었을 때 2a가 꺼지는지 체크.
2 - 2. 다시 1번 진행 후, 좌표에 버프를 반납했을 때 2a가 꺼지는지 체크.
3. 이후 버프를 재습득했을 때 2a가 갱신되는지 체크.
최소 2번 루프 진행.

### 4b.

임시 레이아웃을 만들고, 4a의 2 - 2에서, 좌표에 Interact를 시작했을 때 Hold 게이지가 표시되는지 체크.

## 보류

Dialogue UI는 다루지 않음.

---

## 대화 시스템

기존 DialogueSystem을 활용한다.

### 구현된 기능

- **StandingCGChanger**: Yarn 커맨드 `ChangeImage(speaker, emotion)` — `Resources/Sprites/{speaker}_{emotion}` 스프라이트로 캐릭터 이미지 교체.
- **AlphaValueController**: 대화 시작 시 CanvasGroup alpha → 1, 종료 시 → 0. Yarn 커맨드 `SetAlpha(float)` 지원.
- **NumberKeyOptionSelector**: 숫자키 1~4로 선택지 하이라이트 이동, 스페이스로 제출.
- **AskHelpTrigger**: `` ` `` 키 입력 시 AskForHelp 노드 트리거.

---

## 구현 참고

- BuffPanelUI는 UICanvas(구 CameraRenderCanvas) 하위, Update 폴링 방식. 버프 없을 때 패널 자체를 숨김.
- HoldGaugeUI는 각 Coordinate GameObject에 직접 부착, World Space Canvas 하위 Slider 연결.
- Canvas 체계: DialogueCanvas / UICanvas 2개 운용.
