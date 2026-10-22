using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
using Incheol.Modules;
using Incheol.Presenter;
using Incheol.Utils;
using System.Collections.Generic;
using UnityEngine;

public class UI_EditorView : MonoBehaviour
{
    private class TreeNode
    {
        /// <summary>이 노드가 가리키는 Pack / Module / Cell 모델. 항목을 구분하는 키로도 쓴다.</summary>
        public object model;

        public string label;
        public int depth;
        public TreeNode parent;
        public readonly List<TreeNode> childList = new List<TreeNode>();
        public bool expanded;

        /// <summary>이 항목이 가리키는 씬 오브젝트(Pack/Module/Cell). 선택하면 이것이 보이게 된다.</summary>
        public Transform target;

        /// <summary>항목 종류. 선택 시 보이는 Renderer 범위가 종류마다 다르다(Cell은 본인만).</summary>
        public DeviceType kind;
    }

    #region Variable
    [Header("Editor View Variable")]
    [SerializeField, Tooltip("ScrollRect의 Content")] private RectTransform scrollRect;
    [SerializeField, Min(1.0f), Tooltip("트리 한 줄의 높이. UI_TreeNodeItem 프리팹의 높이와 같아야 한다.")] private float rowHeight = 28.0f;

    private readonly List<TreeNode> rootNodeList = new List<TreeNode>();
    private readonly Dictionary<object, TreeNode> nodeDictionary = new Dictionary<object, TreeNode>();

    // 접힌 부모 아래를 뺀, 지금 트리에 보여야 하는 노드를 위에서 아래 순서로 담는다. 목록의 n번째 항목이 이 목록의 n번째 노드다.
    private readonly List<TreeNode> visibleNodeList = new List<TreeNode>();

    private VirtualScrollList<UI_TreeNodeItem> list;
    private GameObject treeItemPrefab;
    private bool loadingPrefab;
    private TreeNode selectedNode;
    #endregion

    /// <summary>줄의 삭제 버튼이 눌렸을 때 발생한다. 인자는 그 줄의 모델(PackDataModel / ModuleDataModel / CellDataModel)이다.</summary>
    public event System.Action<object> DeleteRequested;

    /// <summary>줄이 추가되거나 지워져 트리 구조가 바뀌었을 때 발생한다. 선택된 항목의 자식 구조와 크기 표시를 새로 그리게 한다.</summary>
    public event System.Action TreeChanged;

    /// <summary>항목(줄)을 눌렀을 때 발생한다. 인자는 그 항목의 모델, 항목이 가리키는 씬 오브젝트, 종류이다.</summary>
    public event System.Action<object, Transform, DeviceType> ItemClicked;

    #region LifeCycle
    private void Awake()
    {
        if (scrollRect != null)
        {
            list = new VirtualScrollList<UI_TreeNodeItem>(scrollRect, rowHeight, CreateRow, BindRow);
        }
    }

    private void OnEnable()
    {
        // 꺼진 탭에 있는 트리는 항목이 추가되는 동안 줄을 그리지 못했으므로, 켜질 때 쌓인 항목을 다시 그린다.
        if (rootNodeList.Count > 0)
        {
            RefreshTree();
        }
    }

    private void OnDestroy()
    {
        list?.Dispose();
    }
    #endregion

    #region Method
    /// <summary>_model 항목에 표시되는 이름(예: "Pack 1"). 트리에 없으면 빈 문자열.</summary>
    public string GetLabel(object _model)
    {
        return _model != null && nodeDictionary.TryGetValue(_model, out TreeNode node) ? node.label : string.Empty;
    }

    /// <summary>_model 항목을 선택된 모습(강조)으로 보여준다. null이면 강조를 모두 끈다. 화면 밖의 항목도 보이게 되면 강조된 채로 나타난다.</summary>
    public void SetSelected(object _model)
    {
        selectedNode = _model != null && nodeDictionary.TryGetValue(_model, out TreeNode node) ? node : null;
        list?.Refresh();
    }

