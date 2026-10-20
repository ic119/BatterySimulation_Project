using Incheol.Presenter;
using Incheol.Utils;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UI_DeviceScaleView : MonoBehaviour
{
    #region Variable
    [Header("Selected Device InputFields")]
    [SerializeField] private TMP_InputField xAxisInputField;
    [SerializeField] private TMP_InputField yAxisInputField;
    [SerializeField] private TMP_InputField zAxisInputField;

    [Header("Selected Device Buttons")]
    [SerializeField] private Button xAxisUpButton;
    [SerializeField] private Button xAxisDownButton;
    [SerializeField] private Button yAxisUpButton;
    [SerializeField] private Button yAxisDownButton;
    [SerializeField] private Button zAxisUpButton;
    [SerializeField] private Button zAxisDownButton;

    private const float StepAmount = 1.0f;

    private MainSceneManager mainSceneManager;
    private UI_AlarmPopupView alarmPopupView;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        AddStepListener(xAxisUpButton, OnClickXUp);
        AddStepListener(xAxisDownButton, OnClickXDown);
        AddStepListener(yAxisUpButton, OnClickYUp);
        AddStepListener(yAxisDownButton, OnClickYDown);
        AddStepListener(zAxisUpButton, OnClickZUp);
        AddStepListener(zAxisDownButton, OnClickZDown);

        AddValueListener(xAxisInputField);
        AddValueListener(yAxisInputField);
        AddValueListener(zAxisInputField);
    }

    private void OnDestroy()
    {
        RemoveStepListener(xAxisUpButton, OnClickXUp);
        RemoveStepListener(xAxisDownButton, OnClickXDown);
        RemoveStepListener(yAxisUpButton, OnClickYUp);
        RemoveStepListener(yAxisDownButton, OnClickYDown);
        RemoveStepListener(zAxisUpButton, OnClickZUp);
        RemoveStepListener(zAxisDownButton, OnClickZDown);

        RemoveValueListener(xAxisInputField);
        RemoveValueListener(yAxisInputField);
        RemoveValueListener(zAxisInputField);
    }
    #endregion

    #region Method
    /// <summary>
    /// 선택된 오브젝트 본인의 Scale(x, y, z)을 xAxisInputField / yAxisInputField / zAxisInputField에 채운다.
    /// Pack / Module은 자식 Body, Cell은 본체의 월드 크기이며 자식 Module / Cell은 섞지 않는다. Renderer가 꺼져 있어도 계산된다.
    /// 입력 필드는 알림 없이 채우므로 이 호출이 크기를 다시 바꾸지는 않는다.
    /// </summary>
    public void ShowSelected(Transform _target)
    {
        if (!TryGetOwnSize(_target, out Vector3 size))
        {
            Clear();
            return;
        }

        SetInput(xAxisInputField, size.x);
        SetInput(yAxisInputField, size.y);
        SetInput(zAxisInputField, size.z);
    }

    /// <summary>선택이 없을 때 입력 필드를 비운다.</summary>
    public void Clear()
    {
        SetInputText(xAxisInputField, string.Empty);
        SetInputText(yAxisInputField, string.Empty);
        SetInputText(zAxisInputField, string.Empty);
    }

    private static void AddStepListener(Button _button, UnityAction _action)
    {
        if (_button != null)
        {
            _button.onClick.AddListener(_action);
        }
    }

    private static void RemoveStepListener(Button _button, UnityAction _action)
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(_action);
        }
    }

    private void AddValueListener(TMP_InputField _field)
    {
        if (_field != null)
        {
            _field.onValueChanged.AddListener(OnAxisValueChanged);
            _field.onEndEdit.AddListener(OnAxisEndEdit);
        }
    }

    private void RemoveValueListener(TMP_InputField _field)
    {
        if (_field != null)
        {
            _field.onValueChanged.RemoveListener(OnAxisValueChanged);
            _field.onEndEdit.RemoveListener(OnAxisEndEdit);
        }
    }

    private void OnClickXUp() => ChangeAxis(xAxisInputField, StepAmount);
    private void OnClickXDown() => ChangeAxis(xAxisInputField, -StepAmount);
    private void OnClickYUp() => ChangeAxis(yAxisInputField, StepAmount);
    private void OnClickYDown() => ChangeAxis(yAxisInputField, -StepAmount);
    private void OnClickZUp() => ChangeAxis(zAxisInputField, StepAmount);
    private void OnClickZDown() => ChangeAxis(zAxisInputField, -StepAmount);

    /// <summary>입력 필드에 값을 타이핑할 때마다 실시간으로 선택 항목의 크기에 반영한다. 입력 중에는 오류 로그를 남기지 않는다.</summary>
    private void OnAxisValueChanged(string _text)
    {
        ApplyScale(false);
    }

    /// <summary>입력을 마치면(Enter / 포커스 이동) 한 번 더 반영하며, 내부 항목 때문에 줄일 수 없는 값은 최소 크기로 되돌리고 알람을 띄운다.</summary>
    private void OnAxisEndEdit(string _text)
    {
        ApplyScale(true);
    }

    /// <summary>
    /// _field에 입력된 값에 _delta를 더해 다시 채우고, 바뀐 값을 선택 항목의 크기에 바로 반영한다.
    /// 비어 있거나 숫자가 아니면 건드리지 않고, 결과가 0 이하가 되면 바꾸지 않는다.
    /// </summary>
    private void ChangeAxis(TMP_InputField _field, float _delta)
    {
        if (_field == null)
        {
            return;
        }

        string text = _field.text.Replace("​", string.Empty).Trim();
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return;
        }

        float next = value + _delta;
        if (next <= 0.0f)
        {
            return;
        }

        SetInput(_field, next);
        ApplyScale(true);
    }

    /// <summary>
    /// 입력 필드의 x, y, z를 MainSceneManager의 SelectedItem 본인의 크기에 바로 반영한다(자식 Module / Cell의 크기와 위치는 그대로).
    /// 비어 있는 칸은 현재 값을 유지한다. 0 이하이거나 숫자가 아닌 값이 있으면 아무것도 바꾸지 않는다.
    /// Pack / Module은 몸체(Body)의 좌측 바닥 뒤쪽 모서리를 고정한 채 +x, +y, -z 방향으로 늘이고 줄이며, Cell은 중심을 고정한다.
    /// _log가 false면(타이핑 중) 값이 맞지 않아도 로그를 남기지 않는다. 입력 필드는 다시 채우지 않는다(입력 중인 글자를 덮어쓰지 않기 위해).
    /// </summary>
    private void ApplyScale(bool _log)
    {
        if (mainSceneManager == null)
        {
            mainSceneManager = FindAnyObjectByType<MainSceneManager>();
        }

        Transform target = mainSceneManager != null ? mainSceneManager.SelectedTarget : null;
        if (target == null)
        {
            return;
        }

        if (!TryGetSizeRenderer(target, mainSceneManager.SelectedKind, out Renderer sizeRenderer))
        {
            if (_log)
            {
                DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>($"크기를 바꿀 수 있는 몸체(Renderer)가 없습니다. 항목 : {target.name}");
            }

            return;
        }

        Bounds current = sizeRenderer.bounds;
        if (!TryParseAxis(xAxisInputField, current.size.x, out float x) ||
            !TryParseAxis(yAxisInputField, current.size.y, out float y) ||
            !TryParseAxis(zAxisInputField, current.size.z, out float z))
        {
            if (_log)
            {
                DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>("크기는 0보다 큰 숫자로 입력해야 합니다.");
            }

            return;
        }

        Vector3 newSize = new Vector3(x, y, z);
        if ((newSize - current.size).sqrMagnitude < 1e-8f)
        {
            return;
        }

        Transform bodyTransform = sizeRenderer.transform;

        // Pack / Module을 줄일 때 안의 Module / Cell이 몸체 밖으로 나가면 최소 크기까지만 줄이고 알람으로 알린다.
        // 타이핑 중(_log가 false)에는 글자를 덮어쓰지 않도록 아무것도 바꾸지 않고, 입력을 끝낼 때 / 버튼을 누를 때 최소 크기로 되돌린다.
        if (bodyTransform != target && !ChildrenFit(target, sizeRenderer, newSize, out Vector3 minSize))
        {
            if (!_log)
            {
                return;
            }

            string message = $"내부 항목이 벗어나 크기를 줄일 수 없습니다. 최소 크기(x,y,z) : {minSize.x:0.##}, {minSize.y:0.##}, {minSize.z:0.##}, 입력 : {newSize.x:0.##}, {newSize.y:0.##}, {newSize.z:0.##}";
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>(message);
            ShowAlarm(message);

            newSize = Vector3.Max(newSize, minSize);
            SetInput(xAxisInputField, newSize.x);
            SetInput(yAxisInputField, newSize.y);
            SetInput(zAxisInputField, newSize.z);

            if ((newSize - current.size).sqrMagnitude < 1e-8f)
            {
                return;
            }
        }

        // Cell / Module을 키울 때 부모 Module / Pack 몸체 밖으로 나가면 막는다.
        MainSceneManager.TargetKind kind = mainSceneManager.SelectedKind;
        if (kind != MainSceneManager.TargetKind.Pack && !FitsInParent(target, kind, current, newSize, out Vector3 maxSize))
        {
            // 최소 크기 위반과 같이, 타이핑 중에는 아무것도 바꾸지 않고 입력을 끝낼 때 / 버튼을 누를 때 최대 크기로 되돌린다.
            if (!_log)
            {
                return;
            }

            string parentName = kind == MainSceneManager.TargetKind.Cell ? "Module" : "Pack";
            string message = $"{parentName} 밖으로 벗어나 크기를 키울 수 없습니다. 최대 크기(x,y,z) : {maxSize.x:0.##}, {maxSize.y:0.##}, {maxSize.z:0.##}, 입력 : {newSize.x:0.##}, {newSize.y:0.##}, {newSize.z:0.##}";
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>(message);
            ShowAlarm(message);

            // 부모 몸체가 이미 현재 크기보다 좁아 최대 크기가 0 이하인 축은 현재 크기를 유지한다.
            newSize = new Vector3(
                maxSize.x > 0.0f ? Mathf.Min(newSize.x, maxSize.x) : current.size.x,
                maxSize.y > 0.0f ? Mathf.Min(newSize.y, maxSize.y) : current.size.y,
                maxSize.z > 0.0f ? Mathf.Min(newSize.z, maxSize.z) : current.size.z);
            SetInput(xAxisInputField, newSize.x);
            SetInput(yAxisInputField, newSize.y);
            SetInput(zAxisInputField, newSize.z);

            if ((newSize - current.size).sqrMagnitude < 1e-8f)
            {
                return;
            }
        }

        if (bodyTransform == target)
        {
            // Cell처럼 본인이 곧 몸체면 현재 스케일을 비율만큼 바꾸고, Pack / Module과 같이 좌측 바닥 뒤쪽 모서리를 고정한다.
            Vector3 anchor = new Vector3(current.min.x, current.min.y, current.max.z);
            Vector3 scale = bodyTransform.localScale;
            bodyTransform.localScale = new Vector3(
                Scale(scale.x, current.size.x, newSize.x),
                Scale(scale.y, current.size.y, newSize.y),
                Scale(scale.z, current.size.z, newSize.z));
            Bounds resized = sizeRenderer.bounds;
            bodyTransform.position += anchor - new Vector3(resized.min.x, resized.min.y, resized.max.z);
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
    }

    private void ShowAlarm(string _message)
    {
        if (alarmPopupView == null)
        {
            alarmPopupView = FindAnyObjectByType<UI_AlarmPopupView>();
        }

        alarmPopupView?.Show(_message);
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
    /// _child(Cell은 Module 안, Module은 Pack 안)를 _newSize로 바꿨을 때(좌측 바닥 뒤쪽 모서리 고정, +x / +y / -z로 변함) 부모 몸체 안에 남는지 검사한다.
    /// _current는 _child 몸체의 현재 bounds. 부모 몸체를 찾을 수 없으면 검사하지 않고 true.
    /// 벗어나면 false이고 _maxSize에는 그 모서리에서 키울 수 있는 최대 크기가 담긴다.
    /// </summary>
    private static bool FitsInParent(Transform _child, MainSceneManager.TargetKind _kind, Bounds _current, Vector3 _newSize, out Vector3 _maxSize)
    {
        _maxSize = _newSize;
        if (_child.parent == null || !TryGetSizeRenderer(_child.parent, _kind == MainSceneManager.TargetKind.Cell ? MainSceneManager.TargetKind.Module : MainSceneManager.TargetKind.Pack, out Renderer parentBody))
        {
            return true;
        }

        Bounds inner = parentBody.bounds;
        _maxSize = new Vector3(
            inner.max.x - _current.min.x,
            inner.max.y - _current.min.y,
            _current.max.z - inner.min.z);

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

    /// <summary>
    /// 선택 항목 본인 몸체(Pack / Module은 자식 Body, Cell은 본체)의 월드 크기.
    /// 몸체 Renderer가 없으면 자식 Renderer를 합친 크기로 대신한다.
    /// </summary>
    private bool TryGetOwnSize(Transform _target, out Vector3 _size)
    {
        _size = Vector3.zero;
        if (_target == null)
        {
            return false;
        }

        if (mainSceneManager == null)
        {
            mainSceneManager = FindAnyObjectByType<MainSceneManager>();
        }

        MainSceneManager.TargetKind kind = mainSceneManager != null ? mainSceneManager.SelectedKind : MainSceneManager.TargetKind.Pack;
        if (TryGetSizeRenderer(_target, kind, out Renderer body))
        {
            _size = body.bounds.size;
            return true;
        }

        return TryGetWorldSize(_target, out _size);
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
