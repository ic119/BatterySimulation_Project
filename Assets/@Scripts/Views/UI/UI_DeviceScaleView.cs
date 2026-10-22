using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
﻿using Incheol.Modules;
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

    [Header("Alarm")]
    [SerializeField] private UI_AlarmPopupView alarmPopupView;

    private const float StepAmount = 1.0f;

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
    /// <summary>MainSceneManager가 프리팹을 인스턴스화한 직후 자신을 넘겨준다. 뷰가 씬에서 직접 찾지 않는다.</summary>
    public void Init(MainSceneManager _manager)
    {
        mainSceneManager = _manager;
    }

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

    /// <summary>
    /// 입력 필드에 값을 타이핑할 때마다 실시간으로 선택 항목의 크기에 반영 (입력 중에는 오류 로그 X)
    /// </summary>
    private void OnAxisValueChanged(string _text)
    {
        ApplyScale(false);
    }

    /// <summary>
    /// 입력을 마치면(Enter / 포커스 이동) 한 번 더 반영 처리, 내부 항목 때문에 줄일 수 없는 값은 최소 크기로 되돌리고 알람 표출
    /// </summary>
    private void OnAxisEndEdit(string _text)
    {
        ApplyScale(true);
    }

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
    
    /// </summary>
    private void ApplyScale(bool _log)
    {
        Transform target = mainSceneManager != null ? mainSceneManager.SelectedTarget : null;
        if (target == null)
        {
            return;
        }

        if (!DeviceSizeCalculator.TryGetSizeRenderer(target, mainSceneManager.SelectedKind, out Renderer sizeRenderer))
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
        if (bodyTransform != target && !DeviceSizeCalculator.ChildrenFit(target, sizeRenderer, newSize, out Vector3 minSize))
        {
            if (!_log)
            {
                return;
            }

            string message = $"내부 항목이 벗어나 크기를 줄일 수 없습니다. 최소 크기(x,y,z) : {minSize.x:0.##}, {minSize.y:0.##}, {minSize.z:0.##}, 입력 : {newSize.x:0.##}, {newSize.y:0.##}, {newSize.z:0.##}";
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>(message);
            alarmPopupView?.Show(message);

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
        DeviceType kind = mainSceneManager.SelectedKind;
        if (kind != DeviceType.Pack && !DeviceSizeCalculator.FitsInParent(target, kind, current, newSize, out Vector3 maxSize))
        {
            // 최소 크기 위반과 같이, 타이핑 중에는 아무것도 바꾸지 않고 입력을 끝낼 때 / 버튼을 누를 때 최대 크기로 되돌린다.
            if (!_log)
            {
                return;
            }

            string parentName = kind == DeviceType.Cell ? "Module" : "Pack";
            string message = $"{parentName} 밖으로 벗어나 크기를 키울 수 없습니다. 최대 크기(x,y,z) : {maxSize.x:0.##}, {maxSize.y:0.##}, {maxSize.z:0.##}, 입력 : {newSize.x:0.##}, {newSize.y:0.##}, {newSize.z:0.##}";
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>(message);
            alarmPopupView?.Show(message);

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

        // Pack은 같은 Container의 다른 Pack / 단독 Module과 겹치면 막는다. 타이핑 중에는 아무것도 바꾸지 않고, 입력을 끝낼 때 / 버튼을 누를 때 현재 크기로 되돌린다.
        if (kind == DeviceType.Pack && DeviceSizeCalculator.OverlapsSiblings(target, current, newSize, out _))
        {
            if (!_log)
            {
                return;
            }

            string message = $"다른 Pack과 겹쳐 크기를 바꿀 수 없습니다. 입력(x,y,z) : {newSize.x:0.##}, {newSize.y:0.##}, {newSize.z:0.##}";
            DebugLogManager.GenerateErrorMessage<UI_DeviceScaleView>(message);
            alarmPopupView?.Show(message);

            SetInput(xAxisInputField, current.size.x);
            SetInput(yAxisInputField, current.size.y);
            SetInput(zAxisInputField, current.size.z);
            return;
        }

        DeviceSizeCalculator.Resize(sizeRenderer, bodyTransform == target, current, newSize);

        // 크기가 바뀌면 부모 안의 자식 사이 실제 간격도 바뀌므로, 부모에 기억된 간격을 현재 값으로 맞춘다.
        DeviceSpacingCalculator.SyncStored(target.parent);
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

    private bool TryGetOwnSize(Transform _target, out Vector3 _size)
    {
        DeviceType kind = mainSceneManager != null ? mainSceneManager.SelectedKind : DeviceType.Pack;
        return DeviceSizeCalculator.TryGetOwnSize(_target, kind, out _size);
    }
    #endregion
}
