using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Lumera.ZRay.Samples.OrbitSalvage.Tests
{
    public sealed class OrbitSalvagePlayModeTests
    {
        private const string SCENE_PATH = "Assets/Scenes/OrbitSalvage.unity";
        private static readonly string[] HISTORY_KEYS = { "Lumera.ZRay.History.v1", "Lumera.ZRay.RecentCheats.v1" };

        private readonly Dictionary<string, string> _savedPrefs = new Dictionary<string, string>();
        private Scene _scene;
        private OrbitSalvageGame _game;
        private ZRayHost _host;
        private ZRayHost _previousHostOrNull;
        private Keyboard _keyboard;
        private Keyboard _previousKeyboardOrNull;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior _previousBackgroundBehavior;
        private float _previousCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator LoadSample()
        {
            _previousCaptureDeltaTime = Time.captureDeltaTime;
            _previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            _previousHostOrNull = ZRayHost.CurrentOrNull;
            _previousKeyboardOrNull = Keyboard.current;
            _savedPrefs.Clear();
            foreach (string key in HISTORY_KEYS)
            {
                if (PlayerPrefs.HasKey(key))
                {
                    _savedPrefs.Add(key, PlayerPrefs.GetString(key));
                }
            }

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SCENE_PATH);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0), "샘플 씬을 Build Settings에서 열 수 있어야 한다.");
            // 배치 모드와 Game View 포커스에 관계없이 가상 키보드의 이벤트를 게임에 전달한다.
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Time.captureDeltaTime = 1.0f / 30.0f;
            yield return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByBuildIndex(buildIndex);
            yield return null;

            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                _game = root.GetComponentInChildren<OrbitSalvageGame>();
                if (_game != null)
                {
                    break;
                }
            }

            Assert.That(_game, Is.Not.Null, "빌드에 포함된 씬에 게임 컴포넌트가 있어야 한다.");
            _host = ZRayHost.CurrentOrNull;
            Assert.That(_host, Is.Not.Null, "샘플 씬이 ZRay 호스트를 초기화해야 한다.");
        }

        [UnityTearDown]
        public IEnumerator UnloadSample()
        {
            if (_keyboard != null && _keyboard.added)
            {
                InputSystem.RemoveDevice(_keyboard);
            }

            if (_previousKeyboardOrNull != null && _previousKeyboardOrNull.added)
            {
                _previousKeyboardOrNull.MakeCurrent();
            }

            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }

            // 루트에 놓인 호스트는 DontDestroyOnLoad로 옮겨질 수 있다.
            ZRayHost hostOrNull = ZRayHost.CurrentOrNull;
            if (hostOrNull != null && hostOrNull != _previousHostOrNull)
            {
                Object.Destroy(hostOrNull.gameObject);
                yield return null;
            }

            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = _previousBackgroundBehavior;
            Time.captureDeltaTime = _previousCaptureDeltaTime;
            foreach (string key in HISTORY_KEYS)
            {
                if (_savedPrefs.TryGetValue(key, out string value))
                {
                    PlayerPrefs.SetString(key, value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(key);
                }
            }

            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator RegisteredCheatsChangeTheRunningRound()
        {
            string[] ids = { "salvage.speed", "salvage.freeze", "salvage.time.add", "salvage.restart" };
            foreach (string id in ids)
            {
                CheatDefinition definition = _host.Registry.FindOrNull(id);
                Assert.That(definition, Is.Not.Null, id);
                Assert.That(definition.TargetOrNull, Is.SameAs(_game), id);
            }

            Execute("salvage.speed 8");
            Assert.That(_host.Executor.Execute("salvage.speed 13").IsSuccess, Is.False);
            Assert.That(_host.Registry.FindOrNull("salvage.speed").ReadValue(), Is.EqualTo(8.0f));
            Execute("salvage.freeze true");
            Execute("salvage.restart");
            float initialSeconds = _game.RemainingSeconds;
            Execute("salvage.time.add 7");
            yield return null;
            Assert.That(_game.RemainingSeconds, Is.EqualTo(initialSeconds + 7.0f).Within(0.001f));

            Execute("salvage.restart");
            Assert.That(_game.RemainingSeconds, Is.EqualTo(initialSeconds));
            Assert.That(_host.Registry.FindOrNull("salvage.speed").ReadValue(), Is.EqualTo(8.0f));
            Execute("salvage.freeze false");
            yield return null;
            Assert.That(_game.RemainingSeconds, Is.LessThan(initialSeconds));
        }

        [UnityTest]
        public IEnumerator ConsoleShortcutOpensTheConsoleWithTheKoreanFont()
        {
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null, "게임과 오버레이를 그릴 렌더 파이프라인이 필요하다.");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Backquote));
            yield return null;
            yield return null;
            ZRayCanvas canvas = _host.GetComponentInChildren<ZRayCanvas>();
            Assert.That(canvas.Current, Is.EqualTo(EDebuggerOverlay.Console));
            TMP_InputField input = canvas.Console.GetComponentInChildren<TMP_InputField>();
            Assert.That(input, Is.Not.Null);
            TMP_FontAsset font = input.fontAsset;
            Assert.That(font.sourceFontFile.name, Is.EqualTo("Galmuri11"));
            Assert.That(font.material.shader.name, Is.EqualTo("TextMeshPro/Default/Mobile/Bitmap"));
            Assert.That(font.HasCharacter('한', searchFallbacks: false, tryAddCharacter: true), Is.True);
        }

        [UnityTest]
        public IEnumerator FlightRespectsTheMenuAndRoundsCanBeWonLostAndRestarted()
        {
            Execute("salvage.speed 12");
            Execute("salvage.freeze true");
            Vector2 start = _game.ShipPosition;
            Graphic ship = FindPlayfieldGraphic("Ship");
            Graphic core = FindPlayfieldGraphic("Core");
            Vector2 shipAnchor = ship.rectTransform.anchorMin;
            Vector2 coreAnchor = core.rectTransform.anchorMin;

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.F1));
            yield return null;
            yield return null;
            Assert.That(_host.IsBlockingGameInput, Is.True, $"F1 상태 {_keyboard.f1Key.isPressed}, 바인딩 {_host.MenuKeyLabel}");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.RightArrow));
            yield return null;
            yield return null;
            Assert.That(_game.ShipPosition, Is.EqualTo(start), "치트 메뉴가 열려 있으면 게임이 이동 입력을 무시한다.");

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape, Key.RightArrow));
            yield return null;
            yield return null;
            Assert.That(_host.IsBlockingGameInput, Is.False);
            Assert.That(_game.ShipPosition.x, Is.GreaterThan(start.x));
            Assert.That(ship.rectTransform.anchorMin.x, Is.GreaterThan(shipAnchor.x), "화면의 우주선도 오른쪽으로 이동해야 한다.");

            Vector2 firstCore = _game.CorePosition;
            yield return FlyToNextCore();
            Assert.That(_game.Collected, Is.EqualTo(1), "입력으로 코어까지 비행하면 수집 수가 증가해야 한다.");
            Assert.That(_game.CorePosition, Is.Not.EqualTo(firstCore));
            Assert.That(core.rectTransform.anchorMin, Is.Not.EqualTo(coreAnchor), "화면에 다음 코어 위치가 반영되어야 한다.");

            for (int index = 1; index < OrbitSalvageGame.TARGET_COUNT; index++)
            {
                yield return FlyToNextCore();
            }

            Assert.That(_game.IsWon, Is.True);
            Assert.That(_host.Executor.Execute("salvage.time.add 1").IsSuccess, Is.False);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.R));
            yield return null;
            yield return null;
            Assert.That(_game.Collected, Is.Zero);
            Assert.That(_game.ShipPosition, Is.EqualTo(start));
            Assert.That(_game.CorePosition, Is.EqualTo(firstCore));
            Assert.That(_game.IsWon, Is.False);

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            Execute("salvage.freeze false");
            Time.captureDeltaTime = 0.5f;
            for (int frame = 0; frame < 180 && !_game.IsLost; frame++)
            {
                yield return null;
            }

            Assert.That(_game.IsLost, Is.True);
            Assert.That(_game.IsWon, Is.False);
            Execute("salvage.restart");
            Assert.That(_game.IsLost, Is.False);
            Assert.That(_game.RemainingSeconds, Is.GreaterThan(0.0f));
        }

        private IEnumerator FlyToNextCore()
        {
            Vector2 target = _game.CorePosition;
            int collected = _game.Collected;
            for (int frame = 0; frame < 100 && _game.Collected == collected; frame++)
            {
                Vector2 delta = target - _game.ShipPosition;
                Key horizontal = delta.x >= 0 ? Key.RightArrow : Key.LeftArrow;
                Key vertical = delta.y >= 0 ? Key.UpArrow : Key.DownArrow;
                Key direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? horizontal : vertical;
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(direction));
                yield return null;
            }

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            Assert.That(_game.Collected, Is.EqualTo(collected + 1), $"코어 {target} 도착, 현재 {_game.ShipPosition}");
        }

        private void Execute(string line)
        {
            ExecutionResult result = _host.Executor.Execute(line);
            Assert.That(result.IsSuccess, Is.True, $"{line} — {result.Message}");
        }

        private Graphic FindPlayfieldGraphic(string name)
        {
            foreach (Graphic graphic in _game.GetComponentsInChildren<Graphic>())
            {
                if (graphic.name == name && graphic.transform.parent.name == "Playfield" && graphic.isActiveAndEnabled)
                {
                    return graphic;
                }
            }

            throw new AssertionException($"플레이 필드에서 활성 그래픽 {name}을 찾지 못했다.");
        }
    }
}
