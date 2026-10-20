using Incheol.Presenter;
using Incheol.Utils;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_DeviceScaleView : MonoBehaviour
{
    #region Variable
    [Header("Selected Device InputFields")]
    [SerializeField] private TMP_InputField xAxisInputField;
    [SerializeField] private TMP_InputField yAxisInputField;
    [SerializeField] private TMP_InputField zAxisInputField;
    [SerializeField] private TextMeshProUGUI currentPositionValue;

    [Header("Apply")]
    [SerializeField] private Button applyButton;

    private MainSceneManager mainSceneManager;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        if (applyButton != null)
        {
            applyButton.onClick.AddListener(OnClickApply);
        }
    }

    private void OnDestroy()
    {
        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(OnClickApply);
        }
    }
    #endregion

    #region Method
    /// <summary>
    /// 선택된 오브젝트의 실제(월드) 크기를 "x : 0, y : 0, z : 0" 형태로 표시하고, 입력 필드에도 현재 크기를 채운다.
    /// 오브젝트와 자식 Renderer의 bounds를 합친 크기이며 Renderer가 꺼져 있어도 계산된다. 표시는 반올림한 정수다.
    /// </summary>
    public void ShowSelected(Transform _target)
    {
        if (!TryGetWorldSize(_target, out Vector3 size))
        {
            Clear();
            return;
        }

        if (currentPositionValue != null)
        {
            currentPositionValue.text = $"x : {Mathf.RoundToInt(size.x)}, y : {Mathf.RoundToInt(size.y)}, z : {Mathf.RoundToInt(size.z)}";
        }

        SetInput(xAxisInputField, size.x);
        SetInput(yAxisInputField, size.y);
        SetInput(zAxisInputField, size.z);
    }

    /// <summary>선택이 없을 때 크기 표시와 입력 필드를 비운다.</summary>
    public void Clear()
    {
        if (currentPositionValue != null)
        {
            currentPositionValue.text = string.Empty;
        }

        SetInputText(xAxisInputField, string.Empty);
        SetInputText(yAxisInputField, string.Empty);
        SetInputText(zAxisInputField, string.Empty);
    }

    /// <summary>
    /// 입력한 x, y, z로 MainSceneManager의 selectedItem 본인의 크기만 바꾼다(자식 Module / Cell의 크기와 위치는 그대로).
    /// 비어 있는 칸은 현재 값을 유지한다. 0 이하이거나 숫자가 아닌 값이 있으면 아무것도 바꾸지 않는다.
    /// Pack / Module은 몸체(Body)의 좌측 바닥 뒤쪽 모서리를 고정한 채 +x, +y, -z 방향으로 늘이고 줄이며, Cell은 중심을 고정한다.
    /// </summary>
    private void OnClickApply()
    {
        if (mainSceneManager == null)
        {
            mainSceneManager = FindAnyObjectByType<MainSceneManager>();
        }

        Transform target = mainSceneManager != null ? mainSceneManager.SelectedTarget : null;
        if (target == null)
        {
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>("선택된 항목이 없어 크기를 바꿀 수 없습니다.");
            return;
        }

        if (!TryGetSizeRenderer(target, mainSceneManager.SelectedKind, out Renderer sizeRenderer))
        {
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>($"크기를 바꿀 수 있는 몸체(Renderer)가 없습니다. 항목 : {target.name}");
            return;
        }

        Bounds current = sizeRenderer.bounds;
        if (!TryParseAxis(xAxisInputField, current.size.x, out float x) ||
            !TryParseAxis(yAxisInputField, current.size.y, out float y) ||
            !TryParseAxis(zAxisInputField, current.size.z, out float z))
        {
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>("크기는 0보다 큰 숫자로 입력해야 합니다.");
            return;
        }

        Vector3 newSize = new Vector3(x, y, z);
        Transform bodyTransform = sizeRenderer.transform;

        // Pack / Module을 줄일 때 안의 Module / Cell이 몸체 밖으로 나가면 막는다.
        if (bodyTransform != target && !ChildrenFit(target, sizeRenderer, newSize, out Vector3 minSize))
        {
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>($"내부 항목이 벗어나 크기를 줄일 수 없습니다. 최소 크기(x,y,z) : {minSize.x:0.##}, {minSize.y:0.##}, {minSize.z:0.##}, 입력 : {newSize.x:0.##}, {newSize.y:0.##}, {newSize.z:0.##}");
            return;
        }

        // Cell을 키울 때 부모 Module 몸체 밖으로 나가면 막는다.
        if (bodyTransform == target && !CellFits(target, current, newSize, out Vector3 maxSize))
        {
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>($"Module 밖으로 벗어나 크기를 키울 수 없습니다. 최대 크기(x,y,z) : {maxSize.x:0.##}, {maxSize.y:0.##}, {maxSize.z:0.##}, 입력 : {newSize.x:0.##}, {newSize.y:0.##}, {newSize.z:0.##}");
            return;
        }

        if (bodyTransform == target)
        {
            // Cell처럼 본인이 곧 몸체면 중심을 유지한 채 현재 스케일을 비율만큼 바꾼다.
            Vector3 center = current.center;
            Vector3 scale = bodyTransform.localScale;
            bodyTransform.localScale = new Vector3(
                Scale(scale.x, current.size.x, newSize.x),
                Scale(scale.y, current.size.y, newSize.y),
                Scale(scale.z, current.size.z, newSize.z));
            bodyTransform.position += center - sizeRenderer.bounds.center;
        }
        else
        {
            // Pack / Module의 Body는 단위 크기 메시(Cube)라 목표 월드 크기를 부모 스케일로 나누면 로컬 스케일이 된다.
            Vector3 parentScale = bodyTransform.parent.lossyScale;
            bodyTransform.localScale = new Vector3(
                Divide(newSize.x, parentScale.x),
                Divide(newSize.y, parentScale.y),
                Divide(newSize.z, parentScale.z));
            bodyTransform.position = new Vector3(
                current.min.x + newSize.x * 0.5f,
                current.min.y + newSize.y * 0.5f,
                current.max.z - newSize.z * 0.5f);
        }

        mainSceneManager.RefreshProperties();
    }

    /// <summary>크기를 바꿀 Renderer. Cell은 본인, Pack / Module은 자식 Body(자식 Module / Cell의 Renderer는 제외). 없으면 false.</summary>
    private static bool TryGetSizeRenderer(Transform _target, MainSceneManager.TargetKind _kind, out Renderer _renderer)
    {
        _renderer = null;
        if (_kind == MainSceneManager.TargetKind.Cell)
        {
            _renderer = _target.GetComponent<Renderer>();
            return _renderer != null;
        }

        _renderer = _target.GetComponent<Renderer>();
        if (_renderer != null)
        {
            return true;
        }

        for (int i = 0; i < _target.childCount; i++)
        {
            Transform child = _target.GetChild(i);
            if (child.name == "Body" && child.TryGetComponent(out _renderer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseAxis(TMP_InputField _field, float _fallback, out float _value)
    {
        _value = _fallback;
        if (_field == null || string.IsNullOrWhiteSpace(_field.text))
        {
            return _fallback > 0.0f;
        }

        string text = _field.text.Replace("​", string.Empty).Trim();
        if (text.Length == 0)
        {
            return _fallback > 0.0f;
        }

        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _value) && _value > 0.0f;
    }

    /// <summary>
    /// 몸체를 _newSize로 바꿨을 때 자식(Module의 Cell, Pack의 Module과 그 Cell)이 모두 몸체 안에 남는지 검사한다.
    /// 몸체는 좌측 바닥 뒤쪽 모서리(min.x, min.y, max.z)를 고정한 채 +x, +y, -z 방향으로 바뀐다고 본다.
    /// 벗어나면 false이고 _minSize에는 자식을 담기 위한 최소 크기가 담긴다.
    /// </summary>
    private static bool ChildrenFit(Transform _target, Renderer _body, Vector3 _newSize, out Vector3 _minSize)
    {
        _minSize = Vector3.zero;
        bool hasChild = false;
        Bounds childBounds = default;
        Renderer[] rendererArray = _target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rendererArray.Length; i++)
        {
            if (rendererArray[i] == _body)
            {
                continue;
            }

            if (!hasChild)
            {
                childBounds = rendererArray[i].bounds;
                hasChild = true;
            }
            else
            {
                childBounds.Encapsulate(rendererArray[i].bounds);
            }
        }

        if (!hasChild)
        {
            return true;
        }

        Bounds body = _body.bounds;
        _minSize = new Vector3(
            childBounds.max.x - body.min.x,
            childBounds.max.y - body.min.y,
            body.max.z - childBounds.min.z);

        const float Tolerance = 0.001f;
        return _newSize.x >= _minSize.x - Tolerance && _newSize.y >= _minSize.y - Tolerance && _newSize.z >= _minSize.z - Tolerance;
    }

    /// <summary>
    /// Cell을 _newSize로 바꿨을 때(중심 고정) 부모 Module 몸체 안에 남는지 검사한다. 부모 몸체를 찾을 수 없으면 검사하지 않고 true.
    /// 벗어나면 false이고 _maxSize에는 현재 중심에서 키울 수 있는 최대 크기가 담긴다.
    /// </summary>
    private static bool CellFits(Transform _cell, Bounds _current, Vector3 _newSize, out Vector3 _maxSize)
    {
        _maxSize = _newSize;
        if (_cell.parent == null || !TryGetSizeRenderer(_cell.parent, MainSceneManager.TargetKind.Module, out Renderer moduleBody))
        {
            return true;
        }

        Bounds inner = moduleBody.bounds;
        Vector3 center = _current.center;
        _maxSize = new Vector3(
            2.0f * Mathf.Min(center.x - inner.min.x, inner.max.x - center.x),
            2.0f * Mathf.Min(center.y - inner.min.y, inner.max.y - center.y),
            2.0f * Mathf.Min(center.z - inner.min.z, inner.max.z - center.z));

        const float Tolerance = 0.001f;
        return _newSize.x <= _maxSize.x + Tolerance && _newSize.y <= _maxSize.y + Tolerance && _newSize.z <= _maxSize.z + Tolerance;
    }

    private static float Scale(float _scale, float _currentSize, float _newSize)
    {
        return Mathf.Approximately(_currentSize, 0.0f) ? _scale : _scale * _newSize / _currentSize;
    }

    private static float Divide(float _value, float _divisor)
    {
        return Mathf.Approximately(_divisor, 0.0f) ? _value : _value / _divisor;
    }

    private static void SetInput(TMP_InputField _field, float _value)
    {
        SetInputText(_field, Mathf.RoundToInt(_value * 100.0f) / 100.0f == Mathf.Round(_value) ? Mathf.RoundToInt(_value).ToString(CultureInfo.InvariantCulture) : _value.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private static void SetInputText(TMP_InputField _field, string _text)
    {
        if (_field != null)
        {
            _field.SetTextWithoutNotify(_text);
        }
    }

    private static bool TryGetWorldSize(Transform _target, out Vector3 _size)
    {
        _size = Vector3.zero;
        if (_target == null)
        {
            return false;
        }

        Renderer[] rendererArray = _target.GetComponentsInChildren<Renderer>(true);
        if (rendererArray.Length == 0)
        {
            return false;
        }

        Bounds bounds = rendererArray[0].bounds;
        for (int i = 1; i < rendererArray.Length; i++)
        {
            bounds.Encapsulate(rendererArray[i].bounds);
        }

        _size = bounds.size;
        return true;
    }
    #endregion
}
