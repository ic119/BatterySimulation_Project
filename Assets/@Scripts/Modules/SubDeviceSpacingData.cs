using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// 항목(Pack / Module)에 적용한 자식 간격을 축별로 기억한다. 새 자식이 생길 때 같은 간격을 다시 적용하는 데 쓴다.
    /// </summary>
    public class SubDeviceSpacingData : MonoBehaviour
    {
        private readonly float[] spacingArray = new float[3];
        private readonly bool[] appliedArray = new bool[3];

        public bool TryGet(int _axis, out float _spacing)
        {
            _spacing = spacingArray[_axis];
            return appliedArray[_axis];
        }

        public void Set(int _axis, float _spacing)
        {
            spacingArray[_axis] = _spacing;
            appliedArray[_axis] = true;
        }
    }
}
