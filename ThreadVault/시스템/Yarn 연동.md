# Yarn 연동

Yarn 스크립트는 이벤트 내부의 진행 순서를 담당한다. 대사 출력과 연출을 순서대로 기술한다.
이벤트 실행 흐름은 [[이벤트 관리]]를 참고한다.

## 커맨드

대사 외 연출을 제어할 때 사용한다. 파라미터는 공백으로 구분한다.

| 커맨드 | 파라미터 | 동작 |
| --- | --- | --- |
| `<<SetBgAlpha value>>` | value: 0~1 | 배경 투명도를 변경한다 |
| `<<SetSpeakerAlpha value>>` | value: 0~1 | 화자 이미지 투명도를 변경한다 |
| `<<ChangeImage speaker emotion>>` | speaker, emotion: 이름 | 화자 이미지를 변경한다 |
| `<<Cutscene id>>` | id: 컷씬 ID | 컷씬 이미지를 표시한다. 플레이어가 입력하면 닫히고 대화를 재개한다 |
