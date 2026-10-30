# BatterySimulation_Project

배터리 장치를 **Container → Pack → Module → Cell** 계층으로 만들고, 만든 구성에 **충전 / 방전 / 열감지** 시뮬레이션 연출을 입혀 보는 Unity 프로젝트.

- Unity `6000.4.8f1`, Universal Render Pipeline(URP 17.4)
- 주요 패키지: Addressables 2.9.1, Input System 1.19, Cinemachine 3.1.7, Test Framework 1.6
- 외부 에셋: DOTween(`Assets/Plugins/Demigiant`), Chart And Graph(`Assets/Chart And Graph`), Pretendard 폰트

## 1. 핵심 기능

| 기능 | 내용 |
|---|---|
| 장치 생성 | Container, Pack, Module, Cell을 계층에 맞게 생성한다. 부모 안에서 겹치지 않는 자리에 자동 배치하고, 자리가 없으면 생성하지 않고 안내 문구를 띄운다. |
| 크기 / 간격 조절 | 선택한 장치의 크기와 자식 사이 간격을 바꾼다. 좌측 바닥 뒤쪽 모서리를 고정한 채 늘어난다. 새로 만든 항목에도 부모에 저장된 간격을 다시 적용한다. |
| 트리 / 선택 | 장치 구조를 트리로 보여 준다. 한 번에 1개만 선택되고, 선택한 항목과 그 자식만 화면에 보인다. 카메라는 선택 항목을 따라간다. |
| 축 그래프 | 선택한 항목 주변에 x / y / z 축과 눈금, 치수를 그려 크기를 읽을 수 있게 한다(`DeviceAxisGraphManager`). |
| 저장 / 불러오기 | 구성 전체를 JSON으로 저장하고 다시 불러온다. 예전 형식의 파일도 현재 형식으로 변환해 연다. |
| 시뮬레이션 | 장치마다 시뮬레이션 작업(종류 + 지속 시간)을 순서대로 쌓아 실행하고, 작업이 끝날 때마다 결과를 목록에 쌓는다. |

### 시뮬레이션 종류

| 종류 | 연출 | 결과에 표시되는 값 |
|---|---|---|
| `Charge` | Pack 몸체에 충전 진행도와 전류 흐름 셰이더(`ElectricChargeFlow`)를 입히고, Module / Cell을 무작위로 붉게 점멸시킨다. | 수량, 이상 수량, 안정성 |
| `Discharge` | 충전과 같은 방식의 연출. | 수량, 이상 수량, 안정성 |
| `ThermalDetection` | Pack 몸체를 열화상처럼 표현(`PackThermal` 셰이더)한다. Module / Cell 중 열원을 무작위로 골라 하나씩 나타나 자라게 한다. | 최고 / 최저 온도, 과열 수량 |

- 안정성(%) = (전체 수량 − 이상 수량) / 전체 수량 × 100. 전체 수량이 0이면 100이다.
- 온도는 열원 세기(0~1)를 25~80℃로 환산한 프로토타입 값이며 실제 측정값이 아니다. 70℃ 이상인 열원을 과열로 센다.

## 2. 실행 흐름

```
BootstrapScene
 └ BootstrapSceneManager (SequenceManager로 단계를 순서대로 실행)
     1. AddressableAssetManager 초기화
     2. 로딩바 프리팹 생성 후 표시
     3. SceneLoadManager 초기화 (SceneDataModelSO / AddressableAssetModelSO 로드)
     4. firstSceneTag("MainScene")로 씬 전환 (로딩바 유지, Bootstrap 씬은 언로드)
        ↓
MainScene
 └ MainSceneManager
     - Addressable에서 UI_MainScene 프리팹을 꺼내 인스턴스화하고 MainScene으로 옮긴다
     - 프리팹 안의 뷰를 찾아 연결하고 DeviceSelection을 공유한다
        ├ Editor 탭    : 장치 생성 / 크기·간격 조절 / 이름 변경 / 삭제 / 저장 / 불러오기
        └ Simulation 탭: 장치별 작업 목록 편집 → 실행 → 단계별 결과 확인
```

탭을 바꾸면 만들어 둔 오브젝트, 두 탭의 트리, 선택을 모두 처음 상태로 되돌린다.

### 시뮬레이션 실행 흐름

```
UI_SimulationControl
 └ SimulationRunner      작업(stepList)을 복사본 Queue로 순서대로 실행, 작업마다 duringTime이 지나면 다음으로
     │                   이벤트: StepBegan / StepProgressed / StepEnded / Finished
     ▼
   SimulationEffectPlayer   이벤트에 맞춰 종류별 연출 재생·정리, 작업이 끝나면 SimulationStepResult 생성
     ├ SimulationBlinkEffect     Module / Cell을 무작위로 차례로 점멸 (Charge / Discharge)
     ├ SimulationThermalEffect   열원 선택·성장, 온도·과열 수량 계산 (ThermalDetection)
     └ PackThermalView           열원을 PackThermal 셰이더에 전달
     ▼
   UI_SimulationResultView   StepResultReady를 받아 ResultListItem으로 줄마다 표시
```

