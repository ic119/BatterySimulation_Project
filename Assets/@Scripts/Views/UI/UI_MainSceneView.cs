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
    [SerializeField, Min(0.0f), Tooltip("Pack 사이의 가로 여백.")] private float packPadding = 0.0f;

    [Header("ModuleAssembly Layout")]
    [SerializeField, Min(0.0f), Tooltip("ModuleAssembly 사이(가로/깊이)의 여백. 칸 가운데에 놓여 Pack 벽과도 여백의 절반만큼 떨어진다.")] private float moduleAssemblyPadding = 0.0f;

    [Header("Module Layout")]
    [SerializeField, Min(0.0f), Tooltip("Module 사이(가로/깊이)의 여백. 칸 가운데에 놓여 ModuleAssembly 벽과도 여백의 절반만큼 떨어진다.")] private float modulePadding = 0.0f;

    [Header("ParallelAssembly Layout")]
    [SerializeField, Min(0.0f), Tooltip("ParallelAssembly 사이(가로/깊이)의 여백. 칸 가운데에 놓여 Module 벽과도 여백의 절반만큼 떨어진다.")] private float parallelAssemblyPadding = 0.0f;

    [Header("Cell Layout")]
    [SerializeField, Min(0.0f), Tooltip("Cell 사이(가로/깊이)의 여백. Cell은 칸 가운데에 놓여 벽과도 여백의 절반만큼 떨어진다. 클수록 ParallelAssembly에 들어가는 Cell 개수가 줄어든다.")] private float cellPadding = 0.0f;

    private static readonly DeviceType[] DropdownDeviceTypes =
    {
        DeviceType.Pack,
        DeviceType.ModuleAssembly,
        DeviceType.Module,
        DeviceType.ParallelAssembly,
        DeviceType.Cell,
    };

    private readonly PackFactory packFactory = new PackFactory();
    private readonly CellHandler cellHandler = new CellHandler();

    // 같은 루트(UI_MainScene)에 붙어 있는 트리 뷰. 생성된 장치를 여기에 알린다.
    private UI_EditorView editorView;
    private DeviceSelection selection;

    // 종류별로 마지막에 붙인 이름 번호. 이름이 "3"처럼 번호 문자열이다. 삭제해도 줄이지 않아 이름이 겹치지 않는다.
    private readonly Dictionary<DeviceType, int> countDictionary = new Dictionary<DeviceType, int>();
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

    /// <summary>모든 장치를 만들고 등록부를 들고 있는 팩토리.</summary>
    public PackFactory PackFactory => packFactory;

    /// <summary>EditorTab의 트리 뷰(이 루트에 붙어 있다).</summary>
    public UI_EditorView EditorTreeView => editorView;

    /// <summary>SimulationTab의 트리 뷰. 없으면 null.</summary>
    public UI_EditorView SimulationTreeView => simulationTreeView;

    /// <summary>장치를 지우기 직전에 발생한다. 인자는 지워질 오브젝트이다. 선택된 항목이 그 안에 있으면 받는 쪽이 선택을 풀어야 한다.</summary>
    public event System.Action<Transform> ObjectRemoving;

    /// <summary>모두 지우고 다시 만들기 전에 발생한다. 받는 쪽은 현재 선택을 풀어 카메라와 패널을 되돌린다.</summary>
    public event System.Action SelectionClearRequested;

    /// <summary>새 장치를 만들어 숨긴 직후 발생한다. 인자는 만들어진 오브젝트이다.</summary>
    public event System.Action<Transform> ObjectCreated;

    /// <summary>CreatePack(true)로 만든 Pack이 완성되었을 때 발생한다. 받는 쪽이 그 Pack을 선택해 보이게 한다. 인자는 만들어진 Pack이다.</summary>
    public event System.Action<IDeviceNode> PackSelectRequested;

    #region Method
    // 트리는 EditorTab의 UI_EditorView가 기준이고, SimulationTab의 트리(simulationTreeView)에는 같은 변경을 그대로 따라 한다.
    private void AddDeviceNode(IDeviceNode _node)
    {
        editorView?.AddDevice(_node);
        simulationTreeView?.AddDevice(_node);
    }

    private void RemoveTreeNode(IDeviceNode _model)
    {
        editorView?.RemoveNode(_model);
        simulationTreeView?.RemoveNode(_model);
    }

    /// <summary>
    /// 만들어 둔 모든 장치(씬 오브젝트와 모델), 두 탭의 트리, 선택, 번호 이어 붙이기를 모두 처음 상태로 되돌린다.
    /// 탭을 바꿀 때 쓴다. 저장하지 않은 내용은 사라진다.
    /// </summary>
    public void ResetAll()
    {
        // 지울 오브젝트가 선택되어 있으면 먼저 선택을 풀어 카메라와 패널을 되돌린다.
        SelectionClearRequested?.Invoke();

        ClearTrees();
        packFactory.Clear();

        countDictionary.Clear();
        skippedCount = 0;
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

    /// <summary>MainSceneManager가 프리팹을 인스턴스화한 직후 선택 상태를 넘겨준다. 뷰가 씬에서 직접 찾지 않는다.</summary>
    public void Init(DeviceSelection _selection)
    {
        selection = _selection;
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
        // Dropdown에서 선택된 종류를 생성한다. Dropdown이 없으면 Pack.
        DeviceType type = DeviceType.Pack;
        if (newDropdown != null && newDropdown.value >= 0 && newDropdown.value < DropdownDeviceTypes.Length)
        {
            type = DropdownDeviceTypes[newDropdown.value];
        }

        CreateObject(type);
    }

    /// <summary>
    /// 트리 줄의 삭제 버튼이 눌리면 해당 장치를 (자식까지) 씬, 데이터 모델, 트리에서 지운다.
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
        if (_model == null)
        {
            return;
        }

        string description = DeviceHierarchy.TryGetChildKind(_model.Kind, out DeviceType childKind)
            ? $"{_model.Kind} {_model.Name}과(와) 그 안의 모든 하위 장치를"
            : $"{_model.Kind} {_model.Name}을(를)";

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

        ObjectRemoving?.Invoke(removedTransform);

        if (packFactory.Remove(_model))
        {
            RemoveTreeNode(_model);
            DebugLogManager.GenerateLogMessage<UI_MainSceneView>("삭제 완료");
        }
    }

    /// <summary>saveButton은 지금까지 생성된 장치를 Json 파일로 저장하고 결과를 알람 팝업으로 알려준다.</summary>
    private void OnClickSaveButton()
    {
        bool saved = JsonDataController.Save(packFactory);
        ShowAlarm(saved ? "저장되었습니다." : "저장할 Pack이 없거나 저장에 실패했습니다.");
    }

    /// <summary>loadButton은 저장된 Json 파일 목록을 JsonFileListContainer의 ScrollView에 보여준다.</summary>
    private void OnClickLoadButton()
    {
        jsonFileListView?.Show();
    }

    /// <summary>목록에서 Json 파일을 누르면 현재 장치를 모두 지우고 그 파일의 내용으로 다시 만든다.</summary>
    private void OnJsonFileClicked(string _path)
    {
        LoadFromFile(_path);
    }

    /// <summary>_path의 Json으로 모든 장치를 다시 만든다. Editor 탭과 Simulation 탭의 Load가 같이 쓴다.</summary>
    public void LoadFromFile(string _path)
    {
        JsonDataController.SaveJson data = JsonDataController.Load(_path);
        if (data == null)
        {
            ShowAlarm("Json 파일을 읽지 못했습니다.");
            return;
        }

        // 위 계층부터 아래 계층 순서로 프리팹을 모두 불러온 뒤 한 번에 만든다.
        LoadPrefabsFrom(0, new Dictionary<DeviceType, GameObject>(), prefabs => BuildFromJson(data, prefabs));
    }

    /// <summary>
    /// 이미 읽은 저장 데이터(React가 보낸 것 등)로 모든 장치를 다시 만든다. 프리팹을 불러온 뒤 만들고, 다 만들면 _onBuilt를 부른다.
    /// 프리팹 로드가 실패하면 _onBuilt는 불리지 않는다.
    /// </summary>
    public void LoadFromSaveJson(JsonDataController.SaveJson _data, System.Action _onBuilt = null)
    {
        if (_data == null)
        {
            ShowAlarm("불러올 데이터가 없습니다.");
            return;
        }

        LoadPrefabsFrom(0, new Dictionary<DeviceType, GameObject>(), prefabs =>
        {
            BuildFromJson(_data, prefabs);
            _onBuilt?.Invoke();
        });
    }

    /// <summary>DropdownDeviceTypes[_index]부터 끝까지 프리팹을 차례로 불러와 _prefabs에 담고, 다 불러오면 _done을 부른다. 하나라도 실패하면 로그만 남기고 멈춘다.</summary>
    private void LoadPrefabsFrom(int _index, Dictionary<DeviceType, GameObject> _prefabs, System.Action<Dictionary<DeviceType, GameObject>> _done)
    {
        if (_index >= DropdownDeviceTypes.Length)
        {
            _done(_prefabs);
            return;
        }

        DeviceType type = DropdownDeviceTypes[_index];
        string key = GetPrefabKey(type).ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab =>
            {
                _prefabs[type] = prefab;
                LoadPrefabsFrom(_index + 1, _prefabs, _done);
            },
            () => DebugLogManager.GenerateErrorMessage<UI_MainSceneView>($"{key} 로드 실패"));
    }

    /// <summary>
    /// 지금 있는 것을 모두 지우고 _data의 장치를 newButton으로 만들 때와 같은 방식으로 다시 만든다.
    /// 만드는 일은 PackJsonBuilder가 하고, 이 뷰는 만들어진 것을 숨겨 두고 트리에 추가하며 번호를 이어 붙인다.
    /// </summary>
    private void BuildFromJson(JsonDataController.SaveJson _data, Dictionary<DeviceType, GameObject> _prefabs)
    {
        // 지울 오브젝트가 선택되어 있으면 먼저 선택을 풀어 카메라와 패널을 되돌린다.
        SelectionClearRequested?.Invoke();

        ClearTrees();
        packFactory.Clear();

        // newButton으로 만들 때와 같은 여백 설정을 쓴다.
        ApplyPadding();

        PackJsonBuilder builder = new PackJsonBuilder(packFactory, OnNodeCreated);
        PackJsonBuilder.Result result = builder.Build(_data, _prefabs);

        countDictionary.Clear();
        countDictionary[DeviceType.Pack] = result.packCount;
        countDictionary[DeviceType.ModuleAssembly] = result.moduleAssemblyCount;
        countDictionary[DeviceType.Module] = result.moduleCount;
        countDictionary[DeviceType.ParallelAssembly] = result.parallelAssemblyCount;
        countDictionary[DeviceType.Cell] = result.cellCount;
        skippedCount = result.skippedCount;

        DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"Json 불러오기 완료 : Pack {packFactory.GetNodes(DeviceType.Pack).Count}개, Module {packFactory.GetNodes(DeviceType.Module).Count}개, 건너뜀 {skippedCount}개");
        ShowAlarm(skippedCount == 0 ? "불러왔습니다." : $"불러왔지만 공간이 없어 {skippedCount}개는 만들지 못했습니다.");
    }

    private void BuildDropdownOptions()
    {
        List<string> optionList = new List<string>();
        for (int i = 0; i < DropdownDeviceTypes.Length; i++)
        {
            optionList.Add(DropdownDeviceTypes[i].ToString());
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

    /// <summary>Dropdown 옵션 항목이 클릭되면 UI_DropdownItemRelay가 호출해 해당 종류를 생성한다.</summary>
    public void OnDropdownItemClicked(string _optionText)
    {
        if (System.Enum.TryParse(_optionText, out DeviceType type))
        {
            CreateObject(type);
        }
    }

    private static AddressableAssetKey GetPrefabKey(DeviceType _type)
    {
        switch (_type)
        {
            case DeviceType.Pack: return AddressableAssetKey.PackPrefab;
            case DeviceType.ModuleAssembly: return AddressableAssetKey.ModuleAssemblyPrefab;
            case DeviceType.Module: return AddressableAssetKey.ModulePrefab;
            case DeviceType.ParallelAssembly: return AddressableAssetKey.ParallelAssemblyPrefab;
            default: return AddressableAssetKey.Cell_PouchType;
        }
    }

    /// <summary>생성할 종류 사이의 여백 설정을 PackFactory에 넘긴다. 만들기 직전에 부른다.</summary>
    private void ApplyPadding()
    {
        packFactory.SetPadding(DeviceType.Pack, packPadding);
        packFactory.SetPadding(DeviceType.ModuleAssembly, moduleAssemblyPadding);
        packFactory.SetPadding(DeviceType.Module, modulePadding);
        packFactory.SetPadding(DeviceType.ParallelAssembly, parallelAssemblyPadding);
        packFactory.SetPadding(DeviceType.Cell, cellPadding);
    }

    /// <summary>
    /// 새로 만들어진 _node를 숨기고 두 탭의 트리에 추가한다. 직접 만들 때와 Json에서 불러올 때 같이 쓴다.
    /// 새로 만든 것은 기본적으로 숨긴다. 단, 현재 선택된 장치 아래에 생성된 것이면
    /// 생성되는 모습이 바로 보이도록 ObjectCreated를 받는 쪽이 다시 켜준다.
    /// </summary>
    private void OnNodeCreated(IDeviceNode _node)
    {
        DeviceVisibility.SetVisible(_node.Position, false);
        ObjectCreated?.Invoke(_node.Position);
        AddDeviceNode(_node);
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

    /// <summary>Pack을 만든다(아래 ModuleAssembly > Module > ParallelAssembly > Cell도 하나씩 함께 만든다). 생성 버튼, 단축키가 같이 쓴다.</summary>
    public void CreatePack(bool _selectCreated = false)
    {
        CreateObject(DeviceType.Pack, _selectCreated);
    }

    /// <summary>Addressable의 프리팹을 불러와 _type 종류를 생성한다. 선택된 항목이 있으면 그 항목의 하위에 만든다.</summary>
    private void CreateObject(DeviceType _type, bool _selectCreated = false)
    {
        // 클릭 시점의 선택으로 부모를 정한다. 만들 수 없는 조합이면 불필요한 로드를 하지 않고 끝낸다.
        CreateParentResolver.Result resolved = CreateParentResolver.Resolve(_type, selection, packFactory);
        if (!resolved.success)
        {
            DebugLogManager.GenerateErrorMessage<UI_MainSceneView>(resolved.message);
            if (resolved.showAlarm)
            {
                ShowAlarm(resolved.message);
            }

            return;
        }

        // Pack은 만들 때 ModuleAssembly > Module > ParallelAssembly > Cell을 하나씩 함께 만들므로 5종 프리팹을 모두 불러온다.
        if (_type == DeviceType.Pack)
        {
            LoadPrefabsFrom(0, new Dictionary<DeviceType, GameObject>(), prefabs => CreatePackWithChildren(prefabs, _selectCreated));
            return;
        }

        string key = GetPrefabKey(_type).ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab => OnPrefabLoaded(_type, prefab, resolved.parent),
            () => DebugLogManager.GenerateErrorMessage<UI_MainSceneView>($"{key} 로드 실패"));
    }

    /// <summary>
    /// Pack을 만들고 그 아래에 ModuleAssembly > Module > ParallelAssembly > Cell을 하나씩 차례로 만든다(저장된 파일을 불러올 때는 쓰지 않는다).
    /// 중간 단계가 부모 안에 들어가지 않아 만들지 못하면 거기서 멈추고, 이미 만든 윗 단계는 그대로 둔다.
    /// </summary>
    private void CreatePackWithChildren(Dictionary<DeviceType, GameObject> _prefabs, bool _selectCreated)
    {
        ApplyPadding();

        IDeviceNode pack = null;
        IDeviceNode parent = null;
        for (int i = 0; i < DropdownDeviceTypes.Length; i++)
        {
            DeviceType type = DropdownDeviceTypes[i];
            countDictionary.TryGetValue(type, out int count);

            IDeviceNode node = packFactory.Create(type, parent, (count + 1).ToString(), _prefabs[type]);
            if (node == null)
            {
                ShowFullAlarm();
                break;
            }

            countDictionary[type] = count + 1;
            OnNodeCreated(node);
            DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"{type} 생성 : {node.Name}" + (parent != null ? $" ({parent.Kind} : {parent.Name})" : string.Empty));
            parent = node;
            pack ??= node;
        }

        // 중간에 멈췄더라도 Pack이 만들어졌으면 선택해 보이게 한다.
        if (_selectCreated && pack != null)
        {
            PackSelectRequested?.Invoke(pack);
        }
    }

    /// <summary>
    /// 기준 Cell 옆에 새 Cell을 하나 만든다. _direction은 월드 +x(Vector3.right) 또는 +z(Vector3.forward)다.
    /// 기준 Cell은 선택된 Cell이 있으면 그것, 없으면 가장 최근에 만든 Cell이고, 새 Cell은 기준 Cell과 같은 부모 아래에 같은 크기로 만든다.
    /// 그 방향의 바로 옆자리가 이미 차 있으면 같은 방향으로 한 칸씩 더 가서 첫 빈 자리에 놓고, 부모 몸체 안에 빈 자리가 없으면 만들지 않고 알람으로 알린다.
    /// 기본 칸 채우기(뒤쪽 -z, 다음 줄 +x)와 달리 방향을 직접 정하는 생성이다. 배치 공식과 생성은 CellHandler.StackingXAxis(+x) / StackingZAxis(+z)가 맡는다.
    /// 부모에 몸체(Renderer)가 없으면(예: ParallelAssembly) 내부 공간 검사는 건너뛰고, 일정한 간격으로 나열한다. x는 Body끼리 맞닿는 간격(중심 간격 = Cell x 크기), z는 Tag(PlusTag / MinusTag)끼리 맞닿는 간격(중심 간격 = Tag까지 포함한 Cell 길이)이다.
    /// </summary>
    public void CreateCellBeside(Vector3 _direction)
    {
        int axis;
        if (_direction.x > 0.5f)
        {
            axis = 0;
        }
        else if (_direction.z > 0.5f)
        {
            axis = 2;
        }
        else
        {
            DebugLogManager.GenerateErrorMessage<UI_MainSceneView>($"CreateCellBeside는 +x 또는 +z 방향만 지원합니다 : {_direction}");
            return;
        }

        IDeviceNode reference = selection != null && selection.Model != null && selection.Model.Kind == DeviceType.Cell
            ? selection.Model
            : packFactory.GetLast(DeviceType.Cell);
        if (reference == null || reference.Position == null || reference.Parent == null)
        {
            DebugLogManager.GenerateWarningMessage<UI_MainSceneView>("CreateCellBeside : 기준이 될 Cell이 없습니다(Pack 생성은 프리팹을 불러오는 동안 비동기라, 생성 직후에는 아직 없을 수 있습니다).");
            ShowAlarm("기준이 될 Cell이 없습니다. 먼저 Cell을 만들어 주세요.");
            return;
        }

        string key = GetPrefabKey(DeviceType.Cell).ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab => OnCellBesidePrefabLoaded(prefab, reference, axis),
            () => DebugLogManager.GenerateErrorMessage<UI_MainSceneView>($"{key} 로드 실패"));
    }

    /// <summary>
    /// 기준 Cell(_reference) 옆에 Cell을 하나 쌓는다. _axis가 0이면 CellHandler.StackingXAxis(+x), 2이면 StackingZAxis(+z)다.
    /// 배치 공식과 생성은 CellHandler가 맡고, 이 뷰는 이름 번호, 트리 등록, 실패 안내(알람)만 맡는다.
    /// 프리팹을 기다리는 사이 기준 Cell이 지워졌을 수 있는데, 그 확인도 CellHandler가 한다.
    /// </summary>
    private void OnCellBesidePrefabLoaded(GameObject _prefab, IDeviceNode _reference, int _axis)
    {
        ApplyPadding();

        countDictionary.TryGetValue(DeviceType.Cell, out int count);
        string name = (count + 1).ToString();

        string failMessage;
        IDeviceNode node = _axis == 0
            ? cellHandler.StackingXAxis(packFactory, _reference, _prefab, name, cellPadding, out failMessage)
            : cellHandler.StackingZAxis(packFactory, _reference, _prefab, name, cellPadding, out failMessage);
        if (node == null)
        {
            ShowAlarm(failMessage);
            return;
        }

        countDictionary[DeviceType.Cell] = count + 1;
        OnNodeCreated(node);
        DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"Cell 생성({(_axis == 0 ? "+x" : "+z")} 방향) : {node.Name} ({node.Parent.Kind} : {node.Parent.Name})");
    }

    /// <summary>_parent는 클릭 시점에 CreateParentResolver로 정해진 부모다(프리팹 로드를 기다리는 사이 선택이 바뀌어도 그대로 쓴다).</summary>
    private void OnPrefabLoaded(DeviceType _type, GameObject _prefab, IDeviceNode _parent)
    {
        ApplyPadding();

        countDictionary.TryGetValue(_type, out int count);
        string name = (count + 1).ToString();

        IDeviceNode node = packFactory.Create(_type, _parent, name, _prefab);
        if (node == null)
        {
            ShowFullAlarm();
            return;
        }

        countDictionary[_type] = count + 1;
        OnNodeCreated(node);
        DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"{_type} 생성 : {node.Name}" + (_parent != null ? $" ({_parent.Kind} : {_parent.Name})" : string.Empty));
    }
    #endregion
}
