using Incheol.Models.Define;
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
    [Header("Tools Button Variable")]
    [SerializeField] private Button newButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private TMP_Dropdown newDropdown;

    [Header("Popup / List")]
    [SerializeField] private UI_AlarmPopupView alarmPopupView;
    [SerializeField] private UI_ConfirmPopupView confirmPopupView;
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
    private JsonDataController jsonDataController;
    private MainSceneManager mainSceneManager;

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

    /// <summary>Pack / Module / Cell을 지우기 직전에 발생한다. 인자는 지워질 오브젝트이다. 선택된 항목이 그 안에 있으면 받는 쪽이 선택을 풀어야 한다.</summary>
    public event System.Action<Transform> ObjectRemoving;

    /// <summary>모두 지우고 다시 만들기 전에 발생한다. 받는 쪽은 현재 선택을 풀어 카메라와 패널을 되돌린다.</summary>
    public event System.Action SelectionClearRequested;

    /// <summary>새 Pack / Module / Cell을 만들어 숨긴 직후 발생한다. 인자는 만들어진 오브젝트이다.</summary>
    public event System.Action<Transform> ObjectCreated;

    #region Method
    /// <summary>MainSceneManager가 프리팹을 인스턴스화한 직후 자신을 넘겨준다. 뷰가 씬에서 직접 찾지 않는다.</summary>
    public void Init(MainSceneManager _manager)
    {
        mainSceneManager = _manager;
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
    private void OnDeleteRequested(object _model)
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
        if (confirmPopupView == null)
        {
            DebugLogManager.GenerateErrorMessage<UI_MainSceneView>("UI_ConfirmPopupView를 찾지 못해 삭제하지 않았습니다.");
            return;
        }

        confirmPopupView.Show($"{description} 삭제하시겠습니까?\n삭제하면 되돌릴 수 없습니다.", () => DeleteModel(_model));
    }

    private void DeleteModel(object _model)
    {
        Transform removedTransform = null;
        ContainerDataModel parentContainer = null;
        switch (_model)
        {
            case ContainerDataModel container:
                removedTransform = container.containerPosition;
                break;
            case PackDataModel pack:
                removedTransform = packFactory.GetPackTransform(pack);
                parentContainer = pack.parentContainer;
                break;
            case ModuleDataModel module:
                removedTransform = module.modulePosition;
                parentContainer = module.parentContainer;
                break;
            case CellDataModel cell:
                removedTransform = cell.cellPosition;
                break;
        }

        if (removedTransform == null)
        {
            return;
        }

        ObjectRemoving?.Invoke(removedTransform);

        bool removed = false;
        switch (_model)
        {
            case ContainerDataModel container:
                removed = packFactory.RemoveContainer(container);
                break;
            case PackDataModel pack:
                removed = packFactory.RemovePack(pack);
                break;
            case ModuleDataModel module:
                removed = packFactory.RemoveModule(module);
                break;
            case CellDataModel cell:
                removed = packFactory.RemoveCell(cell);
                break;
        }

        if (removed)
        {
            editorView?.RemoveNode(_model);

            // 마지막 Pack / 단독 Module을 지워 비게 된 Container는 함께 지운다.
            if (parentContainer != null && parentContainer.packsList.Count == 0 && parentContainer.modulesList.Count == 0)
            {
                ObjectRemoving?.Invoke(parentContainer.containerPosition);
                packFactory.RemoveContainer(parentContainer);
                editorView?.RemoveNode(parentContainer);
            }

            DebugLogManager.GenerateLogMessage<UI_MainSceneView>("삭제 완료");
        }
    }

    /// <summary>saveButton은 지금까지 생성된 Pack / Module / Cell을 Json 파일로 저장하고 결과를 알람 팝업으로 알려준다.</summary>
    private void OnClickSaveButton()
    {
        if (jsonDataController == null)
        {
            jsonDataController = FindAnyObjectByType<JsonDataController>();
            if (jsonDataController == null)
            {
                jsonDataController = gameObject.AddComponent<JsonDataController>();
            }
        }

        bool saved = jsonDataController.Save(packFactory);
        alarmPopupView?.Show(saved ? "저장되었습니다." : "저장할 Pack / Module이 없거나 저장에 실패했습니다.");
    }

    /// <summary>loadButton은 저장된 Json 파일 목록을 JsonFileListContainer의 ScrollView에 보여준다.</summary>
    private void OnClickLoadButton()
    {
        jsonFileListView?.Show();
    }

    /// <summary>목록에서 Json 파일을 누르면 현재 Pack / Module / Cell을 모두 지우고 그 파일의 내용으로 다시 만든다.</summary>
    private void OnJsonFileClicked(string _path)
    {
        JsonDataController.SaveJson data = JsonDataController.Load(_path);
        if (data == null)
        {
            alarmPopupView?.Show("Json 파일을 읽지 못했습니다.");
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

        editorView?.Clear();
        packFactory.Clear();

        // newButton으로 만들 때와 같은 여백 설정을 쓴다.
        packFactory.PackPadding = packPadding;
        packFactory.ModulePadding = modulePadding;
        packFactory.CellPadding = cellPadding;

        PackJsonBuilder builder = new PackJsonBuilder(
            packFactory,
            container => editorView?.AddContainer(container),
            (pack, packTransform) =>
            {
                HideOrShowCreated(packTransform);
                editorView?.AddPack(pack, packTransform);
            },
            (module, parentPack) =>
            {
                HideOrShowCreated(module.modulePosition);
                editorView?.AddModule(module, parentPack);
            },
            cell =>
            {
                HideOrShowCreated(cell.cellPosition);
                editorView?.AddCell(cell);
            });

        PackJsonBuilder.Result result = builder.Build(_data, _packPrefab, _modulePrefab, _cellPrefab);
        containerCount = result.containerCount;
        packCount = result.packCount;
        moduleCount = result.moduleCount;
        cellCount = result.cellCount;
        skippedCount = result.skippedCount;

        DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"Json 불러오기 완료 : Pack {packFactory.Packs.Count}개, Module {packFactory.Modules.Count}개, 건너뜀 {skippedCount}개");
        alarmPopupView?.Show(skippedCount == 0 ? "불러왔습니다." : $"불러왔지만 공간이 없어 {skippedCount}개는 만들지 못했습니다.");
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

        if (mainSceneManager == null || mainSceneManager.SelectedModel == null || mainSceneManager.SelectedTarget == null)
        {
            return true;
        }

        Transform selectedTarget = mainSceneManager.SelectedTarget;
        switch (mainSceneManager.SelectedKind)
        {
            case MainSceneManager.TargetKind.Container:
                {
                    ContainerDataModel selectedContainer = packFactory.FindContainer(selectedTarget);
                    if (selectedContainer == null)
                    {
                        break;
                    }

                    // 선택한 Container 안에 Pack을, Module은 하위 Pack 안에(Pack이 없으면 Pack 없는 Module로) 만든다.
                    if (_type == CreateType.Pack)
                    {
                        if (mainSceneManager != null && !mainSceneManager.CanCreatePack(selectedContainer))
                        {
                            mainSceneManager.TryGetMaxPackCount(selectedContainer, out int maxPackCount);
                            string message = $"Container {selectedContainer.containerName}에는 Pack을 최대 {maxPackCount}개까지만 만들 수 있습니다.";
                            DebugLogManager.GenerateErrorMessage<UI_MainSceneView>(message);
                            alarmPopupView?.Show(message);
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
            case MainSceneManager.TargetKind.Pack:
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
            case MainSceneManager.TargetKind.Module:
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
            case MainSceneManager.TargetKind.Cell:
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


    /// <summary>
    /// 새로 만든 _created는 기본적으로 숨긴다. 단, 현재 선택된 Pack / Module 아래에 생성된 것이면
    /// 생성되는 모습이 바로 보이도록 ObjectCreated를 받는 쪽이 다시 켜준다.
    /// </summary>
    private void HideOrShowCreated(Transform _created)
    {
        MainSceneManager.SetVisible(_created, false);

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

        alarmPopupView?.Show(message);
    }

    /// <summary>_container가 있으면 그대로, 없으면 새 Container를 만들어 트리에 추가해 돌려준다.</summary>
    private ContainerDataModel EnsureContainer(ContainerDataModel _container)
    {
        if (_container != null)
        {
            return _container;
        }

        ContainerDataModel container = packFactory.CreateContainer((++containerCount).ToString());
        editorView?.AddContainer(container);
        return container;
    }

    /// <summary>_parentPack / _parentModule / _parentContainer는 클릭 시점에 ResolveParent로 정해진 부모다(프리폹 로드를 기다리는 사이 선택이 바뀔어도 그대로 쓴다).</summary>
    private void OnPrefabLoaded(CreateType _type, GameObject _prefab, PackDataModel _parentPack, ModuleDataModel _parentModule, ContainerDataModel _parentContainer)
    {
        switch (_type)
        {
            case CreateType.Pack:
                {
                    // 선택한 Container가 없으면 이 Pack을 담을 새 Container를 만든다.
                    ContainerDataModel container = EnsureContainer(_parentContainer);
                    packFactory.PackPadding = packPadding;
                    PackDataModel pack = packFactory.CreatePack((++packCount).ToString(), _prefab, default, container);
                    Transform packTransform = packFactory.GetPackTransform(pack);
                    HideOrShowCreated(packTransform);
                    editorView?.AddPack(pack, packTransform);
                    DebugLogManager.GenerateLogMessage<UI_MainSceneView>($"Pack 생성 : {pack.packName}");
                    break;
                }
            case CreateType.Module:
                {
                    // 부모 Pack이 있으면 그 Pack에 속하고, 없으면 단독 Module로 만든다.
                    packFactory.ModulePadding = modulePadding;
                    ContainerDataModel container = _parentPack == null ? EnsureContainer(_parentContainer) : null;
                    ModuleDataModel module = packFactory.CreateModule(_parentPack, (++moduleCount).ToString(), _prefab, default, container);
                    if (module != null)
                    {
                        HideOrShowCreated(module.modulePosition);
                        editorView?.AddModule(module, _parentPack);
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
                    packFactory.CellPadding = cellPadding;
                    CellDataModel cell = packFactory.CreateCell(_parentModule, (++cellCount).ToString(), _prefab);
                    if (cell != null)
                    {
                        HideOrShowCreated(cell.cellPosition);
                        editorView?.AddCell(cell);
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
