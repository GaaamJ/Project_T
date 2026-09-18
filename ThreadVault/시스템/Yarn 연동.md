# Yarn 연동

이 프로젝트에서 Yarn은 이벤트 내부의 진행 순서를 담당한다. 대사 출력, 연출 시작, 다른 게임 시스템 호출을 모두 Yarn Node 안에서 순서대로 기술한다.

EventRunner가 어떤 Yarn Node를 언제 실행할지 결정하고, Yarn은 Node 안의 내용을 실행한다. 이벤트 관리 흐름은 시스템/이벤트/이벤트 관리.md를 참고한다.

## Command

다른 게임 시스템에 행동을 요청할 때 사용한다. 파라미터는 공백으로 구분한다.

| 커맨드 | 파라미터 | 담당 컴포넌트 | 동작 |
| --- | --- | --- | --- |
| `<<SetBgAlpha value>>` | value: float (0~1) | DialogueBackgroundController | 배경 CanvasGroup alpha 변경 |
| `<<SetSpeakerAlpha value>>` | value: float (0~1) | DialogueSpeakerImageController | 화자 이미지 CanvasGroup alpha 변경 |
| `<<ChangeImage speaker emotion>>` | speaker, emotion: string | DialogueSpeakerImageController | 화자 이미지를 `Resources/Sprites/{speaker}_{emotion}` 경로에서 로드해 변경 |
| `<<Cutscene id>>` | id: string | CutscenePresenter | 컷씬 재생 요청. 컷씬 에셋 미구현으로 현재는 id를 로그로 출력 |

각 커맨드는 담당 컴포넌트가 활성화될 때 등록되고 비활성화될 때 해제된다. 씬에 컴포넌트가 없으면 해당 커맨드를 사용할 수 없다.

## Function

현재 구현된 Function 없음.

## Variable

현재 구현된 Variable 없음.
