using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 시뮬레이션 작업 목록의 한 줄(작업 정보 + 우측 끝 삭제 버튼)을 이루는 프리팹(SimulationItem)에 붙는 컴포넌트.
/// UI_SimulationControl이 선택된 Container의 SimulationDataModel.stepList 순서대로 줄을 만든다.
/// </summary>
public class UI_SimulationItem : MonoBehaviour
{
    #region Variable
    [Header("Simulation Item Variable")]
    [SerializeField] private TMP_Text label;

    [Header("Delete Variable")]
    [SerializeField, Tooltip("이 줄의 작업을 stepList에서 지우는 버튼")] private Button deleteButton;
    #endregion

    #region Method
    /// <summary>표시할 문구를 정하고 삭제 버튼 콜백을 연결한다. _onDelete가 null이면 삭제 버튼을 숨긴다.</summary>
    public void Setup(string _label, UnityAction _onDelete)
    {
        label.text = _label;

        deleteButton.onClick.RemoveAllListeners();
        if (_onDelete != null)
        {
            deleteButton.onClick.AddListener(_onDelete);
        }

        deleteButton.gameObject.SetActive(_onDelete != null);
    }
    #endregion
}
