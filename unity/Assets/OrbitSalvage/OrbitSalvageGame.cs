using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumera.ZRay.Samples.OrbitSalvage
{
    [DisallowMultipleComponent]
    public sealed class OrbitSalvageGame : MonoBehaviour
    {
        public const int TARGET_COUNT = 5;

        private const float ROUND_SECONDS = 45.0f;
        private const float HALF_WIDTH = 8.0f;
        private const float HALF_HEIGHT = 4.5f;
        private const float PICKUP_RADIUS = 0.5f;
        private static readonly Vector2 START_POSITION = new Vector2(-3.9f, -1.35f);
        private static readonly Vector2[] CORE_POSITIONS =
        {
            new Vector2(2.5f, 1.25f),
            new Vector2(-5.5f, 2.6f),
            new Vector2(5.7f, -2.5f),
            new Vector2(-4.3f, -2.8f),
            new Vector2(0.8f, 3.0f)
        };

#if UNITY_EDITOR || LUMERA_ZRAY
        private ZRayHost _hostOrNull;
        private System.IDisposable _registrationOrNull;
#endif

        public Vector2 ShipPosition { get; private set; }
        public Vector2 CorePosition => CORE_POSITIONS[Mathf.Min(Collected, TARGET_COUNT - 1)];
        public Vector2 Heading { get; private set; }
        public int Collected { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsWon => Collected == TARGET_COUNT;
        public bool IsLost => !IsWon && RemainingSeconds <= 0.0f;

#if UNITY_EDITOR || LUMERA_ZRAY
        [CheatProperty("salvage.speed", Name = "구조선 속도", Category = "궤도 구조", Description = "초당 이동 거리. 라운드를 다시 시작해도 유지한다.", Min = 0.5, Max = 12, Step = 0.5)]
#endif
        public float Speed { get; private set; } = 3.5f;

#if UNITY_EDITOR || LUMERA_ZRAY
        [CheatProperty("salvage.freeze", Name = "제한시간 정지", Category = "궤도 구조", Description = "제한시간만 멈춘다. 재시작 후에도 비행과 수집은 계속할 수 있다.")]
#endif
        public bool IsTimerFrozen { get; private set; }

        private void Awake()
        {
            RestartRound();
        }

#if UNITY_EDITOR || LUMERA_ZRAY
        private void OnEnable()
        {
            RefreshRegistration();
        }

        private void OnDisable()
        {
            _registrationOrNull?.Dispose();
            _registrationOrNull = null;
            _hostOrNull = null;
        }

        private void RefreshRegistration()
        {
            ZRayHost currentOrNull = ZRayHost.CurrentOrNull;
            if (ReferenceEquals(currentOrNull, _hostOrNull))
            {
                return;
            }

            _registrationOrNull?.Dispose();
            _hostOrNull = currentOrNull;
            _registrationOrNull = currentOrNull != null ? currentOrNull.Register(this) : null;
        }
#endif

        private void Update()
        {
#if UNITY_EDITOR || LUMERA_ZRAY
            RefreshRegistration();
            if (_hostOrNull != null && _hostOrNull.IsBlockingGameInput)
            {
                return;
            }
#endif
            Keyboard keyboardOrNull = Keyboard.current;
            if (keyboardOrNull != null && keyboardOrNull.rKey.wasPressedThisFrame)
            {
                RestartRound();
                return;
            }

            if (IsWon || IsLost)
            {
                return;
            }

            float stepSeconds = IsTimerFrozen ? Time.deltaTime : Mathf.Min(Time.deltaTime, RemainingSeconds);
            Vector2 direction = keyboardOrNull != null ? ReadDirection(keyboardOrNull) : Vector2.zero;
            Vector2 previousPosition = ShipPosition;
            if (direction != Vector2.zero)
            {
                Heading = direction.normalized;
                Vector2 nextPosition = ShipPosition + Heading * (Speed * stepSeconds);
                ShipPosition = new Vector2(Mathf.Clamp(nextPosition.x, -HALF_WIDTH, HALF_WIDTH), Mathf.Clamp(nextPosition.y, -HALF_HEIGHT, HALF_HEIGHT));
            }

            // 프레임 사이에 코어를 지나친 경우도 수집한다.
            Vector2 travel = ShipPosition - previousPosition;
            float fraction = travel.sqrMagnitude > 0.0f ? Mathf.Clamp01(Vector2.Dot(CorePosition - previousPosition, travel) / travel.sqrMagnitude) : 0.0f;
            Vector2 nearest = previousPosition + travel * fraction;
            if ((CorePosition - nearest).sqrMagnitude <= PICKUP_RADIUS * PICKUP_RADIUS)
            {
                Collected++;
            }

            if (!IsTimerFrozen)
            {
                RemainingSeconds -= stepSeconds;
            }
        }

        private static Vector2 ReadDirection(Keyboard keyboard)
        {
            Vector2 direction = Vector2.zero;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                direction.x++;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                direction.x--;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                direction.y++;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                direction.y--;
            }

            return direction;
        }

        // 라운드의 시간과 진행도만 초기화한다. 속도와 시간 정지 설정은 이 게임 인스턴스의 수명 동안 유지한다.
#if UNITY_EDITOR || LUMERA_ZRAY
        [Cheat("salvage.restart", Name = "라운드 재시작", Category = "궤도 구조", Description = "속도와 시간 정지 설정을 유지한 채 처음부터 시작한다.")]
#endif
        public void RestartRound()
        {
            Collected = 0;
            RemainingSeconds = ROUND_SECONDS;
            ShipPosition = START_POSITION;
            Heading = (CorePosition - ShipPosition).normalized;
        }

#if UNITY_EDITOR || LUMERA_ZRAY
        [Cheat("salvage.time.add", Name = "제한시간 추가", Category = "궤도 구조", Description = "진행 중인 라운드에 시간을 더한다.")]
        private CheatResult AddTime([Arg("추가할 초", Min = 1, Max = 300)] int seconds = 5)
        {
            if (IsWon || IsLost)
            {
                return CheatResult.Fail("라운드를 다시 시작한 뒤 시간을 추가하세요.");
            }

            RemainingSeconds += seconds;
            return CheatResult.Ok($"제한시간을 {seconds}초 추가했습니다.");
        }
#endif
    }
}
