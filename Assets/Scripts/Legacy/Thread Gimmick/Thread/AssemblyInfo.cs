using System.Runtime.CompilerServices;

// EditMode 테스트에서 Tick(float dt)처럼 internal로 노출된 테스트용 hook을 호출하기 위한 friend 선언.
// 런타임 코드에서는 internal 멤버를 유지해 외부 API 표면을 최소화한다.
[assembly: InternalsVisibleTo("ProjectT.Tests.Editor")]