    /// <summary>
    /// _model 항목의 Pack / Module / Cell 이름을 _newName으로 바꾼다. 트리 줄 표시는 접두어("Pack " 등)까지 포함해 _newName 그대로가 된다.
    /// 모델 이름과 씬 오브젝트 이름에는 앞에 종류 접두어가 있으면 그것을 뺀 값을 쓴다(저장 후 불러올 때 접두어가 다시 붙기 때문).
    /// 앞뒤 공백을 뺀 이름이 비어 있거나 트리에 없는 항목이면 바꾸지 않고 false를 반환한다.
    /// </summary>
    public bool Rename(object _model, string _newName)
    {
        string newName = _newName != null ? _newName.Trim() : string.Empty;
        if (newName.Length == 0 || _model == null || !nodeDictionary.TryGetValue(_model, out TreeNode node))
        {
            return false;
        }

        string prefix;
        switch (_model)
        {
            case ContainerDataModel _:
                prefix = "Container";
                break;
            case PackDataModel _:
                prefix = "Pack";
                break;
            case ModuleDataModel _:
                prefix = "Module";
                break;
            case CellDataModel _:
                prefix = "Cell";
                break;
            default:
                return false;
        }

        string modelName = newName;
        string prefixWithSpace = prefix + " ";
        if (newName.StartsWith(prefixWithSpace) && newName.Substring(prefixWithSpace.Length).Trim().Length > 0)
        {
            modelName = newName.Substring(prefixWithSpace.Length).Trim();
        }

        switch (_model)
        {
            case ContainerDataModel container:
                container.containerName = modelName;
                break;
            case PackDataModel pack:
                pack.packName = modelName;
                break;
            case ModuleDataModel module:
                module.moduleName = modelName;
                break;
            case CellDataModel cell:
                cell.cellName = modelName;
                break;
        }

        if (node.target != null)
        {
            node.target.name = $"{prefix}_{modelName}";
        }

        node.label = newName;
        list?.Refresh();
        return true;
    }

    /// <summary>
    /// _model(Pack / Module / Cell 모델)의 줄과 그 아래 모든 자식 줄을 트리에서 지운다. 씬 오브젝트와 모델은 건드리지 않는다.
    /// 트리에 없는 모델이면 아무 것도 하지 않는다.
    /// </summary>
    public void RemoveNode(object _model)
    {
        if (_model == null || !nodeDictionary.TryGetValue(_model, out TreeNode node))
        {
            return;
        }

        RemoveSubtree(node);

        if (node.parent != null)
        {
            node.parent.childList.Remove(node);
        }
        else
        {
            rootNodeList.Remove(node);
        }

        RefreshTree();

        TreeChanged?.Invoke();
    }

    /// <summary>_node와 그 자손을 딕셔너리에서 뺀다. 선택된 노드가 포함되어 있으면 선택도 푼다.</summary>
    private void RemoveSubtree(TreeNode _node)
    {
        for (int i = 0; i < _node.childList.Count; i++)
        {
            RemoveSubtree(_node.childList[i]);
        }

        if (selectedNode == _node)
        {
            selectedNode = null;
        }

        nodeDictionary.Remove(_node.model);
    }

    /// <summary>트리의 모든 항목을 지운다(저장된 파일을 불러오기 전에 쓴다).</summary>
    public void Clear()
    {
        nodeDictionary.Clear();
        rootNodeList.Clear();
        selectedNode = null;
        RefreshTree();
    }

    /// <summary>Container를 최상위 항목으로 추가한다. 씬 오브젝트는 _container.containerPosition이다.</summary>
    public void AddContainer(ContainerDataModel _container)
    {
        if (_container == null)
        {
            return;
        }

        AddNode(_container, $"Container {_container.containerName}", null, _container.containerPosition, DeviceType.Container);
    }

    /// <summary>Pack을 그 Container의 자식으로 추가한다. _target은 Pack의 씬 오브젝트다(PackDataModel에는 Transform이 없다).</summary>
    public void AddPack(PackDataModel _pack, Transform _target)
    {
        if (_pack == null)
        {
            return;
        }

        AddNode(_pack, $"Pack {_pack.packName}", _pack.parentContainer, _target, DeviceType.Pack);
    }

    /// <summary>Module을 추가한다. _parentPack이 있으면 그 Pack의 자식, 없으면 그 Module을 담은 Container의 자식이 된다.</summary>
    public void AddModule(ModuleDataModel _module, PackDataModel _parentPack)
    {
        if (_module == null)
        {
            return;
        }

        object parentKey = _parentPack != null ? _parentPack : (object)_module.parentContainer;
        AddNode(_module, $"Module {_module.moduleName}", parentKey, _module.modulePosition, DeviceType.Module);
    }

    /// <summary>Cell을 추가한다. 부모는 cell.parentModule이다.</summary>
    public void AddCell(CellDataModel _cell)
    {
        if (_cell == null)
        {
            return;
        }

        AddNode(_cell, $"Cell {_cell.cellName}", _cell.parentModule, _cell.cellPosition, DeviceType.Cell);
    }

