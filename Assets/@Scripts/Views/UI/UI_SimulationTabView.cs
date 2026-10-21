using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SimulationTab의 Load 버튼. Editor 탭과 같게 Json 파일 목록을 열고, 파일을 고르면 UI_MainSceneView가 그 파일로 다시 만든다.
/// SimulationTab이 처음 켜질 때 Awake가 실행되며, 그 전에는 버튼을 누를 수 없으므로 문제가 없다.
/// </summary>
public class UI_SimulationTabView : MonoBehaviour
{
    #region Variable
    [Header("SimulationTab Tools Button Variable")]
    [SerializeField] private Button loadButton;

    [Header("Popup / List")]
    [SerializeField] private UI_JsonFileListView jsonFileListView;

    private UI_MainSceneView mainSceneView;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        mainSceneView = GetComponentInParent<UI_MainSceneView>(true);

        if (loadButton != null)
        {
            loadButton.onClick.AddListener(OnClickLoadButton);
        }

        if (jsonFileListView != null)
        {
            jsonFileListView.FileClicked += OnJsonFileClicked;
        }
    }

    private void OnDestroy()
    {
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

    #region Method
    private void OnClickLoadButton()
    {
        jsonFileListView?.Show();
    }

    private void OnJsonFileClicked(string _path)
    {
        mainSceneView?.LoadFromFile(_path);
    }
    #endregion
}