- Runner는 "언제"만, EffectPlayer는 "무엇을 보여줄지"만 맡는다. 서로의 내부를 알지 못하고 이벤트로만 연결된다.
- 한 작업에서 초과된 시간은 다음 작업으로 넘겨 전체 시간이 지속 시간의 합과 어긋나지 않는다.
- `Stop()`으로 중단하면 진행 중이던 작업의 `StepEnded`와 `Finished`는 호출되지 않는다.
- 연출이 끝나거나 중단되면 바꿨던 몸체 머티리얼을 원래대로 되돌린다.

## 3. 프로젝트 구조

```
Assets/
├ @Scenes/            BootstrapScene, MainScene
├ @Scripts/           (어셈블리: Incheol.asmdef)
│  ├ Models/          데이터 모델. ScriptableObject(Container/Pack/Module/Cell/Scene/Addressable), SimulationData, SimulationStepResult, Define(enum)
│  ├ Controllers/     JsonDataController(저장 형식·파일 입출력), JsonDataMigration(버전 변환)
│  ├ Modules/         도메인 로직. 생성·배치·삭제·크기·간격·선택·시뮬레이션 연출·씬 로드
│  ├ Presenters/      MainSceneManager, BootstrapSceneManager, SelectionPanels, DeviceAxisGraphManager
│  ├ Views/           OrbitCameraView, PackThermalView, UI/ 아래 UI_* 뷰
│  └ Utils/           SingletonObject, Awaitable 헬퍼, DebugLogManager, TransformExtensions
├ @Graphics/          Shaders(CustomOutline, ElectricChargeFlow, PackThermal), Materials, Fonts
├ AddressableAssets/  Data(모델 SO), Prefabs(Pack/Module/Cell, UI)
├ Tests/              EditMode, PlayMode
├ Chart And Graph/    외부 차트 에셋
└ Plugins/Demigiant/  DOTween
```

### 주요 클래스

| 영역 | 클래스 | 역할 |
|---|---|---|
| 부팅 | `SequenceManager` | `ISequenceStep`을 큐에 쌓아 순서대로 실행하고 진행 이벤트를 알린다. |
| 부팅 | `SceneLoadManager`, `SceneLoadTasks`, `SceneDataRegistry` | 태그로 씬을 전환하고 필요한 Addressable을 미리 로드한다. 씬을 넘어 유지된다. |
| 리소스 | `AddressableAssetManager` | `AddressableAssetKey` 기준으로 프리팹을 로드·캐시·인스턴스화한다. |
| 생성 | `PackFactory` | 오브젝트 생성 → 배치 → 등록 흐름을 총괄한다. |
| 생성 | `CreateParentResolver` | 선택 상태에 따라 새 항목의 부모를 정한다. 알림 방식은 호출하는 쪽이 정한다. |
| 배치 | `PackGridPlacer`, `PackLayout`, `RendererBounds`, `DeviceBody` | Pack은 격자 칸, Module / Cell은 첫 빈 칸에 배치한다. 계산부는 씬을 바꾸지 않고 읽기만 한다. |
| 편집 | `DeviceSizeCalculator`, `DeviceSpacingCalculator`, `SubDeviceSpacingData` | 크기·간격 변경과 저장된 간격의 재적용. |
| 관리 | `PackRegistry`, `PackRemover`, `DeviceHierarchy`, `DeviceSelection`, `DeviceVisibility` | 등록·조회, 삭제, 계층 순회, 선택 상태, 표시 상태. |
| 저장 | `JsonDataController`, `PackJsonBuilder`, `JsonDataMigration` | 저장 데이터 구조와 파일 입출력, 불러온 데이터로 장치 재생성, 버전 변환. |
| 시뮬레이션 | `SimulationRunner`, `SimulationEffectPlayer`, `SimulationBlinkEffect`, `SimulationThermalEffect`, `ElectricChargeController` | 위 실행 흐름 참고. |
| UI | `UI_MainSceneView`, `UI_EditorView`, `UI_SimulationControl`, `UI_SimulationResultView` 외 | 생성·저장·불러오기, 트리, 시뮬레이션 편집·실행, 결과 표시. |

### 구조 원칙

- 데이터(`Models`), 로직(`Modules`), 화면(`Views`), 연결(`Presenters`)을 나눈다. 로직은 UI를 모르고 이벤트로만 알린다.
- 계산 클래스(`PackLayout`, `RendererBounds`, `CreateParentResolver`, `SimulationStepResult`)는 로그·팝업 없이 결과만 돌려주므로 EditMode 테스트로 확인하기 쉽다.
- 선택은 항상 1개이고 `DeviceSelection`이 단일 출처다. 선택에 따라 달라지는 패널은 `Changed` 이벤트 하나로 갱신한다(`SelectionPanels`).

