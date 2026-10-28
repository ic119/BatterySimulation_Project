using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Pack의 몸체를 열화상처럼 보이게 한다. 열이 있는 부분은 붉게, 없는 부분은 차가운 색으로 바뀐다.
/// 열원(월드 좌표 / 반경 / 세기)은 밖에서 SetHeatSources()로 넘겨 주고, 이 View는 Custom/PackThermal 셰이더에 전달하는 일만 한다.
/// 값은 공유 머티리얼을 건드리지 않고 MaterialPropertyBlock으로만 넘기므로 Pack마다 따로 동작한다.
/// 몸체 머티리얼이 Custom/PackThermal이 아니면 아무 일도 일어나지 않는다.
/// </summary>
public class PackThermalView : MonoBehaviour
{
    /// <summary>한 지점의 열. position은 월드 좌표이고, strength는 0 ~ 1(여러 열원은 합산한 뒤 1로 잘린다).</summary>
    public struct HeatSource
    {
        public Vector3 position;
        public float radius;
        public float strength;

        /// <summary>열이 번지는 경계 모양을 정하는 값. 시드가 다르면 모양이 다르다.</summary>
        public float seed;

        public HeatSource(Vector3 _position, float _radius, float _strength, float _seed = 0.0f)
        {
            position = _position;
            radius = _radius;
            strength = _strength;
            seed = _seed;
        }
    }

    #region Variable
    /// <summary>셰이더(PackThermal)의 HEAT_MAX와 같아야 한다.</summary>
    public const int MaxHeatSources = 8;

    private static readonly int ThermalAmountId = Shader.PropertyToID("_ThermalAmount");
    private static readonly int HeatCountId = Shader.PropertyToID("_HeatCount");
    private static readonly int HeatPointsId = Shader.PropertyToID("_HeatPoints");
    private static readonly int HeatParamsId = Shader.PropertyToID("_HeatParams");

    [SerializeField, Tooltip("비워 두면 자식 Body(없으면 본인)의 Renderer를 자동으로 찾는다.")] private Renderer bodyRenderer;

    // MonoBehaviour는 필드 초기화에서 MaterialPropertyBlock을 만들 수 없어 처음 쓸 때 만든다.
    private MaterialPropertyBlock propertyBlock;
    private readonly Vector4[] heatPoints = new Vector4[MaxHeatSources];
    private readonly Vector4[] heatParams = new Vector4[MaxHeatSources];

    private int heatCount;
    private float thermalAmount;
    private Tween thermalTween;
    #endregion

    #region Property
    /// <summary>현재 열화상 정도. 0 = 원래 모습, 1 = 완전한 열화상.</summary>
    public float ThermalAmount => thermalAmount;
    public int HeatCount => heatCount;

    /// <summary>열화상이 입혀지는 몸체 Renderer. 지정하지 않았으면 처음 필요할 때 자식 Body(없으면 본인)에서 찾는다. 못 찾으면 null.</summary>
    public Renderer BodyRenderer
    {
        get
        {
            if (bodyRenderer == null)
            {
                DeviceBody.TryGetRenderer(transform, out bodyRenderer);
            }

            return bodyRenderer;
        }
    }
    #endregion

    #region Unity Life Cycle
    private void Awake()
    {
        Apply();
    }

    private void OnDestroy()
    {
        thermalTween?.Kill();
    }
    #endregion

    #region Public Method
    /// <summary>
    /// 열원 목록을 통째로 바꾼다. MaxHeatSources개를 넘는 열원은 무시한다.
    /// 열원의 위치가 월드 좌표이므로 Pack이 움직이면 다시 불러야 한다.
    /// </summary>
    public void SetHeatSources(IReadOnlyList<HeatSource> _sources)
    {
        heatCount = _sources == null ? 0 : Mathf.Min(_sources.Count, MaxHeatSources);

        for (int i = 0; i < MaxHeatSources; i++)
        {
            if (i < heatCount)
            {
                HeatSource source = _sources[i];
                heatPoints[i] = new Vector4(source.position.x, source.position.y, source.position.z, source.radius);
                heatParams[i] = new Vector4(source.strength, source.seed, 0.0f, 0.0f);
            }
            else
            {
                heatPoints[i] = Vector4.zero;
                heatParams[i] = Vector4.zero;
            }
        }

        Apply();
    }

    public void ClearHeatSources()
    {
        SetHeatSources(null);
    }

    /// <summary>열화상 정도를 바로 정한다(0 ~ 1). 진행 중인 전환은 멈춘다.</summary>
    public void SetThermalAmount(float _amount)
    {
        thermalTween?.Kill();
        thermalTween = null;

        thermalAmount = Mathf.Clamp01(_amount);
        Apply();
    }

    /// <summary>열화상 모드를 켜거나 끈다. _duration(초) 동안 서서히 전환하며, 0 이하이면 바로 바꾼다.</summary>
    public void SetThermalMode(bool _on, float _duration = 0.5f)
    {
        float target = _on ? 1.0f : 0.0f;
        if (_duration <= 0.0f)
        {
            SetThermalAmount(target);
            return;
        }

        thermalTween?.Kill();
        thermalTween = DOTween.To(() => thermalAmount, _value =>
            {
                thermalAmount = _value;
                Apply();
            }, target, _duration)
            .SetEase(Ease.InOutSine)
            .SetLink(gameObject);
    }
    #endregion

    #region Private Method
    /// <summary>현재 열화상 정도와 열원 목록을 PropertyBlock으로 몸체 Renderer에 넘긴다.</summary>
    private void Apply()
    {
        // Awake보다 먼저 불려도 동작하도록 Renderer는 처음 필요할 때 찾는다(BodyRenderer).
        Renderer targetRenderer = BodyRenderer;
        if (targetRenderer == null)
        {
            Debug.LogWarning("PackThermalView: 몸체 Renderer를 찾지 못했습니다.", this);
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(ThermalAmountId, thermalAmount);
        propertyBlock.SetFloat(HeatCountId, heatCount);
        propertyBlock.SetVectorArray(HeatPointsId, heatPoints);
        propertyBlock.SetVectorArray(HeatParamsId, heatParams);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
    #endregion
}
