using Incheol.Models.Define;
using Incheol.Modules;
using Incheol.Utils;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 저장된 Json 파일 목록을 ScrollView(content)에 보여주는 패널. JsonFileListContainer에 붙는다.
/// 각 줄은 Addressable 프리팹(UI_JsonFileItem)이며, 목록을 열 때마다 프리팹을 불러와(UI_EditorView의 UI_TreeNodeItem과 같은 방식) 줄 수만큼 만든다.
/// 컨테이너(이 오브젝트) 자체를 켜고 꺼서 열고 닫으며, 프리팹에 켜진 채로 저장되어 있든 꺼진 채로 저장되어 있든 시작할 때는 닫혀 있다.
/// 꺼져 있는 동안에는 이 컴포넌트가 실행되지 않으므로, 찾을 때는 비활성 오브젝트도 포함해 찾아야 한다(UI_MainSceneView가 그렇게 한다).
/// 파일 항목을 누르면 패널을 닫고 FileClicked로 그 파일의 전체 경로를 알린다. 배경(blockedImage)을 누르면 그냥 닫힌다.
/// </summary>
public class UI_JsonFileListView : MonoBehaviour
{
    /// <summary>파일 항목이 눌렸을 때 그 Json 파일의 전체 경로와 함께 발생한다. 패널은 이미 닫힌 뒤다.</summary>
    public event System.Action<string> FileClicked;

    #region Variable
    [Header("Json File List Variable")]
    [SerializeField] private GameObject blockedImage;
    [SerializeField] private GameObject container;
    [SerializeField] private GameObject scrollView;
    [SerializeField] private RectTransform content;

    // Show()가 처음 컨테이너를 켜서 Awake가 실행되는 경우와, 프리팹이 켜진 채로 시작해 Awake가 실행되는 경우를 구분한다.
    private bool isShown;

    // 프리팹 로드를 기다리는 사이 Show()가 다시 불리면, 이전 요청의 콜백이 줄을 중복으로 만들지 않도록 구분한다.
    private int showVersion;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        if (blockedImage != null && blockedImage.TryGetComponent(out Button blockedButton))
        {
            blockedButton.onClick.AddListener(Hide);
        }

        // 프리팹에 켜진 채로 저장되어 있어도 시작할 때는 닫혀 있게 한다. Show()로 처음 켜질 때 실행된 Awake는 그대로 둔다.
        if (!isShown)
        {
            container.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (blockedImage != null && blockedImage.TryGetComponent(out Button blockedButton))
        {
            blockedButton.onClick.RemoveListener(Hide);
        }
    }
    #endregion

    #region Method
    public void Show()
    {
        if (content == null)
        {
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        List<(string text, string path)> entryList = new List<(string text, string path)>();
        string[] pathArray = JsonDataController.GetSavedFilePaths();
        if (pathArray.Length == 0)
        {
            entryList.Add(("저장된 Json 파일이 없습니다.", null));
        }

        for (int i = 0; i < pathArray.Length; i++)
        {
            entryList.Add((Path.GetFileName(pathArray[i]), pathArray[i]));
        }

        isShown = true;
        container.SetActive(true);
        SetChildrenActive(true);

        int version = ++showVersion;
        string key = AddressableAssetKey.UI_JsonFileItem.ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab => CreateItems(prefab, entryList, version),
            () => DebugLogManager.GenerateErrorMessage<UI_JsonFileListView>($"{key} 로드 실패"));
    }

    public void Hide()
    {
        container.SetActive(false);
    }

    private void SetChildrenActive(bool _active)
    {
        if (blockedImage != null)
        {
            blockedImage.SetActive(_active);
        }

        if (scrollView != null)
        {
            scrollView.SetActive(_active);
        }
    }

    private void CreateItems(GameObject _prefab, List<(string text, string path)> _entryList, int _version)
    {
        // 로드를 기다리는 사이 뷰가 파괴되었거나 목록이 다시 열렸으면 이 요청은 버린다.
        if (this == null || content == null || _version != showVersion)
        {
            return;
        }

        for (int i = 0; i < _entryList.Count; i++)
        {
            string filePath = _entryList[i].path;
            GameObject itemObject = AddressableAssetManager.Instance.InstantiatePrefab(_prefab, content);
            itemObject.name = $"JsonFileItem_{_entryList[i].text}";
            GameObject createdItem = itemObject;
            itemObject.GetComponent<UI_JsonFileItem>().Setup(
                _entryList[i].text,
                filePath != null ? () => OnClickFile(filePath) : null,
                filePath != null ? () => OnDeleteFile(filePath, createdItem) : null);
        }
    }

    /// <summary>삭제 버튼을 확인까지 눌렀을 때: 파일을 지우고 그 줄을 목록에서 뺀다. 마지막 파일이었으면 안내 문구 한 줄로 다시 그린다.</summary>
    private void OnDeleteFile(string _filePath, GameObject _item)
    {
        if (!JsonDataController.Delete(_filePath))
        {
            return;
        }

        Destroy(_item);

        // Destroy는 프레임 끝에 처리되므로, 남은 줄이 없는지는 지워질 줄을 빼고 센다.
        int remaining = 0;
        for (int i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i).gameObject != _item)
            {
                remaining++;
            }
        }

        if (remaining == 0)
        {
            Show();
        }
    }

    private void OnClickFile(string _filePath)
    {
        Hide();
        FileClicked?.Invoke(_filePath);
    }
    #endregion
}
