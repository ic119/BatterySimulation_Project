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

        private readonly DeviceSelection selection = new DeviceSelection();
        private SelectionPanels panels;
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
                },
                () => DebugLogManager.GenerateErrorMessage<MainSceneManager>("UI_MainScene 프리팹 로드 실패"));
        }

        private void OnDestroy()
        {
            selection.Changed -= OnSelectionChanged;

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

        private void OnItemClicked(IDeviceNode _model, Transform _target)
        {
            SelectItem(_model, _target);
        }

        /// <summary>선택이 바뀌었을 때(선택 / 해제) 선택에 따라 달라지는 패널을 모두 갱신한다.</summary>
        private void OnSelectionChanged()
        {
            panels.RefreshAll();
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

            // 트리 뷰는 탭마다 하나씩 있어서 계층 순서에 기대지 않고 UI_MainSceneView가 가진 것을 쓴다.
            editorView = mainSceneView != null ? mainSceneView.EditorTreeView : null;
            simulationTreeView = mainSceneView != null ? mainSceneView.SimulationTreeView : null;
            UI_CreateDeviceCount createCountView = FindView<UI_CreateDeviceCount>(_root);
            UI_SimulationControl simulationControl = FindView<UI_SimulationControl>(_root);

            panels.Bind(propertiesView, scaleView, spacingView, structureView, nameView, createCountView, simulationControl, editorView, simulationTreeView);
            selection.Changed += OnSelectionChanged;

            simulationControl?.Init(selection, editorView, mainSceneView != null ? mainSceneView.PackFactory : null);
            scaleView?.Init(selection);
            spacingView?.Init(selection);
            createCountView?.Init(selection);
            mainSceneView?.Init(selection, createCountView);

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
            if (_created == null || selection.Target == null || selection.Kind == DeviceType.Cell)
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