    private void AddNode(object _key, string _label, object _parentKey, Transform _target, DeviceType _kind)
    {
        // 이미 추가된 항목이면 무시한다.
        if (nodeDictionary.ContainsKey(_key))
        {
            return;
        }

        TreeNode parent = null;
        if (_parentKey != null)
        {
            nodeDictionary.TryGetValue(_parentKey, out parent);
        }

        TreeNode node = new TreeNode
        {
            model = _key,
            label = _label,
            depth = parent != null ? parent.depth + 1 : 0,
            parent = parent,
            target = _target,
            kind = _kind,
        };

        nodeDictionary[_key] = node;

        if (parent != null)
        {
            parent.childList.Add(node);
            // 새 자식이 바로 보이도록 부모를 펼친다.
            parent.expanded = true;
        }
        else
        {
            rootNodeList.Add(node);
        }

        RefreshTree();

        // 선택된 항목 아래에 새 자식이 생겼을 수 있으므로 자식 구조 표시를 갱신한다.
        TreeChanged?.Invoke();
    }

    /// <summary>_model 본인을 빼는 자식 구조를 트리 순서대로 (이름, 깊이) 목록으로 돌려준다. 깊이 0이 _model의 직접 자식이다. 트리에 없는 항목이면 빈 목록.</summary>
    public List<(string label, int depth)> GetChildStructure(object _model)
    {
        List<(string label, int depth)> lineList = new List<(string label, int depth)>();
        if (_model != null && nodeDictionary.TryGetValue(_model, out TreeNode node))
        {
            AppendChildren(node, 0, lineList);
        }

        return lineList;
    }

    private static void AppendChildren(TreeNode _node, int _depth, List<(string label, int depth)> _lineList)
    {
        for (int i = 0; i < _node.childList.Count; i++)
        {
            _lineList.Add((_node.childList[i].label, _depth));
            AppendChildren(_node.childList[i], _depth + 1, _lineList);
        }
    }

    /// <summary>항목(줄)을 누르면 선택을 알린다. 선택된 오브젝트만 씬에 보이게 되는 것은 받는 쪽이 정한다.</summary>
    private void OnClickItem(TreeNode _node)
    {
        ItemClicked?.Invoke(_node.model, _node.target, _node.kind);
    }

    private void OnClickArrow(TreeNode _node)
    {
        if (_node.childList.Count == 0)
        {
            return;
        }

        _node.expanded = !_node.expanded;
        RefreshTree();
    }

    /// <summary>접힌 부모 아래를 뺀 보이는 노드 목록을 트리 순서대로 다시 만들고 목록(스크롤)에 반영한다.</summary>
    private void RefreshTree()
    {
        visibleNodeList.Clear();
        for (int i = 0; i < rootNodeList.Count; i++)
        {
            CollectVisible(rootNodeList[i]);
        }

        EnsurePrefab();
        list?.SetCount(visibleNodeList.Count);
    }

    private void CollectVisible(TreeNode _node)
    {
        visibleNodeList.Add(_node);
        if (!_node.expanded)
        {
            return;
        }

        for (int i = 0; i < _node.childList.Count; i++)
        {
            CollectVisible(_node.childList[i]);
        }
    }

    /// <summary>줄 프리팹을 아직 불러오지 않았으면 Addressable로 불러온다. 다 불러오면 목록이 줄 오브젝트를 만들어 채운다.</summary>
    private void EnsurePrefab()
    {
        if (treeItemPrefab != null || loadingPrefab || scrollRect == null)
        {
            return;
        }

        loadingPrefab = true;
        string key = AddressableAssetKey.UI_TreeNodeItem.ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab =>
            {
                loadingPrefab = false;

                // 불러오는 사이 뷰가 파괴되었으면 무시한다.
                if (this == null)
                {
                    return;
                }

                treeItemPrefab = prefab;
                list?.Refresh();
            },
            () =>
            {
                loadingPrefab = false;
                DebugLogManager.GenerateErrorMessage<UI_EditorView>($"{key} 로드 실패");
            });
    }

    /// <summary>VirtualScrollList가 부르는 줄 오브젝트 생성. 프리팹을 아직 못 불러왔으면 null.</summary>
    private UI_TreeNodeItem CreateRow()
    {
        if (treeItemPrefab == null)
        {
            return null;
        }

        GameObject itemObject = AddressableAssetManager.Instance.InstantiatePrefab(treeItemPrefab, scrollRect);
        itemObject.name = "TreeItem";
        return itemObject.GetComponent<UI_TreeNodeItem>();
    }

    /// <summary>VirtualScrollList가 부르는 줄 채우기. 줄 오브젝트는 다른 항목에 쓰였던 것이므로 이름, 들여쓰기, 화살표, 선택 강조, 클릭을 모두 새로 정한다.</summary>
    private void BindRow(UI_TreeNodeItem _item, int _index)
    {
        TreeNode node = visibleNodeList[_index];
        _item.Setup(node.label, node.depth, () => OnClickArrow(node), () => OnClickItem(node), () => DeleteRequested?.Invoke(node.model));
        _item.SetArrow(node.childList.Count > 0, node.expanded);
        _item.SetClicked(node == selectedNode);
    }
    #endregion
}
