# 인연 — 대화 UI

## 구조

- **말풍선** — 어떤 캐릭터가 말하는지 시각적으로 표현.
- **스탠딩 CG** — 캐릭터 Sprite로 감정 상태를 시각적으로 표현.
- UI 뷰와 게임플레이 뷰를 구분.

## 목표

1. 단기적으로 플레이어가 주인공들을 좋아하게 한다.
2. 장기적으로 플레이어가 주인공을 통해 깊은 여운을 느끼게 한다.

## 구현 참고

- `StandingCGChanger.cs`, `AlphaValueController.cs`: 캐릭터 스프라이트 비주얼 제어.
- 대사 관리는 Yarn Spinner(Yarn.Unity) 기반. `NodeManager.cs`에서 `DialogueRunner`를 직접 참조해 `StartDialogue()` 호출.

## 관련 문서

- [[인연/선택지]] — 플레이어가 주인공에게 말 거는 방식
