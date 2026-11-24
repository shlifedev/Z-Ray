# Lumera ZRay

An in-game developer console and cheat menu for Unity.

## Unity 7 플레이어 빌드

에디터에서는 설치하면 바로 쓸 수 있다. Unity 7에서 **Development Build**는 플레이어의 종류를 정하며, C# 컴파일 심볼은 별도로 설정한다. ZRay를 넣을 개발용 프로필을 **File > Build Profiles**에서 선택한 뒤 **Scripting Defines**에 `LUMERA_ZRAY`를 추가한다. ZRay가 필요한 프로필에만 이 심볼을 추가한다.

스크립트로 빌드할 때는 `BuildPipeline.BuildPlayer`를 호출하기 전에 개발 빌드에 심볼을 추가한다. 기존 빌드 스크립트에서 설정한 다른 심볼도 함께 넘긴다.

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;

private static BuildReport BuildPlayer(BuildPlayerOptions options)
{
    if ((options.options & BuildOptions.Development) != 0)
    {
        List<string> defines = new List<string>(options.extraScriptingDefines ?? Array.Empty<string>());
        if (!defines.Contains("LUMERA_ZRAY"))
        {
            defines.Add("LUMERA_ZRAY");
        }

        options.extraScriptingDefines = defines.ToArray();
    }

    return BuildPipeline.BuildPlayer(options);
}
```

게임 코드의 ZRay 선언과 호출도 `#if UNITY_EDITOR || LUMERA_ZRAY`로 감싼다. 치트 전용 asmdef로 분리한 경우에는 같은 식을 **Define Constraints**에 넣을 수 있다.

## 커스텀 GUI에서 치트 사용하기

씬에 `ZRayHost`를 두면 정적 치트를 자동으로 등록한다. 인스턴스 치트는 `host.Register(target)`으로 추가한다. 기본 콘솔과 메뉴도 함께 쓰려면 호스트의 **Canvas Prefab**에 `ZRayCanvas` 프리팹을 연결한다. 커스텀 GUI만 쓸 때는 비워 둔다.

뷰는 호스트가 초기화된 뒤 `host.Registry.Definitions`에서 치트 이름, 분류, 설명과 파라미터를 읽는다. 예를 들어 UI Toolkit 패널을 열 때 다음 메서드를 호출하면 메뉴에 공개한 쓰기 가능한 `bool` 프로퍼티마다 토글이 하나씩 생긴다.

```csharp
#if UNITY_EDITOR || LUMERA_ZRAY
using Lumera.ZRay;
using UnityEngine.UIElements;

// root는 이 패널의 토글 목록 전용 컨테이너다.
private void BindToggles(VisualElement root, ZRayHost host)
{
    root.Clear();
    foreach (CheatDefinition cheat in host.Registry.Definitions)
    {
        if (!cheat.IsShownInMenu || cheat.Kind != ECheatKind.Value
            || !cheat.CanWrite || cheat.Parameters[0].Type != typeof(bool))
        {
            continue;
        }

        Toggle toggle = new Toggle(cheat.Name);
        toggle.SetValueWithoutNotify((bool)cheat.ReadValue());
        Label status = new Label();
        toggle.RegisterValueChangedCallback(change =>
        {
            ExecutionResult result = host.ExecuteFromMenu(cheat, new[] { change.newValue ? "true" : "false" });
            toggle.SetValueWithoutNotify((bool)cheat.ReadValue());
            status.text = result.Message;
        });
        root.Add(toggle);
        root.Add(status);
    }
}
#endif
```

메서드 치트도 같은 방식으로 호출한다. `cheat.Parameters`의 선언 순서대로 입력값을 문자열로 넘기면 호스트가 타입과 범위를 검증한다. 인자가 없으면 `System.Array.Empty<string>()`을 넘기고, 뒤쪽 선택 인자를 생략하면 선언된 기본값이 쓰인다.

패널을 연 채로 치트가 등록되거나 해제될 수 있다면 `host.Registry.Version`이 바뀔 때 목록을 갱신한다. 커스텀 패널의 열기·닫기와 게임 입력 차단은 뷰가 관리한다. `host.IsBlockingGameInput`은 기본 콘솔과 메뉴의 상태를 나타낸다.

## License

The bundled Galmuri11 font is licensed under the [SIL Open Font License 1.1](Runtime/Unity/Fonts/Galmuri-OFL.txt).
