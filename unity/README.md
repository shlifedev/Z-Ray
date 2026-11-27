# Orbit Salvage

ZRay를 직접 조작해 보는 Unity 샘플이다. 구조선을 조종해 45초 안에 호박색 코어 5개를 회수한다. 코어를 모으면 화면에 다음 코어가 나타난다.

## 실행

1. Unity Hub에서 **Add → Add project from disk**로 이 `unity/` 폴더를 선택한다.
2. **Unity 7000.0.0a20**으로 연다. Unity 7을 요구하는 패키지와 함께 검증한 버전이다.
3. [OrbitSalvage 씬](Assets/Scenes/OrbitSalvage.unity)을 열고 **Play**를 누른 뒤 Game 뷰를 클릭한다.

| 키 | 동작 |
| --- | --- |
| WASD 또는 방향키 | 구조선 이동 |
| R | 라운드 재시작 |
| F1 | ZRay 치트 메뉴 열기·닫기 |
| 백쿼트 (Esc 아래 키) | ZRay 콘솔 열기·닫기 |
| Esc | 현재 입력을 닫거나 오버레이로부터 게임으로 돌아가기 |

오버레이가 열려 있는 동안 비행과 제한시간이 멈춘다. macOS에서 F1이 화면 밝기를 조절하면 **Fn + F1**을 사용한다.

## 치트 확인

| 콘솔에 입력할 명령 | 확인할 결과 |
| --- | --- |
| `salvage.speed 8` | 오버레이를 닫으면 구조선의 속도가 초당 8로 바뀐다. |
| `salvage.freeze true` | 게임으로 돌아가도 타이머가 멈춘다. 이동과 코어 수집은 계속된다. `false`로 되돌린다. |
| `salvage.time.add 10` | 진행 중인 라운드의 타이머에 10초가 더해진다. |
| `salvage.restart` | 시작 위치, 코어 수와 45초 타이머가 복구된다. 속도와 타이머 정지 설정은 유지된다. |

치트 메뉴의 **궤도 구조** 분류에서도 같은 동작을 확인할 수 있다. 콘솔에 `salvage.`를 입력하면 자동완성 후보가 보이고, `salvage.speed`만 입력하면 현재 값을 읽는다.

## Play Mode 검사와 빌드

**Window → General → Test Runner → PlayMode → Run All**로 샘플 씬의 통합 검사를 실행한다. 저장소 루트에서 Unity CLI로 실행할 수도 있다.

```sh
unity test ./unity --mode PlayMode --output ./unity/Logs/playmode-results.xml
```

[Packages/manifest.json](Packages/manifest.json)의 `file:../../src`는 `unity/Packages/`에서 저장소의 패키지 원본을 가리킨다. 패키지 소스를 고치면 다시 임포트된다. 샘플의 Standalone 플레이어에는 `LUMERA_ZRAY`가 설정되어 있어 **File → Build Profiles → Build And Run**으로 빌드한 뒤에도 같은 치트를 사용할 수 있다.
