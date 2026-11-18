using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
using Incheol.Modules;
using Incheol.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Incheol.Presenter
{
    /// <summary>
    /// MainScene의 진입점. SceneLoadManager가 preload해 둔 Addressable 프리팹을 캐시에서 꺼내 인스턴스화하고,
    /// 트리에서 선택된 항목(1개)과 그 항목이 가리키는 오브젝트의 표시 상태를 관리한다.
    /// </summary>
    public class MainSceneManager : MonoBehaviour
    {
        #region Variable
        [Header("Scene References")]
        [SerializeField, Tooltip("MainScene의 OrbitCamera. 비워 두면 Start에서 씬을 뒤져 찾는다(경고).")] private OrbitCameraView cameraView;

        private UI_EditorView editorView;
        private UI_EditorView simulationTreeView;
        private UI_SelectedDeviceNameView nameView;
        private UI_MainSceneView mainSceneView;
        private UI_TabBarView tabBarView;
        private DeviceGridGraphManager gridGraph;

        private readonly DeviceSelection selection = new DeviceSelection();

        /// <summary>React → Unity 선택 신호(OnEssSelected)의 페이로드. 예) {"deviceId":"P-1"}. deviceId는 문자열로 보낸다.</summary>
        [System.Serializable]
        private class ReactDevicePayload
        {
            public string deviceId;
        }

        /// <summary>React → Unity 뷰 필터(OnEssListUpdated)의 페이로드. 예) {"list":[{"deviceId":"P-1"},{"deviceId":"M-3"}]}. list가 비어 있으면 원복이다.</summary>
        [System.Serializable]
        private class ReactDeviceListPayload
        {
            public System.Collections.Generic.List<ReactDevicePayload> list = new System.Collections.Generic.List<ReactDevicePayload>();
        }

        // 구독할 때 잡아 둔 브리지. 종료 중 Instance를 다시 부르면 새로 만들어지므로 해제할 때는 이 참조를 쓴다.
        private ReactBridge reactBridge;

        // React가 보낸 선택을 적용하는 동안 true. 이때 일어난 선택 변경은 React로 다시 보내지 않는다(에코 방지).
        private bool isApplyingReactSelection;

        // React의 OnEssListUpdated로 여러 장치를 보여 주는 중이면 true. 다른 선택이 들어오면 필터를 먼저 걷어낸다.
        private bool isReactFilterActive;
        private SelectionPanels panels;
        #endregion

        #region R&D Variable
        #endregion

        #region LifeCycle
        private void Awake()
        {
            panels = new SelectionPanels(selection);
        }

        private void Start()
        {
            if (cameraView == null)
            {
                cameraView = FindAnyObjectByType<OrbitCameraView>();
                DebugLogManager.GenerateWarningMessage<MainSceneManager>("cameraView가 인스펙터에 지정되지 않아 씬에서 찾았습니다. MainSceneManager에 OrbitCamera를 연결하세요.");
            }

            AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
                AddressableAssetKey.UI_MainScene.ToString(),
                prefab =>
                {
                    // 캐시된 프리팹은 Start에서 즉시 인스턴스화되는데, 이때 활성 씬이 아직 BootstrapScene이면 Bootstrap 언로드와 함께 사라진다.
                    // 항상 이 MainSceneManager가 속한 씬(MainScene)으로 옮긴다.
                    GameObject instance = AddressableAssetManager.Instance.InstantiatePrefab(prefab);
                    SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                    BindViews(instance);
                    DebugLogManager.GenerateLogMessage<MainSceneManager>("UI_MainScene 프리팹 인스턴스화 완료");

                    // UI 인스턴스화와 뷰 바인딩(React 이벤트 구독 포함)이 끝났으므로 React에 준비 완료를 알린다.
                    if (JsonDataLoader.Instance != null)
                    {
                        JsonDataLoader.Instance.NotifyUnityReady();
                    }
                },
                () => DebugLogManager.GenerateErrorMessage<MainSceneManager>("UI_MainScene 프리팹 로드 실패"));
        }

        private void OnDestroy()
        {

            if (editorView != null)
            {
                editorView.ItemClicked -= OnItemClicked;
                editorView.TreeChanged -= OnTreeChanged;
            }

            if (simulationTreeView != null)
            {
                simulationTreeView.ItemClicked -= OnItemClicked;
            }

            if (nameView != null)
            {
                nameView.RenameRequested -= panels.RenameSelected;
            }

            if (tabBarView != null)
            {
                tabBarView.TabChanged -= OnTabChanged;
            }

            if (mainSceneView != null)
            {
                mainSceneView.ObjectRemoving -= OnObjectRemoving;
                mainSceneView.SelectionClearRequested -= OnSelectionClearRequested;
                mainSceneView.ObjectCreated -= ShowIfInSelection;
                mainSceneView.ObjectCreated -= ApplySpacingToCreated;
                mainSceneView.ObjectCreated -= FollowCreatedPack;
                mainSceneView.PackSelectRequested -= OnPackSelectRequested;
            }


            if (JsonDataLoader.Instance != null)
            {
                JsonDataLoader.Instance.ReactParsed -= OnReactParsed;
            }

            DeviceAxisGraphManager.SetSelectedOwner(null);
        }
        #endregion

        #region Method
        /// <summary>
        /// 탭이 바뀌면 만들어 둔 오브젝트, 두 탭의 트리, 선택(DeviceSelection)을 모두 처음 상태로 되돌린다.
        /// 선택 해제와 카메라 복귀는 mainSceneView.ResetAll이 알리는 SelectionClearRequested에서 처리한다.
        /// </summary>
        private void OnTabChanged(bool _editorActive)
        {
            mainSceneView?.ResetAll();
        }

        /// <summary>A키로 만든 Pack을 선택해, 그 Pack(과 자식)만 보이게 하고 카메라가 따라가게 한다.</summary>
        private void OnPackSelectRequested(IDeviceNode _pack)
        {
            if (_pack != null && _pack.Position != null)
            {
                SelectItem(_pack, _pack.Position);
            }
        }

        private void OnItemClicked(IDeviceNode _model, Transform _target)
        {
            SelectItem(_model, _target);
        }



        private void OnTreeChanged()
        {
            panels.RefreshTree();
        }

        /// <summary>선택된 항목이 지워질 _removed 안에 있으면 선택을 푼다.</summary>
        private void OnObjectRemoving(Transform _removed)
        {
            if (selection.Target != null && selection.Target.IsChildOf(_removed))
            {
                DeselectItem();
            }
        }

        private void OnSelectionClearRequested()
        {
            if (selection.Model != null)
            {
                DeselectItem();
            }
        }

        /// <summary>
        /// 인스턴스화한 UI_MainScene 안의 뷰를 한 번만 모아 두고, 뷰가 알리는 이벤트를 받는다. 선택 정보를 읽는 뷰에는 DeviceSelection을 넘겨준다.
        /// 프리팹이 만들어지기 전에는 각 필드가 null이므로 사용하는 쪽은 null이면 건너뛴다.
        /// </summary>
        private void BindViews(GameObject _root)
        {
            UI_PropertiesView propertiesView = FindView<UI_PropertiesView>(_root);
            UI_DeviceScaleView scaleView = FindView<UI_DeviceScaleView>(_root);
            UI_SubDeviceSpacingView spacingView = FindView<UI_SubDeviceSpacingView>(_root);
            UI_DeviceStructureView structureView = FindView<UI_DeviceStructureView>(_root);
            nameView = FindView<UI_SelectedDeviceNameView>(_root);

            mainSceneView = FindView<UI_MainSceneView>(_root);
            tabBarView = FindView<UI_TabBarView>(_root);
            UI_GizmoIconView gizmoView = _root.GetComponentInChildren<UI_GizmoIconView>(true);
            if (gizmoView != null)
            {
                gizmoView.Init(Camera.main);
            }

            // 트리 뷰는 탭마다 하나씩 있어서 계층 순서에 기대지 않고 UI_MainSceneView가 가진 것을 쓴다.
            editorView = mainSceneView != null ? mainSceneView.EditorTreeView : null;
            simulationTreeView = mainSceneView != null ? mainSceneView.SimulationTreeView : null;
            UI_CreateDeviceCount createCountView = FindView<UI_CreateDeviceCount>(_root);
            UI_SimulationControl simulationControl = FindView<UI_SimulationControl>(_root);
            UI_SimulationResultView simulationResultView = FindView<UI_SimulationResultView>(_root);

            panels.Bind(propertiesView, scaleView, spacingView, structureView, nameView, createCountView, simulationControl, editorView, simulationTreeView);

            simulationControl?.Init(selection, editorView, mainSceneView != null ? mainSceneView.PackFactory : null, simulationResultView);
            scaleView?.Init(selection);
            spacingView?.Init(selection);
            createCountView?.Init(selection);
            mainSceneView?.Init(selection);

            if (editorView != null)
            {
                editorView.ItemClicked += OnItemClicked;
                editorView.TreeChanged += OnTreeChanged;
            }

            if (simulationTreeView != null)
            {
                simulationTreeView.ItemClicked += OnItemClicked;
            }

            if (nameView != null)
            {
                nameView.RenameRequested += panels.RenameSelected;
            }

            if (tabBarView != null)
            {
                tabBarView.TabChanged += OnTabChanged;
            }

            if (mainSceneView != null)
            {
                mainSceneView.ObjectRemoving += OnObjectRemoving;
                mainSceneView.SelectionClearRequested += OnSelectionClearRequested;
                mainSceneView.ObjectCreated += ShowIfInSelection;
                mainSceneView.ObjectCreated += ApplySpacingToCreated;
                mainSceneView.ObjectCreated += FollowCreatedPack;
                mainSceneView.PackSelectRequested += OnPackSelectRequested;
            }

            // React가 보낸 장비 데이터로 장면을 만든다. UI 준비보다 먼저 파싱이 끝났다면 이미 파싱된 데이터를 바로 적용한다.
            JsonDataLoader jsonLoader = JsonDataLoader.Instance;
            if (jsonLoader != null)
            {
                jsonLoader.ReactParsed += OnReactParsed;

                if (jsonLoader.isReactParsed)
                {
                    OnReactParsed(jsonLoader.jsonData);
                }
            }

            // 그래프는 가장 최근에 만든 Pack을 따라간다. Pack이 생기기 전에는 숨긴다.
            gridGraph = FindAnyObjectByType<DeviceGridGraphManager>();
            gridGraph?.SetTarget(null);
        }

        /// <summary>
        /// React에서 장치를 선택했을 때(OnEssSelected). deviceId로 장치를 찾아 트리에서 클릭한 것과 같은 방식으로 선택한다.
        /// 이미 그 장치가 선택돼 있으면 아무것도 하지 않는다(SelectItem은 같은 장치를 다시 누르면 해제하는 토글이라 React 신호로 해제되면 안 된다).
        /// </summary>
        private void OnReactEssSelected(string _json)
        {
            ReactDevicePayload payload;
            try
            {
                payload = JsonUtility.FromJson<ReactDevicePayload>(_json);
            }
            catch (System.Exception exception)
            {
                DebugLogManager.GenerateErrorMessage<MainSceneManager>($"OnEssSelected JSON 파싱 실패 : {exception.Message}");
                return;
            }

            if (payload == null || string.IsNullOrEmpty(payload.deviceId))
            {
                DebugLogManager.GenerateErrorMessage<MainSceneManager>($"OnEssSelected에 deviceId가 없습니다 : {_json}");
                return;
            }

            if (mainSceneView == null)
            {
                return;
            }

            IDeviceNode node = mainSceneView.PackFactory.FindByDeviceId(payload.deviceId);
            if (node == null || node.Position == null)
            {
                DebugLogManager.GenerateWarningMessage<MainSceneManager>($"deviceId에 해당하는 장치가 없습니다 : {payload.deviceId}");
                return;
            }

            if (selection.Model == node)
            {
                return;
            }

            isApplyingReactSelection = true;
            try
            {
                SelectItem(node, node.Position);
            }
            finally
            {
                isApplyingReactSelection = false;
            }
        }

        /// <summary>
        /// React에서 보여 줄 장치 목록이 왔을 때(OnEssListUpdated). 목록의 deviceId에 해당하는 장치만 보이게 하고 카메라를 맞춘다.
        /// list가 비어 있으면 원복(모두 숨기고 선택 해제, 카메라 처음 시점)이다. 목록에 없는 deviceId는 건너뛰고, 하나도 못 찾으면 현재 상태를 그대로 둔다.
        /// </summary>
        private void OnReactEssListUpdated(string _json)
        {
            ReactDeviceListPayload payload;
            try
            {
                payload = JsonUtility.FromJson<ReactDeviceListPayload>(_json);
            }
            catch (System.Exception exception)
            {
                DebugLogManager.GenerateErrorMessage<MainSceneManager>($"OnEssListUpdated JSON 파싱 실패 : {exception.Message}");
                return;
            }

            if (mainSceneView == null)
            {
                return;
            }

            if (payload == null || payload.list == null || payload.list.Count == 0)
            {
                RestoreReactView();
                return;
            }

            PackFactory packFactory = mainSceneView.PackFactory;
            System.Collections.Generic.List<IDeviceNode> nodes = new System.Collections.Generic.List<IDeviceNode>(payload.list.Count);
            System.Collections.Generic.HashSet<IDeviceNode> seen = new System.Collections.Generic.HashSet<IDeviceNode>();
            for (int i = 0; i < payload.list.Count; i++)
            {
                string deviceId = payload.list[i] != null ? payload.list[i].deviceId : null;
                IDeviceNode node = packFactory.FindByDeviceId(deviceId);
                if (node == null || node.Position == null)
                {
                    DebugLogManager.GenerateWarningMessage<MainSceneManager>($"OnEssListUpdated : deviceId에 해당하는 장치가 없습니다 : {deviceId}");
                    continue;
                }

                if (seen.Add(node))
                {
                    nodes.Add(node);
                }
            }

            if (nodes.Count == 0)
            {
                return;
            }

            // 하나만 선택돼 있던 상태는 목록과 섞이지 않게 먼저 푼다(선택 해제로 보이던 것은 숨겨지고 카메라는 아래에서 다시 맞춘다).
            if (selection.Model != null)
            {
                DeselectItem();
            }

            DeviceViewFilter.ShowOnly(packFactory, nodes);
            isReactFilterActive = true;

            if (cameraView != null)
            {
                cameraView.Track(nodes[0].Position, packFactory.Root);
            }

            ReactBridge.SendSceneUpdated();
        }

        /// <summary>OnEssListUpdated의 원복. 선택과 필터를 풀어 모두 숨기고 카메라를 처음 시점으로 되돌린다.</summary>
        private void RestoreReactView()
        {
            if (selection.Model != null)
            {
                DeselectItem();
            }

            ClearReactFilter();

            if (cameraView != null)
            {
                cameraView.ResetView();
            }

            ReactBridge.SendSceneUpdated();
        }

        /// <summary>여러 장치를 보여 주던 필터가 켜져 있으면 모두 숨기고 끈다. 꺼져 있으면 아무것도 하지 않는다.</summary>
        private void ClearReactFilter()
        {
            if (!isReactFilterActive)
            {
                return;
            }

            isReactFilterActive = false;

            if (mainSceneView != null)
            {
                DeviceViewFilter.HideAll(mainSceneView.PackFactory);
            }
        }

        /// <summary>React가 보낸 장비 데이터(JsonDataLoader가 파싱을 마친 것)로 장면을 다시 만들고, 다 만들면 React에 알린다.</summary>
        private void OnReactParsed(JsonDataController.SaveJson _data)
        {
            if (mainSceneView == null)
            {
                return;
            }

            mainSceneView.LoadFromSaveJson(_data, ReactBridge.SendSceneLoaded);
        }

        /// <summary>새로 생성된 _created가 Pack이면 좌표 그래프가 그 Pack 중심을 기준으로 그려지게 한다.</summary>
        private void FollowCreatedPack(Transform _created)
        {
            if (gridGraph == null || _created == null || mainSceneView == null)
            {
                return;
            }

            IDeviceNode node = mainSceneView.PackFactory.Find(_created);
            if (node != null && node.Kind == DeviceType.pack)
            {
                gridGraph.SetTarget(_created);
            }
        }

        /// <summary>
        /// 인스턴스화한 프리팹(_root) 아래에서 T 뷰를 찾는다(비활성 오브젝트 포함). 프리팹은 런타임에 만들어져 씬에서 미리 연결해 둘 수 없으므로 여기서 한 번만 찾는다.
        /// 찾지 못하면 그 뷰만 조용히 빠진 채 동작하게 되므로 어떤 뷰가 없는지 경고로 알린다.
        /// </summary>
        private static T FindView<T>(GameObject _root) where T : Component
        {
            T view = _root.GetComponentInChildren<T>(true);
            if (view == null)
            {
                DebugLogManager.GenerateWarningMessage<MainSceneManager>($"UI_MainScene 프리팹에서 {typeof(T).Name}을(를) 찾지 못했습니다. 해당 패널은 갱신되지 않습니다.");
            }

            return view;
        }

        /// <summary>
        /// 새로 생성된 _created의 부모에 이미 적용한 간격이 있으면 같은 간격을 다시 적용한다.
        /// 간격을 늘리면 부모 몸체를 벗어나는 축은 건드리지 않고 알람으로 알린다.
        /// </summary>
        private void ApplySpacingToCreated(Transform _created)
        {
            if (_created == null || _created.parent == null)
            {
                return;
            }

            Transform parent = _created.parent;
            if (DeviceSpacingCalculator.ApplyStored(parent, out int failedAxis) && parent == selection.Target)
            {
                panels.RefreshProperties();
            }

            if (failedAxis >= 0)
            {
                string message = $"{parent.name}에 저장된 간격을 새 항목에 적용하면 밖으로 벗어나 적용하지 않았습니다.";
                DebugLogManager.GenerateErrorMessage<MainSceneManager>(message);
                panels.ShowSpacingAlarm(message);
            }
        }

        /// <summary>
        /// _model을 선택 (항상 1개뿐이라 이전 선택은 해제하고 그 오브젝트는 비활성화)
        /// 새로 선택한 오브젝트일 경우, 자식(Module의 Cell 등)까지 전부 활성화 후 카메라 추적 실행
        /// 이미 선택된 항목을 다시 누르면 선택이 취소되어 해당 오브젝트 비활성화, 선택된 항목은 없는 상태 처리 (null)
        /// </summary>
        public void SelectItem(IDeviceNode _model, Transform _target)
        {
            if (_model == null || _target == null)
            {
                return;
            }

            // 여러 장치를 보여 주던 필터가 있으면 걷어내고 하나만 보여 준다.
            ClearReactFilter();

            if (selection.Model == _model)
            {
                DeselectItem();
                return;
            }

            if (selection.Model != null)
            {
                DeviceVisibility.SetVisible(selection.Target, false);
            }

            // 패널 갱신은 selection.Set이 알리는 Changed(OnSelectionChanged)에서 하므로, 트리 강조와 표시 상태를 먼저 맞춰 둔다.
            editorView?.SetSelected(_model);
            simulationTreeView?.SetSelected(_model);
            DeviceVisibility.SetVisible(_target, true, _model.Kind);
            selection.Set(_model, _target);

            if (cameraView != null)
            {
                cameraView.Track(_target, _target.root);
            }
        }

        /// <summary>
        /// 새로 생성된 _created가 현재 선택된 Pack / Module 아래에 있으면 생성 즉시 보이게 하고 카메라를 다시 맞춘다.
        /// 선택이 없거나 Cell이 선택되어 있거나 선택 범위 밖에 생성된 것이면 아무 것도 하지 않는다(숨긴 채로 둔다).
        /// </summary>
        public void ShowIfInSelection(Transform _created)
        {
            if (_created == null || selection.Target == null || selection.Kind == DeviceType.cell)
            {
                return;
            }

            if (!_created.IsChildOf(selection.Target))
            {
                return;
            }

            DeviceVisibility.SetVisible(_created, true);

            if (cameraView != null)
            {
                cameraView.Refocus();
            }
        }

        /// <summary>현재 선택을 취소한다. 항목의 강조를 끄고 그 오브젝트(와 자식)를 다시 숨긴다.</summary>
        public void DeselectItem()
        {
            DeviceVisibility.SetVisible(selection.Target, false);
            editorView?.SetSelected(null);
            simulationTreeView?.SetSelected(null);
            selection.Clear();

            // 선택이 없으면 카메라를 처음 시점으로 되돌린다.
            if (cameraView != null)
            {
                cameraView.ResetView();
            }
        }
        #endregion
    }
}
