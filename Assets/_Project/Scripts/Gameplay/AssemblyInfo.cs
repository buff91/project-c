using System.Runtime.CompilerServices;

// 내부 Gameplay 계약을 에디터 회귀가 공개 API로 승격하지 않고 직접 고정할 수 있게 연다.
[assembly: InternalsVisibleTo("ProjectC.Tests.EditMode")]
[assembly: InternalsVisibleTo("ProjectC.Tests.PlayMode")]
