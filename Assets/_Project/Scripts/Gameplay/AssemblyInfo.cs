using System.Runtime.CompilerServices;

// 절차 생성 스프라이트(내부 클래스)의 규격 계약을 EditMode 테스트가 직접 고정할 수 있게 연다.
[assembly: InternalsVisibleTo("ProjectC.Tests.EditMode")]
