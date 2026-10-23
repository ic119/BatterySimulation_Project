using DeviceType = Incheol.Models.Define.DeviceType;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>Pack / Module / Cell 오브젝트의 표시(Renderer on/off) 규칙.</summary>
    public static class DeviceVisibility
    {
        /// <summary>
        /// _visible이 false면 _target과 그 자식들의 모든 Renderer를 끈다.
        /// true일 때 Pack / Module은 자식까지 모든 Renderer를 켜고, Cell은 본인의 Renderer만 켜고 같은 루트 아래의 나머지 Renderer는 모두 끈다.
        /// 오브젝트는 비활성화하지 않아 자식만 따로 보여줄 수 있다.
        /// </summary>
        public static void SetVisible(Transform _target, bool _visible, DeviceType _kind = DeviceType.Pack)
        {
            if (_target == null)
            {
                return;
            }

            if (_visible && _kind == DeviceType.Cell)
            {
                Renderer[] allRendererArray = _target.root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < allRendererArray.Length; i++)
                {
                    // IsChildOf는 자기 자신도 포함하므로 Cell 본인(과 Cell 아래)만 켜진다.
                    allRendererArray[i].enabled = allRendererArray[i].transform.IsChildOf(_target);
                }

                return;
            }

            Renderer[] rendererArray = _target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rendererArray.Length; i++)
            {
                rendererArray[i].enabled = _visible;
            }
        }
    }
}
