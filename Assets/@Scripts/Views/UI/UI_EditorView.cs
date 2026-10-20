using Incheol.Models.Define;
using Incheol.Modules;
using Incheol.Presenter;
using Incheol.Utils;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 생성된 Pack / Module / Cell을 scrollRect(ScrollRect의 Content) 아래에 트리 구조로 보여준다.
/// 각 항목은 Addressable 프리팹(UI_TreeNodeItem)이며, 자식이 있는 항목은 화살표 버튼으로 자식들을 펼치거나(▼) 접는다(▶).
/// </summary>
public class UI_EditorView : MonoBehaviour
{
    private class TreeNode
    {
        public int depth;
        public TreeNode parent;
        public readonly List<TreeNode> childList = new List<TreeNode>();
        public bool expanded;
        public UI_TreeNodeItem item;

        /// <summary>이 항목이 가리키는 씬 오브젝트(Pack/Module/Cell). 선택하면 이것이 보이게 된다.</summary>
        public Transform target;

        /// <summary>항목 종류. 선택 시 보이는 Renderer 범위가 종류마다 다르다(Cell은 본인만).</summary>
        public MainSceneManager.TargetKind kind;
    }

    #region Variable
    [Header("Editor View Variable")]
    [SerializeField] private RectTransform scrollRect;

    private MainSceneManager mainSceneManager;

    private readonly List<TreeNode> rootNodeList = new List<TreeNode>();
    private readonly Dictionary<object, TreeNode> nodeDictionary = new Dictionary<object, TreeNode>();
    #endregion

    /// <summary>줄의 삭제 버튼이 눌렸을 때 발생한다. 인자는 그 줄의 모델(PackDataModel / ModuleDataModel / CellDataModel)이다.</summary>
    public event System.Action<object> DeleteRequested;

    #region Method
    /// <summary>
    /// _item 줄이 가리키는 Pack / Module / Cell의 이름을 _newName으로 바꾼다. 트리 줄 표시는 접두어("Pack " 등)까지 포함해 _newName 그대로가 된다.
    /// 모델 이름과 씬 오브젝트 이름에는 앞에 종류 접두어가 있으면 그것을 뺀 값을 쓴다(저장 후 불러올 때 접두어가 다시 붙기 때문).
    /// 앞뒤 공백을 뺀 이름이 비어 있거나 트리에 없는 줄이면 바꾸지 않고 false를 반환한다.
    /// </summary>
    public bool Rename(UI_TreeNodeItem _item, string _newName)
    {
        string newName = _newName != null ? _newName.Trim() : string.Empty;
        if (newName.Length == 0 || !TryFindNode(_item, out object model, out TreeNode node))
        {
            return false;
        }

        string prefix;
        switch (model)
        {
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

        switch (model)
        {
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

        node.item.SetLabel(newName);
        node.item.gameObject.name = $"TreeItem_{newName}";
        return true;
    }

    private bool TryFindNode(UI_TreeNodeItem _item, out object _model, out TreeNode _node)
    {
        _model = null;
        _node = null;
        if (_item == null)
        {
            return false;
        }

        foreach (KeyValuePair<object, TreeNode> pair in nodeDictionary)
        {
            if (pair.Value.item == _item)
            {
                _model = pair.Key;
                _node = pair.Value;
                return true;
            }
        }

        return false;
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
            UpdateArrow(node.parent);
        }
        else
        {
            rootNodeList.Remove(node);
        }

        RefreshTree();

        if (mainSceneManager == null)
        {
            mainSceneManager = FindAnyObjectByType<MainSceneManager>();
        }

        mainSceneManager?.RefreshStructure();
        mainSceneManager?.RefreshProperties();
    }

    /// <summary>_node와 그 자손의 줄 오브젝트를 파괴하고 딕셔너리에서 뺀다.</summary>
    private void RemoveSubtree(TreeNode _node)
    {
        for (int i = 0; i < _node.childList.Count; i++)
        {
            RemoveSubtree(_node.childList[i]);
        }

        if (_node.item != null)
        {
            _node.item.gameObject.SetActive(false);
            Destroy(_node.item.gameObject);
        }

        object key = null;
        foreach (KeyValuePair<object, TreeNode> pair in nodeDictionary)
        {
            if (pair.Value == _node)
            {
                key = pair.Key;
                break;
            }
        }

        if (key != null)
        {
            nodeDictionary.Remove(key);
        }
    }

    /// <summary>트리의 모든 항목을 지운다(저장된 파일을 불러오기 전에 쓴다).</summary>
    public void Clear()
    {
        foreach (TreeNode node in nodeDictionary.Values)
        {
            if (node.item != null)
            {
                Destroy(node.item.gameObject);
            }
        }

        nodeDictionary.Clear();
        rootNodeList.Clear();
    }

    /// <summary>Pack을 최상위 항목으로 추가한다. _target은 Pack의 씬 오브젝트다(PackDataModel에는 Transform이 없다).</summary>
    public void AddPack(PackDataModel _pack, Transform _target)
    {
        if (_pack == null)
        {
            return;
        }

        RequestNode(_pack, $"Pack {_pack.packName}", null, _target, MainSceneManager.TargetKind.Pack);
    }

    /// <summary>Module을 추가한다. _parentPack이 있으면 그 Pack의 자식, 없으면 최상위 항목이 된다.</summary>
    public void AddModule(ModuleDataModel _module, PackDataModel _parentPack)
    {
        if (_module == null)
        {
            return;
        }

        RequestNode(_module, $"Module {_module.moduleName}", _parentPack, _module.modulePosition, MainSceneManager.TargetKind.Module);
    }

    /// <summary>Cell을 추가한다. 부모는 cell.parentModule이다.</summary>
    public void AddCell(CellDataModel _cell)
    {
        if (_cell == null)
        {
            return;
        }

        RequestNode(_cell, $"Cell {_cell.cellName}", _cell.parentModule, _cell.cellPosition, MainSceneManager.TargetKind.Cell);
    }

    /// <summary>
    /// 항목 프리팹을 Addressable로 불러와 노드를 만든다. 첫 로드 중에 들어온 요청도 요청 순서대로 처리되므로
    /// 부모는 항상 자식보다 먼저 만들어진다. 부모는 콜백 시점에 찾는다.
    /// </summary>
    private void RequestNode(object _key, string _label, object _parentKey, Transform _target, MainSceneManager.TargetKind _kind)
    {
        if (scrollRect == null)
        {
            return;
        }

        string key = AddressableAssetKey.UI_TreeNodeItem.ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab => CreateNode(prefab, _key, _label, _parentKey, _target, _kind),
            () => DebugLogManager.GenerateErrorMessage<UI_EditorView>($"{key} 로드 실패"));
    }

