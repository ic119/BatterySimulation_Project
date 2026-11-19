using Incheol.Models.Define;
using Incheol.Utils;
using System.Collections.Generic;
using UnityEngine;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Modules
{
    /// <summary>
    /// 배치 계산 결과 한 칸. 저장 모델(견본) 하나가 씬에서 만들어질 한 개를 뜻한다(견본 하나가 여러 개로 복제되면 노드도 그만큼 생긴다).
    /// 길이는 모두 Unity 월드 단위(m)이고, LocalMin은 부모 몸체 영역의 최소 모서리(min.x, min.y, min.z)를 기준으로 한 이 노드 최소 모서리의 위치다.
    /// </summary>
    public sealed class LayoutNode
    {
        public IDeviceNode Spec;
        public DeviceType Kind;
        public Vector3 Size;
        public Vector3 LocalMin;
        public readonly List<LayoutNode> Children = new List<LayoutNode>();
    }

    /// <summary>
    /// React가 보낸 저장 모델(Pack ~ Cell)에서 만들 부품의 크기와 자리를 계산한다. 씬 오브젝트를 만들지 않는 순수 계산이다.
    /// - 크기는 아래에서 위로 정해진다: Cell(geometry) → ParallelAssembly(Cell을 numParallelCells개) → Module(PA를 numSeriesAssemblies개)
    ///   → ModuleAssembly(Module을 numLevels층으로 같은 수량씩 나눔) → Pack(ModuleAssembly를 한 줄로).
    /// - 쌓는 축: React "Y" → Unity x축, "X" → Unity z축. 층은 Unity +y 방향이다.
    /// - 간격(mm)은 DeviceUnit으로 월드 단위로 바꿔 쓴다. 현재는 topology가 SingleStack(한 줄)인 배치만 지원하고, 그 밖의 값은 경고 후 SingleStack으로 처리한다.
    /// </summary>
    public static class DeviceLayout
    {
        /// <summary>
        /// 견본 모델 _spec의 배치를 계산한다. 계산할 수 없으면(필수 하위 부품이 없거나 Cell 크기가 없음) 에러 로그를 남기고 null.
        /// _cellTagLength는 Cell의 PlusTag / MinusTag가 Body 끝에서 바깥으로 나오는 길이를 합친 값(월드 단위, Cell 길이 방향 z)이다.
        /// 이 값만큼 Cell이 차지하는 칸을 z로 늘리므로, z 방향으로 쌓인 Cell은 Tag끼리 맞닿고 x 방향은 Body끼리 맞닿는다. 0이면 Body 크기만 쓴다.
        /// </summary>
        public static LayoutNode Compute(IDeviceNode _spec, float _cellTagLength = 0.0f)
        {
            float tagLength = Mathf.Max(0.0f, _cellTagLength);
            switch (_spec)
            {
                case PackDataModel pack: return ComputePack(pack, tagLength);
                case ModuleAssemblyDataModel moduleAssembly: return ComputeModuleAssembly(moduleAssembly, tagLength);
                case ModuleDataModel module: return ComputeModule(module, tagLength);
                case ParallelAssemblyDataModel parallelAssembly: return ComputeParallelAssembly(parallelAssembly, tagLength);
                case CellDataModel cell: return ComputeCell(cell, tagLength);
                default:
                    DebugLogManager.GenerateErrorMessage<LayoutNode>("배치를 계산할 수 없는 모델입니다.");
                    return null;
            }
        }

        /// <summary>React 쌓는 축을 Vector3 인덱스로 바꾼다. Y → 0(x), X → 2(z).</summary>
        public static int ToAxisIndex(StackingAxisType _axis)
        {
            return _axis == StackingAxisType.Y ? 0 : 2;
        }

        /// <summary>Cell이 차지하는 칸. 크기는 Body 크기에 Tag가 나오는 길이(z)를 더한 것이다(Tag는 Body 양 끝에 같은 길이씩 나온다).</summary>
        private static LayoutNode ComputeCell(CellDataModel _cell, float _tagLength)
        {
            Vector3 size = _cell.BodySize;
            if (size.x <= 0.0f || size.y <= 0.0f || size.z <= 0.0f)
            {
                DebugLogManager.GenerateErrorMessage<LayoutNode>($"Cell 크기(length/thickness/height)가 없어 만들 수 없습니다. id={_cell.values.deviceId}");
                return null;
            }

            size.z += _tagLength;
            return new LayoutNode { Spec = _cell, Kind = DeviceType.cell, Size = size };
        }

        private static LayoutNode ComputeParallelAssembly(ParallelAssemblyDataModel _pa, float _tagLength)
        {
            if (_pa.cellsList.Count == 0 || _pa.cellsList[0] == null)
            {
                DebugLogManager.GenerateErrorMessage<LayoutNode>($"ParallelAssembly에 cell이 없어 만들 수 없습니다. id={_pa.values.deviceId}");
                return null;
            }

            WarnIfUnsupported(_pa.topology, _pa.values.deviceId);
            LayoutNode cell = ComputeCell(_pa.cellsList[0], _tagLength);
            if (cell == null)
            {
                return null;
            }

            LayoutNode node = new LayoutNode { Spec = _pa, Kind = DeviceType.parallelassembly };
            StackCopies(node, cell, Mathf.Max(1, _pa.numParallelCells), ToAxisIndex(_pa.stackingAxis), DeviceUnit.FromMm(Mathf.Max(0.0f, _pa.interCellGap)));
            return node;
        }

        private static LayoutNode ComputeModule(ModuleDataModel _module, float _tagLength)
        {
            if (_module.parallelAssembliesList.Count == 0 || _module.parallelAssembliesList[0] == null)
            {
                DebugLogManager.GenerateErrorMessage<LayoutNode>($"Module에 parallelAssembly가 없어 만들 수 없습니다. id={_module.values.deviceId}");
                return null;
            }

            WarnIfUnsupported(_module.topology, _module.values.deviceId);
            LayoutNode parallelAssembly = ComputeParallelAssembly(_module.parallelAssembliesList[0], _tagLength);
            if (parallelAssembly == null)
            {
                return null;
            }

            LayoutNode node = new LayoutNode { Spec = _module, Kind = DeviceType.module };
            StackCopies(node, parallelAssembly, Mathf.Max(1, _module.numSeriesAssemblies), ToAxisIndex(_module.stackingAxis), DeviceUnit.FromMm(Mathf.Max(0.0f, _module.interParallelAssemblyGap)));
            return node;
        }

        private static LayoutNode ComputeModuleAssembly(ModuleAssemblyDataModel _ma, float _tagLength)
        {
            int moduleCount = _ma.modulesList.Count;
            if (moduleCount == 0)
            {
                DebugLogManager.GenerateErrorMessage<LayoutNode>($"ModuleAssembly에 module이 없어 만들 수 없습니다. id={_ma.values.deviceId}");
                return null;
            }

            // Module을 numLevels층에 같은 수량씩 나눈다. 나누어떨어지지 않으면 마지막 층이 모자란다.
            int levels = Mathf.Clamp(_ma.numLevels, 1, moduleCount);
            int perLevel = Mathf.CeilToInt(moduleCount / (float)levels);
            if (moduleCount % levels != 0)
            {
                DebugLogManager.GenerateWarningMessage<LayoutNode>($"module {moduleCount}개를 {levels}층에 같은 수량으로 나눌 수 없습니다(층당 {perLevel}개, 마지막 층은 모자랍니다). id={_ma.values.deviceId}");
            }

            int axis = ToAxisIndex(_ma.stackingAxis);
            float gap = DeviceUnit.FromMm(Mathf.Max(0.0f, _ma.interModuleGap));
            LayoutNode node = new LayoutNode { Spec = _ma, Kind = DeviceType.moduleassembly };
            Vector3 size = default;
            float y = 0.0f;
            float maxExtent = 0.0f;

            for (int start = 0; start < moduleCount; start += perLevel)
            {
                int end = Mathf.Min(moduleCount, start + perLevel);
                float cursor = 0.0f;
                float levelHeight = 0.0f;
                for (int i = start; i < end; i++)
                {
                    LayoutNode child = ComputeModule(_ma.modulesList[i], _tagLength);
                    if (child == null)
                    {
                        return null;
                    }

                    Vector3 min = new Vector3(0.0f, y, 0.0f);
                    min[axis] = cursor;
                    child.LocalMin = min;
                    node.Children.Add(child);

                    cursor += child.Size[axis] + gap;
                    levelHeight = Mathf.Max(levelHeight, child.Size.y);
                    size = Vector3.Max(size, child.Size);
                }

                maxExtent = Mathf.Max(maxExtent, cursor - gap);
                y += levelHeight + gap;
            }

            size.y = y - gap;
            size[axis] = maxExtent;
            node.Size = size;
            return node;
        }

        private static LayoutNode ComputePack(PackDataModel _pack, float _tagLength)
        {
            if (_pack.moduleAssembliesList.Count == 0)
            {
                DebugLogManager.GenerateErrorMessage<LayoutNode>($"Pack에 moduleAssembly가 없어 만들 수 없습니다. id={_pack.values.deviceId}");
                return null;
            }

            LayoutNode node = new LayoutNode { Spec = _pack, Kind = DeviceType.pack };
            List<LayoutNode> children = new List<LayoutNode>();
            for (int i = 0; i < _pack.moduleAssembliesList.Count; i++)
            {
                LayoutNode child = ComputeModuleAssembly(_pack.moduleAssembliesList[i], _tagLength);
                if (child == null)
                {
                    return null;
                }

                children.Add(child);
            }

            Stack(node, children, ToAxisIndex(_pack.stackingAxis), DeviceUnit.FromMm(Mathf.Max(0.0f, _pack.interModuleAssemblyGap)));
            return node;
        }

        /// <summary>_template과 같은 배치의 노드를 _count개 복제해 _axis 방향으로 _gap 간격을 두고 한 줄로 쌓는다.</summary>
        private static void StackCopies(LayoutNode _parent, LayoutNode _template, int _count, int _axis, float _gap)
        {
            List<LayoutNode> items = new List<LayoutNode>(_count);
            for (int i = 0; i < _count; i++)
            {
                items.Add(i == 0 ? _template : Clone(_template));
            }

            Stack(_parent, items, _axis, _gap);
        }

        /// <summary>_items를 _axis(0 = x, 2 = z) 방향으로 차례로 놓고 _parent의 크기를 정한다. 다른 축은 모두 최소 모서리에 맞춘다.</summary>
        private static void Stack(LayoutNode _parent, List<LayoutNode> _items, int _axis, float _gap)
        {
            float cursor = 0.0f;
            Vector3 size = default;
            for (int i = 0; i < _items.Count; i++)
            {
                Vector3 min = default;
                min[_axis] = cursor;
                _items[i].LocalMin = min;
                _parent.Children.Add(_items[i]);

                cursor += _items[i].Size[_axis] + _gap;
                size = Vector3.Max(size, _items[i].Size);
            }

            size[_axis] = cursor - _gap;
            _parent.Size = size;
        }

        private static LayoutNode Clone(LayoutNode _source)
        {
            LayoutNode copy = new LayoutNode { Spec = _source.Spec, Kind = _source.Kind, Size = _source.Size, LocalMin = _source.LocalMin };
            for (int i = 0; i < _source.Children.Count; i++)
            {
                copy.Children.Add(Clone(_source.Children[i]));
            }

            return copy;
        }

        private static void WarnIfUnsupported(TopologyType _topology, string _id)
        {
            if (_topology != TopologyType.SingleStack)
            {
                DebugLogManager.GenerateWarningMessage<LayoutNode>($"topology '{_topology}'는 아직 지원하지 않아 SingleStack(한 줄)으로 배치합니다. id={_id}");
            }
        }
    }
}
