using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// 트리에서 선택된 항목(항상 0개 또는 1개)의 상태. 선택을 바꾸는 일은 MainSceneManager만 하고,
    /// 뷰들은 이 객체를 넘겨받아 읽기만 한다(뷰가 MainSceneManager를 직접 참조하지 않게 하기 위함).
    /// </summary>
    public class DeviceSelection
    {
        /// <summary>
        /// 현재 선택된 항목의 모델(Container / Pack / Module / Cell) (없을 경우 Null 처리)
        /// </summary>
        public IDeviceNode Model { get; private set; }

        /// <summary>
        /// 현재 선택된 항목이 가리키는 SceneObject의 위치 (없을 경우 Null 처리)
        /// </summary>
        public Transform Target { get; private set; }

        /// <summary>
        /// 현재 선택된 항목의 종류. 
        /// 선택이 없으면 의미 없는 값(Pack)이므로 HasSelection이 true일 때만 사용
        /// </summary>
        public DeviceType Kind => Model != null ? Model.Kind : DeviceType.Pack;

        /// <summary>
        /// 모델과 씬 오브젝트가 모두 있는 선택이 있는지의 여부
        /// </summary>
        public bool HasSelection => Model != null && Target != null;

        public void Set(IDeviceNode _model, Transform _target)
        {
            Model = _model;
            Target = _target;
        }

        public void Clear()
        {
            Model = null;
            Target = null;
        }
    }
}
