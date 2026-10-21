using Incheol.Modules;
using Incheol.Presenter;
using Incheol.Utils;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UI_SubDeviceSpacingView : MonoBehaviour
{
    #region Variable
    [Header("Spacing InputFields")]
    [SerializeField] private TMP_InputField xAxisInputField;
    [SerializeField] private TMP_InputField yAxisInputField;
    [SerializeField] private TMP_InputField zAxisInputField;

    [Header("Spacing Buttons")]
    [SerializeField] private Button xAxisUpButton;
    [SerializeField] private Button xAxisDownButton;
    [SerializeField] private Button yAxisUpButton;
    [SerializeField] private Button yAxisDownButton;
    [SerializeField] private Button zAxisUpButton;
    [SerializeField] private Button zAxisDownButton;

    [Header("Alarm")]
    [SerializeField] private UI_AlarmPopupView alarmPopupView;

    private const float StepAmount = 1.0f;
    private static readonly string[] AxisNames = { "x", "y", "z" };

    private MainSceneManager mainSceneManager;
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

        xAxisInputField?.onEndEdit.AddListener(OnXEndEdit);
        yAxisInputField?.onEndEdit.AddListener(OnYEndEdit);
        zAxisInputField?.onEndEdit.AddListener(OnZEndEdit);
    }

    private void OnDestroy()
    {
        RemoveStepListener(xAxisUpButton, OnClickXUp);
        RemoveStepListener(xAxisDownButton, OnClickXDown);
        RemoveStepListener(yAxisUpButton, OnClickYUp);
        RemoveStepListener(yAxisDownButton, OnClickYDown);
        RemoveStepListener(zAxisUpButton, OnClickZUp);
        RemoveStepListener(zAxisDownButton, OnClickZDown);

        xAxisInputField?.onEndEdit.RemoveListener(OnXEndEdit);
        yAxisInputField?.onEndEdit.RemoveListener(OnYEndEdit);
        zAxisInputField?.onEndEdit.RemoveListener(OnZEndEdit);
    }
    #endregion

    #region Method
    /// <summary>MainSceneManager가 프리팹을 인스턴스화한 직후 자신을 넘겨준다. 뷰가 씬에서 직접 찾지 않는다.</summary>
    public void Init(MainSceneManager _manager)
    {
        mainSceneManager = _manager;
    }

    /// <summary>
    /// 선택 항목(SelectedTarget)의 한 단계 아래 자식 사이의 현재 간격을 x, y, z 입력 필드에 채운다.
    /// 선택이 없거나 간격을 줄 자식이 2개 미만인 축은 비운다. 입력 필드는 알림 없이 채우므로 이 호출이 간격을 다시 바꾸지는 않는다.
    /// </summary>
    public void ShowSelected(Transform _target)
    {
        if (_target == null || mainSceneManager == null)
        {
            Clear();
            return;
        }

        RefreshAxis(_target, 0, xAxisInputField);
        RefreshAxis(_target, 1, yAxisInputField);
        RefreshAxis(_target, 2, zAxisInputField);
    }

    /// <summary>알람 팝업으로 _message를 보여준다. 팝업이 연결되어 있지 않으면 아무것도 하지 않는다.</summary>
    public void ShowAlarm(string _message)
    {
        alarmPopupView?.Show(_message);
    }

    /// <summary>선택이 없을 때 입력 필드를 비운다.</summary>
    public void Clear()
    {
        SetInputText(xAxisInputField, string.Empty);
        SetInputText(yAxisInputField, string.Empty);
        SetInputText(zAxisInputField, string.Empty);
    }

    private void RefreshAxis(Transform _target, int _axis, TMP_InputField _field)
    {
        if (DeviceSpacingCalculator.TryGetSpacing(_target, mainSceneManager.SelectedKind, _axis, out float current, out _))
        {
            SetInput(_field, current);
        }
        else
        {
            SetInputText(_field, string.Empty);
        }
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

    private void OnClickXUp() => ChangeAxis(0, xAxisInputField, StepAmount);
    private void OnClickXDown() => ChangeAxis(0, xAxisInputField, -StepAmount);
    private void OnClickYUp() => ChangeAxis(1, yAxisInputField, StepAmount);
    private void OnClickYDown() => ChangeAxis(1, yAxisInputField, -StepAmount);
    private void OnClickZUp() => ChangeAxis(2, zAxisInputField, StepAmount);
    private void OnClickZDown() => ChangeAxis(2, zAxisInputField, -StepAmount);

    private void OnXEndEdit(string _text) => ApplyAxis(0, xAxisInputField, TryParse(_text, out float v) ? v : (float?)null);
    private void OnYEndEdit(string _text) => ApplyAxis(1, yAxisInputField, TryParse(_text, out float v) ? v : (float?)null);
    private void OnZEndEdit(string _text) => ApplyAxis(2, zAxisInputField, TryParse(_text, out float v) ? v : (float?)null);

    /// <summary>입력 필드의 현재 값에 _delta를 더해 적용한다. 비어 있거나 숫자가 아니면 건드리지 않고, 0보다 작아지면 바꾸지 않는다.</summary>
    private void ChangeAxis(int _axis, TMP_InputField _field, float _delta)
    {
        if (_field == null || !TryParse(_field.text, out float value))
        {
            return;
        }

        float next = value + _delta;
        if (next < 0.0f)
        {
            return;
        }

        ApplyAxis(_axis, _field, next);
    }

    /// <summary>
    /// _axis 방향의 간격을 _value로 바꾼다. 선택된 항목을 벗어나는 값이면 허용되는 최대 간격으로 낮추고 알람을 띄운다.
    /// 숫자가 아니거나 0보다 작은 값이면 바꾸지 않고 입력 필드를 현재 간격으로 되돌린다.
    /// </summary>
    private void ApplyAxis(int _axis, TMP_InputField _field, float? _value)
    {
        Transform target = mainSceneManager != null ? mainSceneManager.SelectedTarget : null;
        if (target == null)
        {
            return;
        }

        MainSceneManager.TargetKind kind = mainSceneManager.SelectedKind;
        if (!_value.HasValue || _value.Value < 0.0f)
        {
            if (_value.HasValue)
            {
                DebugLogManager.GenerateErrorMessage<UI_SubDeviceSpacingView>("간격은 0 이상의 숫자로 입력해야 합니다.");
            }

            RefreshAxis(target, _axis, _field);
            return;
        }

        float spacing = _value.Value;
        if (!DeviceSpacingCalculator.TrySetSpacing(target, kind, _axis, spacing, out float max))
        {
            string message = $"{target.name} 밖으로 벗어나 간격을 늘릴 수 없습니다. {AxisNames[_axis]} 최대 간격 : {max:0.##}, 입력 : {spacing:0.##}";
            DebugLogManager.GenerateErrorMessage<UI_SubDeviceSpacingView>(message);
            alarmPopupView?.Show(message);

            DeviceSpacingCalculator.TrySetSpacing(target, kind, _axis, max, out _);
        }

        // 이후 새 자식이 생길 때 같은 간격을 다시 적용할 수 있도록 선택 항목에 기억해 둔다.
        if (DeviceSpacingCalculator.TryGetSpacing(target, kind, _axis, out float applied, out _))
        {
            if (!target.TryGetComponent(out SubDeviceSpacingData data))
            {
                data = target.gameObject.AddComponent<SubDeviceSpacingData>();
            }

            data.Set(_axis, applied);
        }

        // 간격이 바뀌면 자식 위치가 달라지므로 세 축 모두 다시 채운다(다른 축의 층 구성이 달라질 수 있다).
        ShowSelected(target);
    }

    private static bool TryParse(string _text, out float _value)
    {
        _value = 0.0f;
        string text = _text.Replace("​", string.Empty).Trim();
        return text.Length > 0 && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _value);
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
    #endregion
}
