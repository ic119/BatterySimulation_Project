using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
using Incheol.Modules;
using Incheol.Presenter;
using Incheol.Utils;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_MainSceneView : MonoBehaviour
{
    private enum CreateType
    {
        Pack,
        Module,
        Cell,
    }

    #region Variable
    [Header("EditorTab Tools Button Variable")]
    [SerializeField] private Button newButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private TMP_Dropdown newDropdown;

    [Header("Popup / List")]
    [SerializeField] private UI_AlarmPopupView alarmPopupView;
    [SerializeField, Tooltip("SimulationTab의 알림 팝업. 같은 내용을 함께 띄운다.")] private UI_AlarmPopupView simulationAlarmPopupView;
    [SerializeField, Tooltip("SimulationTab의 DeviceStructureContainer 트리. EditorTab 트리와 같은 항목을 함께 보여준다.")] private UI_EditorView simulationTreeView;
    [SerializeField] private UI_ConfirmPopupView confirmPopupView;
    [SerializeField, Tooltip("SimulationTab의 삭제 확인 팝업.")] private UI_ConfirmPopupView simulationConfirmPopupView;
    [SerializeField] private UI_JsonFileListView jsonFileListView;

    [Header("Pack Layout")]
    [SerializeField, Min(0.0f), Tooltip("같은 Container 안에서 Pack 사이의 가로 여백.")] private float packPadding = 0.0f;

    [Header("Module Layout")]
    [SerializeField, Min(0.0f), Tooltip("Module 사이(가로/깊이)의 여백. Module은 칸 가운데에 놓여 Pack 벽과도 여백의 절반만큼 떨어진다.")] private float modulePadding = 0.0f;

    [Header("Cell Layout")]
    [SerializeField, Min(0.0f), Tooltip("Cell 사이(가로/깊이)의 여백. Cell은 칸 가운데에 놓여 벽과도 여백의 절반만큼 떨어진다. 클수록 Module에 들어가는 Cell 개수가 줄어든다.")] private float cellPadding = 0.0f;

    private static readonly CreateType[] DropdownCreateTypes = { CreateType.Pack, CreateType.Module, CreateType.Cell };

    private readonly PackFactory packFactory = new PackFactory();

    // 같은 루트(UI_MainScene)에 붙어 있는 트리 뷰. 생성된 Pack/Module/Cell을 여기에 알린다.
    private UI_EditorView editorView;
    private DeviceSelection selection;
    private UI_CreateDeviceCount createCountView;

    private int containerCount;
    private int packCount;
    private int moduleCount;
    private int cellCount;
    private int skippedCount;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        editorView = GetComponent<UI_EditorView>();
        if (editorView != null)
        {
            editorView.DeleteRequested += OnDeleteRequested;
        }

        if (simulationTreeView != null)
        {
            simulationTreeView.DeleteRequested += OnSimulationDeleteRequested;
        }

        if (jsonFileListView != null)
        {
            jsonFileListView.FileClicked += OnJsonFileClicked;
        }

        if (newButton != null)
        {
            newButton.onClick.AddListener(OnClickNewButton);
        }

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnClickSaveButton);
        }

        if (loadButton != null)
        {
            loadButton.onClick.AddListener(OnClickLoadButton);
        }

        if (newDropdown != null)
        {
            BuildDropdownOptions();
        }
    }

    private void Update()
    {
        // SceneLoadManager가 MainScene을 Additive로 로드한 "뒤"에 BootstrapScene(EventSystem 보유)을 언로드하므로,
        // Awake 한 번만 검사하면 그 이후 EventSystem이 사라지는 것을 놓친다. 없어지면 즉시 다시 만든다.
        EnsureEventSystem();
    }

    private void OnDestroy()
    {
        if (editorView != null)
        {
            editorView.DeleteRequested -= OnDeleteRequested;
        }

        if (simulationTreeView != null)
        {
            simulationTreeView.DeleteRequested -= OnSimulationDeleteRequested;
        }

        if (newButton != null)
        {
            newButton.onClick.RemoveListener(OnClickNewButton);
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnClickSaveButton);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(OnClickLoadButton);
        }

        if (jsonFileListView != null)
        {
            jsonFileListView.FileClicked -= OnJsonFileClicked;
        }
    }
    #endregion

    /// <summary>Container / Pack / Module / Cell을 만들고 등록부를 들고 있는 팩토리. 다른 뷰가 Pack의 씬 Transform을 찾을 때 쓴다.</summary>
    public PackFactory PackFactory => packFactory;

    /// <summary>EditorTab의 트리 뷰(이 루트에 붙어 있다).</summary>
    public UI_EditorView EditorTreeView => editorView;

    /// <summary>SimulationTab의 트리 뷰. 없으면 null.</summary>
    public UI_EditorView SimulationTreeView => simulationTreeView;

    /// <summary>Pack / Module / Cell을 지우기 직전에 발생한다. 인자는 지워질 오브젝트이다. 선택된 항목이 그 안에 있으면 받는 쪽이 선택을 풀어야 한다.</summary>
    public event System.Action<Transform> ObjectRemoving;

    /// <summary>모두 지우고 다시 만들기 전에 발생한다. 받는 쪽은 현재 선택을 풀어 카메라와 패널을 되돌린다.</summary>
    public event System.Action SelectionClearRequested;

    /// <summary>새 Pack / Module / Cell을 만들어 숨긴 직후 발생한다. 인자는 만들어진 오브젝트이다.</summary>
    public event System.Action<Transform> ObjectCreated;

    #region Method
    // 트리는 EditorTab의 UI_EditorView가 기준이고, SimulationTab의 트리(simulationTreeView)에는 같은 변경을 그대로 따라 한다.
    private void AddContainerNode(ContainerDataModel _container)
    {
        editorView?.AddContainer(_container);
        simulationTreeView?.AddContainer(_container);
    }

    private void AddPackNode(PackDataModel _pack, Transform _target)
    {
        editorView?.AddPack(_pack, _target);
        simulationTreeView?.AddPack(_pack, _target);
    }

    private void AddModuleNode(ModuleDataModel _module, PackDataModel _parentPack)
    {
        editorView?.AddModule(_module, _parentPack);
        simulationTreeView?.AddModule(_module, _parentPack);
    }

    private void AddCellNode(CellDataModel _cell)
    {
        editorView?.AddCell(_cell);
        simulationTreeView?.AddCell(_cell);
    }

    private void RemoveTreeNode(IDeviceNode _model)
    {
        editorView?.RemoveNode(_model);
        simulationTreeView?.RemoveNode(_model);
    }

    /// <summary>
    /// 만들어 둔 Container / Pack / Module / Cell(씬 오브젝트와 모델), 두 탭의 트리, 선택, 번호 이어 붙이기, Container별 수량을 모두 처음 상태로 되돌린다.
    /// 탭을 바꿀 때 쓴다. 저장하지 않은 내용은 사라진다.
    /// </summary>
    public void ResetAll()
    {
        // 지울 오브젝트가 선택되어 있으면 먼저 선택을 풀어 카메라와 패널을 되돌린다.
        SelectionClearRequested?.Invoke();

        ClearTrees();
        packFactory.Clear();

        containerCount = 0;
        packCount = 0;
        moduleCount = 0;
        cellCount = 0;
        skippedCount = 0;

        createCountView?.ClearCounts();
    }

    private void ClearTrees()
    {
        editorView?.Clear();
        simulationTreeView?.Clear();
    }

    /// <summary>EditorTab과 SimulationTab의 알림 팝업에 같은 내용을 띄운다. 꺼져 있는 탭의 팝업은 보이지 않을 뿐이다.</summary>
    private void ShowAlarm(string _message)
    {
        alarmPopupView?.Show(_message);
        simulationAlarmPopupView?.Show(_message);
    }

    /// <summary>MainSceneManager가 프리팹을 인스턴스화한 직후 선택 상태와 Pack 수량 패널을 넘겨준다. 뷰가 씬에서 직접 찾지 않는다.</summary>
    public void Init(DeviceSelection _selection, UI_CreateDeviceCount _createCountView)
    {
        selection = _selection;
        createCountView = _createCountView;
    }

    /// <summary>
    /// EventSystem은 BootstrapScene에만 있어 MainScene 단독 실행/Bootstrap 언로드 시 UI 입력이 전달되지 않는다.
    /// 활성 EventSystem이 없으면 Input System용 모듈과 함께 만들어 MainScene 쪽에 둔다.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystemObject, gameObject.scene);
    }

    /// <summary>newButton은 Dropdown에서 선택된 종류를 생성한다.</summary>
    private void OnClickNewButton()
    {
        // Dropdown에서 선택된 종류(Pack / Module / Cell)를 생성한다. Dropdown이 없으면 Pack.
        CreateType type = CreateType.Pack;
        if (newDropdown != null && newDropdown.value >= 0 && newDropdown.value < DropdownCreateTypes.Length)
        {
            type = DropdownCreateTypes[newDropdown.value];
        }

        CreateObject(type);
    }

    /// <summary>
    /// 트리 줄의 삭제 버튼이 눌리면 해당 Pack / Module / Cell을 (자식까지) 씬, 데이터 모델, 트리에서 지운다.
    /// 지우는 대상이나 그 하위가 선택되어 있으면 먼저 선택을 풀어 카메라 / 패널 / 축 그래프가 파괴된 오브젝트를 가리키지 않게 한다.
    /// 번호 카운터는 줄이지 않는다(이름이 겹치지 않도록).
    /// </summary>
    private void OnDeleteRequested(IDeviceNode _model)
    {
        RequestDelete(_model, confirmPopupView);
    }

    /// <summary>SimulationTab 트리의 삭제 버튼. 확인 팝업은 SimulationTab의 것을 쓴다(꺼진 탭의 팝업은 보이지 않으므로).</summary>
    private void OnSimulationDeleteRequested(IDeviceNode _model)
    {
        RequestDelete(_model, simulationConfirmPopupView);
    }

    private void RequestDelete(IDeviceNode _model, UI_ConfirmPopupView _confirmPopup)
    {
        string description;
        switch (_model)
        {
            case ContainerDataModel container:
                description = $"Container {container.containerName}과(와) 그 안의 모든 Pack / Module / Cell을";
                break;
            case PackDataModel pack:
                description = $"Pack {pack.packName}과(와) 그 안의 모든 Module / Cell을";
                break;
            case ModuleDataModel module:
                description = $"Module {module.moduleName}과(와) 그 안의 모든 Cell을";
                break;
            case CellDataModel cell:
                description = $"Cell {cell.cellName}을(를)";
                break;
            default:
                return;
        }

        // 확인 팝업이 없으면 묻지 않고 지우는 일은 하지 않는다.
        if (_confirmPopup == null)
        {
            DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("UI_ConfirmPopupView를 찾지 못해 삭제하지 않았습니다.");
            return;
        }

        _confirmPopup.Show($"{description} 삭제하시겠습니까?\n삭제하면 되돌릴 수 없습니다.", () => DeleteModel(_model));
    }

    private void DeleteModel(IDeviceNode _model)
    {
        Transform removedTransform = packFactory.GetTransform(_model);
        if (removedTransform == null)
        {
            return;
        }

        // 지우고 나면 모델에서 읽을 수 없으므로 지우기 전에 속한 Container를 기억해 둔다(Container와 Cell은 없다).
        ContainerDataModel parentContainer = null;
        switch (_model)
        {
            case PackDataModel pack:
                parentContainer = pack.parentContainer;
                break;
            case ModuleDataModel module:
                parentContainer = module.parentContainer;
                break;
        }

        ObjectRemoving?.Invoke(removedTransform);

        if (packFactory.Remove(_model))
        {
            RemoveTreeNode(_model);

            // 마지막 Pack / 단독 Module을 지워 비게 된 Container는 함께 지운다.
            if (parentContainer != null && parentContainer.packsList.Count == 0 && parentContainer.modulesList.Count == 0)
            {
                ObjectRemoving?.Invoke(parentContainer.containerPosition);
                packFactory.RemoveContainer(parentContainer);
                RemoveTreeNode(parentContainer);
            }

            DebugLogManager.GenerateLogMessage<UI_MainSceneView>("삭제 완료");
        }
    }

    /// <summary>saveButton은 지금까지 생성된 Pack / Module / Cell을 Json 파일로 저장하고 결과를 알람 팝업으로 알려준다.</summary>
    private void OnClickSaveButton()
    {
        bool saved = JsonDataController.Save(packFactory, GetPackGrid);
        ShowAlarm(saved ? "저장되었습니다." : "저장할 Pack / Module이 없거나 저장에 실패했습니다.");
    }

    /// <summary>저장할 _container의 Pack 생성 격자(x 개수, z 줄 수). 패널이 없으면 (0, 0)이라 저장하지 않은 것으로 본다.</summary>
    private Vector2Int GetPackGrid(ContainerDataModel _container)
    {
        if (createCountView != null && createCountView.TryGetPackGrid(_container, out int gridX, out int gridZ))
        {
            return new Vector2Int(gridX, gridZ);
        }

        return Vector2Int.zero;
    }

    /// <summary>loadButton은 저장된 Json 파일 목록을 JsonFileListContainer의 ScrollView에 보여준다.</summary>
    private void OnClickLoadButton()
    {
        jsonFileListView?.Show();
    }

    /// <summary>목록에서 Json 파일을 누르면 현재 Pack / Module / Cell을 모두 지우고 그 파일의 내용으로 다시 만든다.</summary>
    private void OnJsonFileClicked(string _path)
    {
        LoadFromFile(_path);
    }

    /// <summary>_path의 Json으로 모든 Pack / Module / Cell을 다시 만든다. Editor 탭과 Simulation 탭의 Load가 같이 쓴다.</summary>
    public void LoadFromFile(string _path)
    {
        JsonDataController.SaveJson data = JsonDataController.Load(_path);
        if (data == null)
        {
            ShowAlarm("Json 파일을 읽지 못했습니다.");
            return;
        }

        // Pack → Module → Cell Prefab을 차례로 불러온 뒤 한 번에 만든다.
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            AddressableAssetKey.PackPrefab.ToString(),
            packPrefab => AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
                AddressableAssetKey.ModulePrefab.ToString(),
                modulePrefab => AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
                    AddressableAssetKey.CellPrefab.ToString(),
                    cellPrefab => BuildFromJson(data, packPrefab, modulePrefab, cellPrefab),
                    () => DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("CellPrefab 로드 실패")),
                () => DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("ModulePrefab 로드 실패")),
            () => DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("PackPrefab 로드 실패"));
    }

    /// <summary>
    /// 지금 있는 것을 모두 지우고 _data의 Pack / Module / Cell을 newButton으로 만들 때와 같은 방식으로 다시 만든다.
    /// 만드는 일은 PackJsonBuilder가 하고, 이 뷰는 만들어진 것을 숨겨 두고 트리에 추가하며 번호를 이어 붙인다.
    /// </summary>
    private void BuildFromJson(JsonDataController.SaveJson _data, GameObject _packPrefab, GameObject _modulePrefab, GameObject _cellPrefab)
    {
        // 지울 오브젝트가 선택되어 있으면 먼저 선택을 풀어 카메라와 패널을 되돌린다.
        SelectionClearRequested?.Invoke();

        ClearTrees();
        packFactory.Clear();

        // newButton으로 만들 때와 같은 여백 설정을 쓴다.
        ApplyPadding();

        PackJsonBuilder builder = new PackJsonBuilder(
            packFactory,
            container => AddContainerNode(container),
            OnPackCreated,
            OnModuleCreated,
            OnCellCreated,
            (container, columns, rows) => createCountView?.SetCount(container, columns, rows));

        PackJsonBuilder.Result result = builder.Build(_data, _packPrefab, _modulePrefab, _cellPrefab);
        containerCount = result.containerCount;
        packCount = result.packCount;
        moduleCount = result.moduleCount;
        cellCount = result.cellCount;
        skippedCount = result.skippedCount;

        DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"Json 불러오기 완료 : Pack {packFactory.Packs.Count}개, Module {packFactory.Modules.Count}개, 건너뜀 {skippedCount}개");
        ShowAlarm(skippedCount == 0 ? "불러왔습니다." : $"불러왔지만 공간이 없어 {skippedCount}개는 만들지 못했습니다.");
    }

    private void BuildDropdownOptions()
    {
        List<string> optionList = new List<string>();
        for (int i = 0; i < DropdownCreateTypes.Length; i++)
        {
            optionList.Add(DropdownCreateTypes[i].ToString());
        }

        newDropdown.ClearOptions();
        newDropdown.AddOptions(optionList);

        // 선택된 항목을 다시 눌러도 onValueChanged가 발생하지 않으므로, 템플릿 Item이 클릭을 직접 전달하도록 한다.
        Toggle itemTemplate = newDropdown.template != null ? newDropdown.template.GetComponentInChildren<Toggle>(true) : null;
        if (itemTemplate != null && itemTemplate.GetComponent<UI_DropdownItemRelay>() == null)
        {
            itemTemplate.gameObject.AddComponent<UI_DropdownItemRelay>();
        }
    }

    /// <summary>Dropdown 옵션(Pack / Module / Cell) 항목이 클릭되면 UI_DropdownItemRelay가 호출해 해당 종류를 생성한다.</summary>
    public void OnDropdownItemClicked(string _optionText)
    {
        if (System.Enum.TryParse(_optionText, out CreateType type))
        {
            CreateObject(type);
        }
    }

    private static AddressableAssetKey GetPrefabKey(CreateType _type)
    {
        switch (_type)
        {
            case CreateType.Pack: return AddressableAssetKey.PackPrefab;
            case CreateType.Module: return AddressableAssetKey.ModulePrefab;
            default: return AddressableAssetKey.CellPrefab;
        }
    }

    /// <summary>
    /// 생성할 _type의 부모를 정한다.
    /// - 트리에서 선택된 항목이 없으면 예전처럼 가장 최근 Pack / Module이 부모다.
    /// - Pack이 선택되어 있으면 Module은 그 Pack에, Cell은 그 Pack의 마지막 Module에 생긴다.
    /// - Module이 선택되어 있으면 Cell은 그 Module에 생긴다.
    /// - 선택된 항목 아래에 만들 수 없는 종류(Pack 아래의 Pack, Module 아래의 Pack/Module, Cell 아래의 모든 것)거나
    ///   부모를 찾지 못하면 false를 반환하고 생성하지 않는다.
    /// </summary>
    private bool ResolveParent(CreateType _type, out PackDataModel _parentPack, out ModuleDataModel _parentModule, out ContainerDataModel _parentContainer)
    {
        _parentPack = packFactory.LastPack;
        _parentModule = packFactory.LastModule;
        _parentContainer = null;

        if (selection == null || !selection.HasSelection)
        {
            return true;
        }

        Transform selectedTarget = selection.Target;
        switch (selection.Kind)
        {
            case DeviceType.Container:
                {
                    ContainerDataModel selectedContainer = packFactory.FindContainer(selectedTarget);
                    if (selectedContainer == null)
                    {
                        break;
                    }

                    // 선택한 Container 안에 Pack을, Module은 하위 Pack 안에(Pack이 없으면 Pack 없는 Module로) 만든다.
                    if (_type == CreateType.Pack)
                    {
                        if (createCountView != null && !createCountView.CanCreatePack(selectedContainer, selectedContainer.packsList.Count))
                        {
                            createCountView.TryGetPackGrid(selectedContainer, out int gridX, out int gridZ);
                            string message = $"Container {selectedContainer.containerName}의 x({gridX}) × z({gridZ}) 칸이 모두 차서 더 이상 Pack을 생성할 수 없습니다.";
                            DebugLogManager.GenerateErrorMessage<UI_MainSceneView>(message);
                            ShowAlarm(message);
                            return false;
                        }

                        _parentContainer = selectedContainer;
                        return true;
                    }

                    if (_type == CreateType.Module)
                    {
                        // 하위에 Pack이 있으면 마지막 Pack의 자식으로, 없을 때만 Pack 없는 Module로 만든다.
                        _parentPack = selectedContainer.packsList.Count > 0 ? selectedContainer.packsList[selectedContainer.packsList.Count - 1] : null;
                        _parentContainer = selectedContainer;
                        return true;
                    }

                    DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("Container 아래에는 Cell을 직접 만들 수 없습니다. Module을 선택하세요.");
                    return false;
                }
            case DeviceType.Pack:
                {
                    PackDataModel selectedPack = packFactory.FindPack(selectedTarget);
                    if (selectedPack == null)
                    {
                        break;
                    }

                    if (_type == CreateType.Module)
                    {
                        _parentPack = selectedPack;
                        return true;
                    }

                    if (_type == CreateType.Cell)
                    {
                        if (selectedPack.modulesList.Count == 0)
                        {
                            DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("선택한 Pack에 Module이 없어 Cell을 만들 수 없습니다.");
                            return false;
                        }

                        _parentModule = selectedPack.modulesList[selectedPack.modulesList.Count - 1];
                        return true;
                    }

                    DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("Pack 아래에는 Pack을 만들 수 없습니다.");
                    return false;
                }
            case DeviceType.Module:
                {
                    ModuleDataModel selectedModule = packFactory.FindModule(selectedTarget);
                    if (selectedModule == null)
                    {
                        break;
                    }

                    if (_type == CreateType.Cell)
                    {
                        _parentModule = selectedModule;
                        return true;
                    }

                    DebugLogManager.GenerateErrorMessage<UI_MainSceneView>($"Module 아래에는 {_type}을(를) 만들 수 없습니다.");
                    return false;
                }
            case DeviceType.Cell:
                DebugLogManager.GenerateErrorMessage<UI_MainSceneView>($"Cell 아래에는 {_type}을(를) 만들 수 없습니다.");
                return false;
        }

        // 선택된 항목의 데이터 모델을 찾지 못하면 선택을 무시하고 예전 방식(최근 오브젝트)으로 만든다.
        return true;
    }

    /// <summary>Addressable의 Pack/Module/CellPrefab을 불러와 해당 종류를 생성한다. 선택된 항목이 있으면 그 항목의 하위에 만든다.</summary>
    private void CreateObject(CreateType _type)
    {
        // 클릭 시점의 선택으로 부모를 정한다. 만들 수 없는 조합이면 불필요한 로드를 하지 않고 끝낸다.
        if (!ResolveParent(_type, out PackDataModel parentPack, out ModuleDataModel parentModule, out ContainerDataModel parentContainer))
        {
            return;
        }

        // Cell은 부모 Module이 있어야만 만들 수 있다.
        if (_type == CreateType.Cell && (!packFactory.CanCreateCell || parentModule == null))
        {
            DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("Cell은 생성된 Module이 있어야 만들 수 있습니다.");
            return;
        }

        string key = GetPrefabKey(_type).ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab => OnPrefabLoaded(_type, prefab, parentPack, parentModule, parentContainer),
            () => DebugLogManager.GenerateErrorMessage<UI_MainSceneView>($"{key} 로드 실패"));
    }

    /// <summary>Pack / Module / Cell 사이의 여백 설정을 PackFactory에 넘긴다. 만들기 직전에 부른다.</summary>
    private void ApplyPadding()
    {
        packFactory.PackPadding = packPadding;
        packFactory.ModulePadding = modulePadding;
        packFactory.CellPadding = cellPadding;
    }

    /// <summary>새로 만들어진 Pack을 숨기고 두 탭의 트리에 추가한다. 직접 만들 때와 Json에서 불러올 때 같이 쓴다.</summary>
    private void OnPackCreated(PackDataModel _pack, Transform _packTransform)
    {
        HideOrShowCreated(_packTransform);
        AddPackNode(_pack, _packTransform);
    }

    /// <summary>새로 만들어진 Module을 숨기고 두 탭의 트리에 추가한다. _parentPack이 null이면 단독 Module이다.</summary>
    private void OnModuleCreated(ModuleDataModel _module, PackDataModel _parentPack)
    {
        HideOrShowCreated(_module.modulePosition);
        AddModuleNode(_module, _parentPack);
    }

    /// <summary>새로 만들어진 Cell을 숨기고 두 탭의 트리에 추가한다.</summary>
    private void OnCellCreated(CellDataModel _cell)
    {
        HideOrShowCreated(_cell.cellPosition);
        AddCellNode(_cell);
    }

    /// <summary>
    /// 새로 만든 _created는 기본적으로 숨긴다. 단, 현재 선택된 Pack / Module 아래에 생성된 것이면
    /// 생성되는 모습이 바로 보이도록 ObjectCreated를 받는 쪽이 다시 켜준다.
    /// </summary>
    private void HideOrShowCreated(Transform _created)
    {
        DeviceVisibility.SetVisible(_created, false);

        ObjectCreated?.Invoke(_created);
    }

    /// <summary>가장 최근 생성이 "더 이상 놓을 공간이 없어서" 실패했다면 그 안내 문구를 알람 팝업으로 잠깐 보여준다. 다른 이유의 실패면 아무것도 하지 않는다.</summary>
    private void ShowFullAlarm()
    {
        string message = packFactory.LastFullMessage;
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        ShowAlarm(message);
    }

    /// <summary>_container가 있으면 그대로, 없으면 새 Container를 만들어 트리에 추가해 돌려준다.</summary>
    private ContainerDataModel EnsureContainer(ContainerDataModel _container)
    {
        if (_container != null)
        {
            return _container;
        }

        ContainerDataModel container = packFactory.CreateContainer((++containerCount).ToString());
        AddContainerNode(container);
        return container;
    }

    /// <summary>_parentPack / _parentModule / _parentContainer는 클릭 시점에 ResolveParent로 정해진 부모다(프리팹 로드를 기다리는 사이 선택이 바뀌어도 그대로 쓴다).</summary>
    private void OnPrefabLoaded(CreateType _type, GameObject _prefab, PackDataModel _parentPack, ModuleDataModel _parentModule, ContainerDataModel _parentContainer)
    {
        ApplyPadding();

        switch (_type)
        {
            case CreateType.Pack:
                {
                    // 선택한 Container가 없으면 이 Pack을 담을 새 Container를 만든다.
                    ContainerDataModel container = EnsureContainer(_parentContainer);
                    // 선택한 Container의 x * z 격자에 맞춰 x 방향으로 채운 뒤 z 방향 다음 줄에 놓는다(새 Container나 패널이 없으면 기존처럼 x 방향).
                    int gridX = 0;
                    int gridZ = 0;
                    if (_parentContainer != null && createCountView != null)
                    {
                        createCountView.TryGetPackGrid(_parentContainer, out gridX, out gridZ);
                    }

                    PackDataModel pack = packFactory.CreatePack((++packCount).ToString(), _prefab, default, container, gridX, gridZ);
                    OnPackCreated(pack, packFactory.GetPackTransform(pack));
                    DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"Pack 생성 : {pack.packName}");
                    break;
                }
            case CreateType.Module:
                {
                    // 부모 Pack이 있으면 그 Pack에 속하고, 없으면 단독 Module로 만든다.
                    ContainerDataModel container = _parentPack == null ? EnsureContainer(_parentContainer) : null;
                    ModuleDataModel module = packFactory.CreateModule(_parentPack, (++moduleCount).ToString(), _prefab, default, container);
                    if (module != null)
                    {
                        OnModuleCreated(module, _parentPack);
                        DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"Module 생성 : {module.moduleName}");
                    }
                    else
                    {
                        moduleCount--;
                        ShowFullAlarm();
                    }
                    break;
                }
            case CreateType.Cell:
                {
                    CellDataModel cell = packFactory.CreateCell(_parentModule, (++cellCount).ToString(), _prefab);
                    if (cell != null)
                    {
                        OnCellCreated(cell);
                        DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"Cell 생성 : {cell.cellName} (Module : {cell.parentModule.moduleName})");
                    }
                    else
                    {
                        cellCount--;
                        ShowFullAlarm();
                    }
                    break;
                }
        }
    }
    #endregion
}
