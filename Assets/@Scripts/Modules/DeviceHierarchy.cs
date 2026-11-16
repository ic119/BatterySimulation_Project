using Incheol.Models.Define;
using System.Collections.Generic;

namespace Incheol.Modules
{
    /// <summary>
    /// Pack -> ModuleAssembly -> Module -> ParallelAssembly -> Cell 데이터 계층의 순서와 순회를 다루는 모음. 읽기만 하며 씬 오브젝트는 건드리지 않는다.
    /// </summary>
    public static class DeviceHierarchy
    {
        /// <summary>위에서 아래로의 계층 순서. 인덱스가 깊이다.</summary>
        private static readonly DeviceType[] Order =
        {
            DeviceType.Pack,
            DeviceType.ModuleAssembly,
            DeviceType.Module,
            DeviceType.ParallelAssembly,
            DeviceType.Cell,
        };

        /// <summary>_kind의 깊이(Pack = 0 ... Cell = 4).</summary>
        public static int GetDepth(DeviceType _kind)
        {
            return System.Array.IndexOf(Order, _kind);
        }

        /// <summary>_kind의 바로 위 종류. Pack이면 false.</summary>
        public static bool TryGetParentKind(DeviceType _kind, out DeviceType _parentKind)
        {
            int depth = GetDepth(_kind);
            _parentKind = depth > 0 ? Order[depth - 1] : _kind;
            return depth > 0;
        }

        /// <summary>_kind의 바로 아래 종류. Cell이면 false.</summary>
        public static bool TryGetChildKind(DeviceType _kind, out DeviceType _childKind)
        {
            int depth = GetDepth(_kind);
            bool hasChild = depth >= 0 && depth < Order.Length - 1;
            _childKind = hasChild ? Order[depth + 1] : _kind;
            return hasChild;
        }

        /// <summary>_root 아래(본인 제외)의 _kind 종류 모델을 트리 순서대로 모두 돌려준다. _root가 null이면 빈 목록.</summary>
        public static IEnumerable<IDeviceNode> GetDescendants(IDeviceNode _root, DeviceType _kind)
        {
            if (_root == null)
            {
                yield break;
            }

            foreach (IDeviceNode child in _root.Children)
            {
                if (child == null)
                {
                    continue;
                }

                if (child.Kind == _kind)
                {
                    yield return child;
                }

                foreach (IDeviceNode descendant in GetDescendants(child, _kind))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>_pack 전체의 Module / Cell 수량.</summary>
        public static void CountModulesAndCells(PackDataModel _pack, out int _moduleCount, out int _cellCount)
        {
            _moduleCount = 0;
            _cellCount = 0;

            foreach (IDeviceNode module in GetDescendants(_pack, DeviceType.Module))
            {
                _moduleCount++;
            }

            foreach (IDeviceNode cell in GetDescendants(_pack, DeviceType.Cell))
            {
                _cellCount++;
            }
        }
    }
}
