# Lumera DebuggerKit

Unity 게임용 개발자 콘솔과 치트 메뉴 패키지다. 메서드에 `[Cheat]`를 붙이면 콘솔 명령, 자동완성, 인자 힌트, 도움말, 메뉴의 행이 모두 그 선언 하나에서 만들어진다. 에디터와 Development Build에서만 컴파일된다.

쓰는 법(씬에 넣기, 치트 선언, 키 조작)은 패키지 문서 [`src/README.md`](src/README.md)에 있다.

## 폴더

| 폴더 | 내용 |
| --- | --- |
| `src/` | Unity 패키지 루트(`com.lumera.debuggerkit`). `Runtime/Core`는 UnityEngine을 쓰지 않는 C# 9 코드, `Runtime/Unity`는 화면(uGUI, TextMeshPro, Input System), `Editor`는 에디터 창과 프리팹 빌더 |
| `dotnet/` | `src/Runtime/Core`를 .NET Standard 2.1로 컴파일하는 프로젝트와 Core 검사 |

## Unity 프로젝트에 넣기

`Packages/manifest.json`의 `dependencies`에 더한다.

```json
"com.lumera.debuggerkit": "https://github.com/<owner>/Lumera.DebuggerKit.git?path=src#v0.1.0"
```

로컬에 받아 둔 저장소를 쓰려면 `"file:<저장소 경로>/src"`로 적는다. 의존 패키지는 `com.unity.ugui`, `com.unity.inputsystem`이고, 프로젝트의 Active Input Handling이 Input System이어야 한다.

## .NET에서 쓰기

Core(선언 특성, 파서, 자동완성, 실행)는 UnityEngine 없이 돈다. .NET 프로젝트에서 `dotnet/Lumera.DebuggerKit/Lumera.DebuggerKit.csproj`를 참조한다.

```xml
<ProjectReference Include="<저장소 경로>/dotnet/Lumera.DebuggerKit/Lumera.DebuggerKit.csproj" />
```

## 빌드와 검사

```bash
dotnet build dotnet/Lumera.DebuggerKit.sln -c Release
dotnet run --project dotnet/tests/Lumera.DebuggerKit.Checks -c Release
```

## 라이선스

킷 글꼴 갈무리11은 SIL OFL 1.1이다(`src/Runtime/Unity/Fonts/Galmuri-OFL.txt`).