## 4. 배치 규칙

자세한 설계는 [PlacementEngine_Draft.md](PlacementEngine_Draft.md) 참고.

- **좌표 기준**: 모든 계층이 좌측 바닥 뒤쪽 모서리 `(min.x, min.y, max.z)`를 고정하고 +x, +y, −z 방향으로 늘어난다.
- **Pack**: Container의 열×줄 격자 칸에 놓이고, 칸 번호(`slotColumn`, `slotRow`)를 모델에 기록한다. 좌표는 이웃 Pack의 실제 면에서 구해 크기·간격을 바꿔도 어긋나지 않는다.
- **Module / Cell**: 부모 내부를 `항목 크기 + 여백` 칸으로 나누고, 이미 놓인 항목과 겹치지 않는 첫 빈 칸에 놓는다. 칸이 없으면 생성하지 않는다.
- **부모 결정**: 선택이 없으면 가장 최근 Pack / Module. Container 선택 시 Pack은 그 안에, Module은 마지막 Pack에(없으면 단독 Module). Pack 선택 시 Cell은 마지막 Module에.

## 5. 저장 형식

- 저장 위치: `Application.persistentDataPath`, 파일 이름은 저장 시각(`yyyyMMdd_HHmmss_fff.json`)이라 이름 내림차순이 최신순이다.
- 임시 파일(`.tmp`)에 먼저 쓴 뒤 이름을 바꿔 완성하므로 쓰는 도중 종료돼도 깨진 JSON이 남지 않는다.
- 구조: `SaveJson { version, containers[ ContainerJson { packs[ PackJson { modules[ ModuleJson { cells[] } ] } ], standaloneModules[] } ] }`
- Pack은 칸 번호, Module / Cell은 몸체 중심(`bodyCenter`)으로 위치를 복원한다. 없을 수도 있는 값은 `has~` 플래그로 구분한다.
- 현재 버전은 `1`이다. 버전 0(Container가 없던 형식)은 `JsonDataMigration`이 Container로 감싸 변환한다. 현재보다 높은 버전은 읽지 않는다.
- 형식을 바꿀 때는 `CurrentVersion`을 올리고 `JsonDataMigration`에 변환 단계를 추가한다.

## 6. 렌더링 메모

- `CustomOutline`: 뒤집힌 껍질 방식 윤곽선 셰이더. Pack / Module은 투명(큐 3001 / 3000), Cell은 불투명이다.
- `ElectricChargeFlow`: 충전 진행도와 전류 흐름 표현. `MaterialPropertyBlock`으로 몸체마다 값을 준다.
- `PackThermal`: 열원 최대 8개(`PackThermalView.MaxHeatSources`, 셰이더의 `HEAT_MAX`와 같아야 한다)를 받아 열화상처럼 그린다.
- Cell 윗면의 z-fighting 개선 기록은 [Overlap_ZFighting_Fix_Notes.txt](Overlap_ZFighting_Fix_Notes.txt) 참고.

## 7. 테스트

Window ▸ General ▸ Test Runner에서 실행한다.

- **EditMode**(`Assets/Tests/EditMode`): 배치(`PackGridPlacementTests`), 팩토리(`PackFactoryTests`, `PackFactoryBodyTests`), 부모 결정(`CreateParentResolverTests`), 선택, 레지스트리, 몸체, Renderer 경계, JSON 왕복·버전 변환, 충전 컨트롤러.
- **PlayMode**(`Assets/Tests/PlayMode`): 삭제(`PackFactoryRemoveTests`), 싱글톤.

## 8. 시작하기

1. Unity Hub에서 Unity `6000.4.8f1`로 프로젝트를 연다.
2. 첫 실행 전 Addressables Groups 창에서 빌드 상태를 확인한다(`Assets/AddressableAssets`, `AddressableAssetsData`).
3. `Assets/@Scenes/BootstrapScene`을 열고 Play한다. 부팅 후 `MainScene`으로 자동 전환된다.
4. Editor 탭에서 Container → Pack → Module → Cell 순으로 만들고, Simulation 탭에서 작업을 추가해 실행한다.

## 9. 알려진 한계

- 시뮬레이션은 연출 중심의 프로토타입이다. 온도, 이상 수량은 무작위 선택과 환산식으로 만든 값이며 물리 계산이 아니다.
- 채우는 순서가 고정이고, Module / Cell은 칸 번호를 기록하지 않는다.
- 빈 칸 탐색이 선형이라 항목 수가 아주 많아지면 비용이 늘 수 있다.
