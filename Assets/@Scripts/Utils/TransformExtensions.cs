using UnityEngine;

namespace Incheol.Utils
{
    /// <summary>Transform 확장 메서드 모음.</summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// 자식을 모두 지운다. Destroy는 프레임 끝에 처리되므로 먼저 부모에서 떼어,
        /// 바로 이어서 새 자식을 만들어도 옛 자식이 레이아웃에 남아 섞이거나 순서가 어긋나지 않게 한다.
        /// </summary>
        public static void DestroyChildren(this Transform _parent)
        {
            for (int i = _parent.childCount - 1; i >= 0; i--)
            {
                Transform child = _parent.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }
    }
}