    private void CreateNode(GameObject _prefab, object _key, string _label, object _parentKey, Transform _target, MainSceneManager.TargetKind _kind)
    {
        // 로드를 기다리는 사이 뷰가 파괴되었거나 이미 추가된 항목이면 무시한다.
        if (this == null || scrollRect == null || nodeDictionary.ContainsKey(_key))
        {
            return;
        }

        TreeNode parent = null;
        if (_parentKey != null)
        {
            nodeDictionary.TryGetValue(_parentKey, out parent);
        }

        GameObject itemObject = AddressableAssetManager.Instance.InstantiatePrefab(_prefab, scrollRect);
        itemObject.name = $"TreeItem_{_label}";

        TreeNode node = new TreeNode
        {
            depth = parent != null ? parent.depth + 1 : 0,
            parent = parent,
            item = itemObject.GetComponent<UI_TreeNodeItem>(),
            target = _target,
            kind = _kind,
        };

        node.item.Setup(_label, node.depth, () => OnClickArrow(node), () => OnClickItem(node), () => DeleteRequested?.Invoke(_key));
        nodeDictionary[_key] = node;

        if (parent != null)
        {
            parent.childList.Add(node);
            // 새 자식이 바로 보이도록 부모를 펼친다.
            parent.expanded = true;
            UpdateArrow(parent);
        }
        else
        {
            rootNodeList.Add(node);
        }

        UpdateArrow(node);
        RefreshTree();

        // 선택된 항목 아래에 새 자식이 생겼을 수 있으므로 자식 구조 표시를 갱신한다.
        if (mainSceneManager == null)
        {
            mainSceneManager = FindAnyObjectByType<MainSceneManager>();
        }

        mainSceneManager?.RefreshStructure();
        mainSceneManager?.RefreshProperties();
    }

    /// <summary>_item 본인을 빼는 자식 구조를 트리 순서대로 (이름, 깊이) 목록으로 돌려준다. 깊이 0이 _item의 직접 자식이다. 트리에 없는 항목이면 빈 목록.</summary>
    public List<(string label, int depth)> GetChildStructure(UI_TreeNodeItem _item)
    {
        List<(string label, int depth)> lineList = new List<(string label, int depth)>();
        foreach (TreeNode node in nodeDictionary.Values)
        {
            if (node.item == _item)
            {
                AppendChildren(node, 0, lineList);
                break;
            }
        }

        return lineList;
    }

    private static void AppendChildren(TreeNode _node, int _depth, List<(string label, int depth)> _lineList)
    {
        for (int i = 0; i < _node.childList.Count; i++)
        {
            _lineList.Add((_node.childList[i].item.Label, _depth));
            AppendChildren(_node.childList[i], _depth + 1, _lineList);
        }
    }

    /// <summary>항목(줄)을 누르면 MainSceneManager에 선택을 알린다. 선택된 오브젝트만 씬에 보이게 된다.</summary>
    private void OnClickItem(TreeNode _node)
    {
        if (mainSceneManager == null)
        {
            mainSceneManager = FindAnyObjectByType<MainSceneManager>();
        }

        mainSceneManager?.SelectItem(_node.item, _node.target, _node.kind);
    }

    private void OnClickArrow(TreeNode _node)
    {
        if (_node.childList.Count == 0)
        {
            return;
        }

        _node.expanded = !_node.expanded;
        UpdateArrow(_node);
        RefreshTree();
    }

    private void UpdateArrow(TreeNode _node)
    {
        _node.item.SetArrow(_node.childList.Count > 0, _node.expanded);
    }

    /// <summary>트리 순서대로 행을 정렬하고, 접힌 부모 아래의 행은 숨긴다.</summary>
    private void RefreshTree()
    {
        int siblingIndex = 0;
        for (int i = 0; i < rootNodeList.Count; i++)
        {
            ApplyNode(rootNodeList[i], true, ref siblingIndex);
        }
    }

    private void ApplyNode(TreeNode _node, bool _visible, ref int _siblingIndex)
    {
        _node.item.gameObject.SetActive(_visible);
        _node.item.transform.SetSiblingIndex(_siblingIndex++);

        bool childVisible = _visible && _node.expanded;
        for (int i = 0; i < _node.childList.Count; i++)
        {
            ApplyNode(_node.childList[i], childVisible, ref _siblingIndex);
        }
    }
    #endregion
}
