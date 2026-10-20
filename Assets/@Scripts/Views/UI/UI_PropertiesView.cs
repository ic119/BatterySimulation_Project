using TMPro;
using UnityEngine;

public class UI_PropertiesView : MonoBehaviour
{
    #region Variable
    [Header("Selected Device Name Property")]
    [SerializeField] private TextMeshProUGUI deviceNameLabel;

    [Header("Selected Device InputFields")]
    [SerializeField] private TMP_InputField xAxisInputField;
    [SerializeField] private TMP_InputField yAxisInputField;
    [SerializeField] private TMP_InputField zAxisInputField;
    [SerializeField] private TextMeshProUGUI currentPositionValue;

    #endregion

    #region Method
    /// <summary>선택된 항목의 이름과 월드 위치를 표시한다. 위치는 반올림한 정수로 "x : 0, y : 0, z : 0" 형태로 보여준다.</summary>
    public void ShowSelected(string _name, Vector3 _position)
    {
        if (deviceNameLabel != null)
        {
            deviceNameLabel.text = _name;
        }

        if (currentPositionValue != null)
        {
            currentPositionValue.text = $"x : {Mathf.RoundToInt(_position.x)}, y : {Mathf.RoundToInt(_position.y)}, z : {Mathf.RoundToInt(_position.z)}";
        }
    }

    /// <summary>선택이 없을 때 이름과 위치 표시를 비운다.</summary>
    public void Clear()
    {
        if (deviceNameLabel != null)
        {
            deviceNameLabel.text = string.Empty;
        }

        if (currentPositionValue != null)
        {
            currentPositionValue.text = string.Empty;
        }
    }

    #endregion
}