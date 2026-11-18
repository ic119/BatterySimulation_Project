using DeviceType = Incheol.Models.Define.DeviceType;
using System.Collections.Generic;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// 여러 장치를 한꺼번에 보이게 하는 표시 규칙(React의 OnEssListUpdated). 선택 하나를 다루는 DeviceVisibility와 달리 "보일 장치의 집합"을 다룬다.
    /// 오브젝트는 비활성화하지 않고 Renderer만 켜고 끈다(DeviceVisibility와 같은 규칙).
    /// 기본 상태는 모두 숨김이므로 원복은 HideAll이다.
    /// </summary>
    public static class DeviceViewFilter
    {
        /// <summary>_factory가 가진 모든 Pack(과 그 아래 전부)의 Renderer를 끈다.</summary>
        public static void HideAll(PackFactory _factory)
        {
            if (_factory == null)
            {
                return;
            }

            IReadOnlyList<IDeviceNode> packList = _factory.GetNodes(DeviceType.pack);
            for (int i = 0; i < packList.Count; i++)
            {
                DeviceVisibility.SetVisible(packList[i].Position, false);
            }
        }

        /// <summary>
        /// 모두 숨긴 뒤 _nodes만 보이게 한다. Pack / ModuleAssembly / Module / ParallelAssembly는 자식까지 모두 켜고,
        /// Cell은 그 Cell만 켠다. DeviceVisibility의 Cell 규칙(같은 루트의 나머지를 모두 끈다)을 쓰면 먼저 켠 항목이 꺼지므로 쓰지 않는다.
        /// </summary>
        public static void ShowOnly(PackFactory _factory, IReadOnlyList<IDeviceNode> _nodes)
        {
            HideAll(_factory);

            if (_nodes == null)
            {
                return;
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                Transform target = _nodes[i] != null ? _nodes[i].Position : null;
                if (target == null)
                {
                    continue;
                }

                if (_nodes[i].Kind == DeviceType.cell)
                {
                    Renderer[] rendererArray = target.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < rendererArray.Length; r++)
                    {
                        rendererArray[r].enabled = true;
                    }
                }
                else
                {
                    DeviceVisibility.SetVisible(target, true, _nodes[i].Kind);
                }
            }
        }
    }
}
