using Incheol.Models.Define;
using Incheol.Presenter;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UI_CreateDeviceCount : MonoBehaviour
{
    #region Variable
    [Header("Create Device Count InputField")]
    [SerializeField] private TMP_InputField xInputField;
    [SerializeField] private TMP_InputField zInputField;

    [Header("Create Device Count Buttons")]
    [SerializeField] private Button xUpButton;
    [SerializeField] private Button xDownButton;
    [SerializeField] private Button zUpButton;
    [SerializeField] private Button zDownButton;

    [Header("Container")]
    [SerializeField] private GameObject container;

    private const int StepAmount = 1;

    // Container마다 x * z개까지만 Pack을 만들 수 있다. 설정한 적 없는 Container는 1 * 1이다.
    private readonly Dictionary<ContainerDataModel, Vector2Int> countDictionary = new Dictionary<ContainerDataModel, Vector2Int>();

    private MainSceneManager mainSceneManager;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        AddStepListener(xUpButton, OnClickXUp);
        AddStepListener(xDownButton, OnClickXDown);
        AddStepListener(zUpButton, OnClickZUp);
        AddStepListener(zDownButton, OnClickZDown);

        xInputField?.onEndEdit.AddListener(OnXEndEdit);
        zInputField?.onEndEdit.AddListener(OnZEndEdit);
    }

    private void OnDestroy()
    {
        RemoveStepListener(xUpButton, OnClickXUp);
        RemoveStepListener(xDownButton, OnClickXDown);
        RemoveStepListener(zUpButton, OnClickZUp);
        RemoveStepListener(zDownButton, OnClickZDown);

        xInputField?.onEndEdit.RemoveListener(OnXEndEdit);
        zInputField?.onEndEdit.RemoveListener(OnZEndEdit);
    }
    #endregion

    #region Method
    /// <summary>MainSceneManager가 프리팹을 인스턴스화한 직후 자신을 넘겨준다. 뷰가 씬에서 직접 찾지 않는다.</summary>
    public void Init(MainSceneManager _manager)
    {
        mainSceneManager = _manager;
        Refresh();
    }

    /// <summary>
    /// 선택된 항목이 Container일 때만 container를 켜고 그 Container의 x / z 수량을 입력 필드에 채운다. 그 밖에는 container를 끈다.
    /// 선택이 바뀔 때마다 MainSceneManager가 호출한다. 입력 필드는 알림 없이 채우므로 이 호출이 수량을 다시 바꾸지는 않는다.
    /// </summary>
    public void Refresh()
    {
        ContainerDataModel selected = GetSelectedContainer();
        if (container != null)
        {
            container.SetActive(selected != null);
        }

        if (selected == null)
        {
            SetInputText(xInputField, string.Empty);
            SetInputText(zInputField, string.Empty);
            return;
        }

        Vector2Int count = GetCount(selected);
        SetInputText(xInputField, count.x.ToString(CultureInfo.InvariantCulture));
        SetInputText(zInputField, count.y.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// _container 안에 Pack을 하나 더 만들 수 있는지. 현재 Pack 개수(_currentPackCount)가 x * z보다 적을 때만 true.
    /// 수량을 설정하지 않은 Container는 1 * 1 = 1개까지다.
    /// </summary>
    public bool CanCreatePack(ContainerDataModel _container, int _currentPackCount)
    {
        if (_container == null)
        {
            return true;
        }

        Vector2Int count = GetCount(_container);
        return _currentPackCount < count.x * count.y;
    }

    /// <summary>_container에 설정된 최대 Pack 수(x * z). 설정하지 않았으면 1.</summary>
    public bool TryGetMaxPackCount(ContainerDataModel _container, out int _max)
    {
        _max = 0;
        if (_container == null)
        {
            return false;
        }

        Vector2Int count = GetCount(_container);
        _max = count.x * count.y;
        return true;
    }

    /// <summary>_container의 x / z 수량. 설정하지 않았으면 기본값(1, 1).</summary>
    private Vector2Int GetCount(ContainerDataModel _container)
    {
        return countDictionary.TryGetValue(_container, out Vector2Int count) ? count : Vector2Int.one;
    }

    private ContainerDataModel GetSelectedContainer()
    {
        if (mainSceneManager == null || mainSceneManager.SelectedKind != MainSceneManager.TargetKind.Container)
        {
            return null;
        }

        return mainSceneManager.SelectedModel as ContainerDataModel;
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

    private void OnClickXUp() => ChangeCount(true, StepAmount);
    private void OnClickXDown() => ChangeCount(true, -StepAmount);
    private void OnClickZUp() => ChangeCount(false, StepAmount);
    private void OnClickZDown() => ChangeCount(false, -StepAmount);

    private void OnXEndEdit(string _text) => ApplyCount(true, TryParse(_text, out int v) ? v : (int?)null);
    private void OnZEndEdit(string _text) => ApplyCount(false, TryParse(_text, out int v) ? v : (int?)null);

    /// <summary>현재 수량에 _delta를 더해 적용한다. 0보다 작아지면 바꾸지 않는다.</summary>
    private void ChangeCount(bool _isX, int _delta)
    {
        ContainerDataModel selected = GetSelectedContainer();
        if (selected == null)
        {
            return;
        }

        Vector2Int count = GetCount(selected);
        int next =(_isX ? count.x : count.y) + _delta;
        if (next < 0)
        {
            return;
        }

        ApplyCount(_isX, next);
    }

    /// <summary>
    /// 선택된 Container의 x 또는 z 수량을 _value로 바꾼다.
    /// 숫자가 아니거나 0보다 작은 값이면 바꾸지 않고 입력 필드를 현재 값으로 되돌린다.
    /// </summary>
    private void ApplyCount(bool _isX, int? _value)
    {
        ContainerDataModel selected = GetSelectedContainer();
        if (selected == null)
        {
            return;
        }

        if (!_value.HasValue || _value.Value < 0)
        {
            Refresh();
            return;
        }

        Vector2Int count = GetCount(selected);
        if (_isX)
        {
            count.x = _value.Value;
        }
        else
        {
            count.y = _value.Value;
        }

        countDictionary[selected] = count;
        Refresh();
    }

    private static bool TryParse(string _text, out int _value)
    {
        _value = 0;
        string text = _text.Replace("​", string.Empty).Trim();
        return text.Length > 0 && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _value);
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
