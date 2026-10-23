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
        private OrbitCameraView cameraView;
        private UI_PropertiesView propertiesView;
        private UI_DeviceScaleView scaleView;
        private UI_SubDeviceSpacingView spacingView;
        private UI_DeviceStructureView structureView;
        private UI_EditorView editorView;
        private UI_EditorView simulationTreeView;
        private UI_SelectedDeviceNameView nameView;
        private UI_MainSceneView mainSceneView;
        private UI_CreateDeviceCount createCountView;
        private UI_SimulationControl simulationControl;

        private readonly DeviceSelection selection = new DeviceSelection();
        #endregion

        #region LifeCycle
        private void Start()
        {
            cameraView = FindAnyObjectByType<OrbitCameraView>();

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
                nameView.RenameRequested -= RenameSelected;
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
        private void OnItemClicked(IDeviceNode _model, Transform _target)
        {
            SelectItem(_model, _target);
        }

        private void OnTreeChanged()
        {
            RefreshStructure();
            RefreshProperties();
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
            propertiesView = _root.GetComponentInChildren<UI_PropertiesView>(true);
            scaleView = _root.GetComponentInChildren<UI_DeviceScaleView>(true);
            spacingView = _root.GetComponentInChildren<UI_SubDeviceSpacingView>(true);
            structureView = _root.GetComponentInChildren<UI_DeviceStructureView>(true);
            nameView = _root.GetComponentInChildren<UI_SelectedDeviceNameView>(true);

            mainSceneView = _root.GetComponentInChildren<UI_MainSceneView>(true);

            // 트리 뷰는 탭마다 하나씩 있어서 계층 순서에 기대지 않고 UI_MainSceneView가 가진 것을 쓴다.
            editorView = mainSceneView != null ? mainSceneView.EditorTreeView : null;
            simulationTreeView = mainSceneView != null ? mainSceneView.SimulationTreeView : null;
            createCountView = _root.GetComponentInChildren<UI_CreateDeviceCount>(true);
            simulationControl = _root.GetComponentInChildren<UI_SimulationControl>(true);

            simulationControl?.Init(selection, editorView);
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
                nameView.RenameRequested += RenameSelected;
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
                RefreshProperties();
            }

            if (failedAxis >= 0)
            {
                string message = $"{parent.name}에 저장된 간격을 새 항목에 적용하면 밖으로 벗어나 적용하지 않았습니다.";
                DebugLogManager.GenerateErrorMessage<MainSceneManager>(message);
                spacingView?.ShowAlarm(message);
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

            selection.Set(_model, _target);

            editorView?.SetSelected(selection.Model);
            simulationTreeView?.SetSelected(selection.Model);
            DeviceVisibility.SetVisible(selection.Target, true, selection.Kind);
            RefreshGraph();
            RefreshProperties();
            RefreshName();
            RefreshStructure();

            if (cameraView != null)
            {
                cameraView.Track(selection.Target, selection.Target.root);
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

        /// <summary>
        /// 선택이 없으면 모든 축 그래프를 숨기고, 있으면 그 항목(selection.Target)의 그래프만 보이게 한다.
        /// 그래프는 DeviceAxisGraphManager가 선택된 항목에서만 만들고 그린다.
        /// </summary>
        private void RefreshGraph()
        {
            if (selection.Model == null)
            {
                DeviceAxisGraphManager.SetSelectedOwner(null);
                return;
            }

            DeviceAxisGraphManager.SetSelectedOwner(selection.Target);
        }

        /// <summary>선택된 항목에 트리가 표시하는 이름. 선택이 없거나 트리 뷰가 없으면 빈 문자열.</summary>
        private string GetSelectedLabel()
        {
            return editorView != null ? editorView.GetLabel(selection.Model) : string.Empty;
        }

        /// <summary>
        /// 선택된 항목 본인의 자식 구조(Pack이면 Module / Cell, Module이면 Cell)를 구조 패널에 보여준다. 선택이 없으면 패널을 비운다.
        /// 선택한 항목 아래에 새 자식이 생길 때도 UI_EditorView가 호출해 갱신한다. 패널과 트리 뷰는 비동기로 생성되므로 필요할 때 찾는다.
        /// </summary>
        private void RefreshStructure()
        {
            if (structureView == null)
            {
                return;
            }

            if (selection.Model == null)
            {
                structureView.Clear();
                return;
            }

            if (editorView == null)
            {
                structureView.Clear();
                return;
            }

            structureView.Show(editorView.GetLabel(selection.Model), editorView.GetChildStructure(selection.Model));
        }

        /// <summary>
        /// 선택된 항목의 이름과 위치를 속성 패널에, 실제 크기를 크기 패널에 반영한다. 선택이 없으면 패널을 비운다.
        /// 선택한 항목 아래에 자식이 생겨 크기가 변할 수 있어 TreeChanged를 받을 때도 호출한다.
        /// </summary>
        private void RefreshProperties()
        {
            bool hasSelection = selection.HasSelection;

            // Container는 빈 오브젝트라 몸체 크기가 없으므로 크기 패널은 비워 둔다(자식 Pack 사이의 간격은 간격 패널로 조절한다).
            bool hasBody = hasSelection && selection.Kind != DeviceType.Container;

            if (propertiesView != null)
            {
                if (hasSelection)
                {
                    propertiesView.ShowSelected(GetSelectedLabel(), selection.Target.position);
                }
                else
                {
                    propertiesView.Clear();
                }
            }

            if (scaleView != null)
            {
                if (hasBody)
                {
                    scaleView.ShowSelected(selection.Target);
                }
                else
                {
                    scaleView.Clear();
                }
            }

            if (spacingView != null)
            {
                if (hasSelection)
                {
                    spacingView.ShowSelected(selection.Target);
                }
                else
                {
                    spacingView.Clear();
                }
            }

            // Container가 선택됐을 때만 Pack 생성 수량 패널을 보여준다.
            createCountView?.Refresh();
        }

        /// <summary>
        /// 선택이 없으면 이름 변경 입력을 비우고 막고, 있으면 그 항목의 이름을 changeNameInputField에 채운다.
        /// 입력 중인 글자를 덮어쓰지 않도록 선택이 바뀔 때와 이름을 바꾼 뒤에만 호출한다.
        /// </summary>
        private void RefreshName()
        {
            simulationControl?.RefreshName();

            if (nameView == null)
            {
                return;
            }

            if (selection.Model == null)
            {
                nameView.Clear();
                return;
            }

            nameView.ShowSelected(GetSelectedLabel());
        }

        /// <summary>
        /// 선택된 항목의 이름을 _newName으로 바꾼다. 모델 / 씬 오브젝트 / 트리 줄이 즉시 바뀌고 속성 패널 이름도 갱신된다.
        /// 선택이 없거나 이름이 비어 있으면 바꾸지 않고 입력 필드를 현재 이름으로 되돌린다.
        /// </summary>
        public void RenameSelected(string _newName)
        {
            if (selection.Model == null)
            {
                return;
            }

            if (editorView != null && editorView.Rename(selection.Model, _newName))
            {
                simulationTreeView?.Rename(selection.Model, _newName);
                RefreshProperties();
            }

            RefreshName();
        }

        /// <summary>현재 선택을 취소한다. 항목의 강조를 끄고 그 오브젝트(와 자식)를 다시 숨긴다.</summary>
        public void DeselectItem()
        {
            DeviceVisibility.SetVisible(selection.Target, false);
            selection.Clear();
            editorView?.SetSelected(null);
            simulationTreeView?.SetSelected(null);
            RefreshGraph();
            RefreshProperties();
            RefreshName();
            RefreshStructure();

            // 선택이 없으면 카메라를 처음 시점으로 되돌린다.
            if (cameraView != null)
            {
                cameraView.ResetView();
            }
        }
        #endregion
    }
}
