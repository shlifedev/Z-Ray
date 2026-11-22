# Lumera.ZRay 에이전트 지침

이 지침은 저장소 전체에서 Codex와 Claude에 공통으로 적용된다.

## 이 저장소

- 개발자 콘솔과 치트 메뉴 Unity 패키지(`com.lumera.zray`)의 원본 저장소다. 패키지 루트는 `src/`다.
- 쓰는 프로젝트는 `src/`를 로컬 패키지(`file:<경로>/src`)나 git URL(`...git?path=src`)로 참조한다. 로컬 패키지로 참조한 프로젝트에서 소스를 고쳐도 변경은 이 저장소에 커밋한다.
- ZRay는 게임을 모른다. `src/`에 특정 게임의 이름이나 코드를 넣지 않는다.

## 언어

- 주석, 문서, AI 지침 문서, 작업 보고는 한국어로 작성한다.
- 클래스·메서드·변수 등 코드 식별자는 영어로 작성한다. 엔진·라이브러리 API 이름과 기술 용어는 원문을 그대로 쓴다.

## C# 코딩 표준

[포프 김의 C# 코딩 표준](https://docs.popekim.com/ko/coding-standards/csharp)을 따르고, 아래 명명 규칙이 우선한다.

- 메서드(로컬 함수 포함), 클래스, 네임스페이스는 PascalCase. 지역 변수와 매개변수는 camelCase.
- private 필드는 `_camelCase`. `m`, `mb` 접두사는 쓰지 않는다. bool 지역 변수와 매개변수에는 `b` 접두사, bool 속성에는 `Is`, `Has`, `Can`, `Should` 접두사.
- 인터페이스는 `I`, 열거형은 `E`, 일반 구조체는 `S` 접두사(`readonly struct`는 제외).
- 상수와 상수 역할의 `static readonly`는 `UPPER_SNAKE_CASE`.
- 들여쓰기 공백 4칸, 여는 중괄호는 새 줄. 제어문 본문이 한 줄이어도 중괄호를 쓴다.
- 명시적 타입을 우선하고, `new` 뒤에 타입을 명시한다.
- `null`을 받는 매개변수와 `null`을 반환하는 메서드에는 `OrNull` 접미사.
- ZRay는 의존성 없는 라이브러리다. ZLinq·ZString 같은 외부 라이브러리를 쓰지 않는다.

## 구조와 Unity

- `src/Runtime/Core`(`Lumera.ZRay`)는 UnityEngine을 쓰지 않는 C# 9, .NET Standard 2.1 코드다. `dotnet/Lumera.ZRay`이 같은 소스를 컴파일해 검사한다.
- csproj는 `dotnet/`에만 둔다. `src/` 안에 두면 `bin/`·`obj/`의 `.cs`를 Unity가 함께 컴파일한다.
- 세 asmdef는 `defineConstraints`로 에디터와 Development Build(또는 `LUMERA_ZRAY` 심볼)에서만 컴파일된다.
- .NET 명령: `dotnet build dotnet/Lumera.ZRay.sln -c Release`, `dotnet run --project dotnet/tests/Lumera.ZRay.Checks -c Release`.
- `src/` 안에 파일을 더하거나 지우면 Unity가 만든 `.meta`를 같은 커밋에 넣는다.

## 도메인 리로드 없는 플레이 모드

ZRay를 쓰는 프로젝트는 도메인 리로드를 끌 수 있다. 바뀌는 상태를 정적 필드에 두지 않고, 정적 이벤트와 Unity 정적 콜백 구독은 `OnDisable`이나 `OnDestroy`에서 해제한다.

## 검증

변경은 빌드와 검사로 확인하고, 검증한 것과 못 한 것을 정확히 보고한다.
