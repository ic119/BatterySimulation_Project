using DeviceType = Incheol.Models.Define.DeviceType;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// React가 보낸 저장 모델(Pack ~ Cell, JsonDataLoader가 파싱해 둔 것)을 PackFactory로 씬에 만든다. UI는 알지 못하고, 만들어진 것은 콜백으로 알린다.
    /// 크기와 자리는 DeviceLayout이 계산한 값을 그대로 쓰고(빈 칸 탐색을 하지 않는다), 견본 하나가 여러 개로 복제되면 deviceId와 이름에 번호를 붙인다.
    /// 받은 모델은 "사양"으로만 쓰고 건드리지 않는다. 씬에 연결되는 모델은 새로 만든 것이다.
    /// 시작 모델이 Pack이 아니면(OnPartSelected로 일부 부품만 온 경우) 그 부품을 부모 없이 단독으로 만들고 Cell까지 내려간다.
    /// </summary>
    public class PartBuilder
    {
        private readonly PackFactory packFactory;
        private readonly Action<IDeviceNode> nodeCreated;
        private readonly Dictionary<string, int> idCounts = new Dictionary<string, int>();
        private IReadOnlyDictionary<DeviceType, GameObject> prefabs;

        /// <param name="_nodeCreated">장치가 만들어질 때마다(부모가 먼저, 자식이 나중 순서).</param>
        public PartBuilder(PackFactory _packFactory, Action<IDeviceNode> _nodeCreated)
        {
            packFactory = _packFactory;
            nodeCreated = _nodeCreated;
        }

        /// <summary>
        /// _spec과 그 아래 모든 부품을 만든다. 이미 있는 것은 지우지 않으므로 호출하기 전에 비워 둔다.
        /// 만든 최상단 노드를 돌려주고, 계산이나 생성에 실패하면 에러 로그를 남기고 null이다(이미 만든 일부는 남아 있을 수 있으니 부르는 쪽이 비운다).
        /// </summary>
        public IDeviceNode Build(IDeviceNode _spec, IReadOnlyDictionary<DeviceType, GameObject> _prefabs)
        {
            if (_spec == null || _prefabs == null)
            {
                DebugLogManager.GenerateErrorMessage<PartBuilder>("만들 부품 또는 프리팹이 없습니다.");
                return null;
            }

            LayoutNode layout = DeviceLayout.Compute(_spec, MeasureCellTagLength(_prefabs));
            if (layout == null)
            {
                return null;
            }

            prefabs = _prefabs;
            idCounts.Clear();
            bool standalone = _spec.Kind != DeviceType.pack;
            return BuildNode(layout, null, packFactory.Root.position, standalone);
        }

        /// <summary>
        /// Cell 프리팹의 PlusTag가 Body 끝에서 양쪽으로 나오는 길이의 합(월드 단위, z 방향)을 잰다. Tag는 Body 끝 중심에 놓이므로 Tag의 z 크기와 같다.
        /// 프리팹이나 Tag, 메시를 찾지 못하면 0이다(Tag 길이를 반영하지 않고 Body 크기만으로 배치한다).
        /// </summary>
        private static float MeasureCellTagLength(IReadOnlyDictionary<DeviceType, GameObject> _prefabs)
        {
            if (_prefabs == null || !_prefabs.TryGetValue(DeviceType.cell, out GameObject cellPrefab) || cellPrefab == null)
            {
                return 0.0f;
            }

            Transform tag = cellPrefab.transform.Find(DeviceBody.PlusTagChildName);
            if (tag == null || !tag.TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
            {
                return 0.0f;
            }

            return meshFilter.sharedMesh.bounds.size.z * Mathf.Abs(tag.lossyScale.z);
        }

        private IDeviceNode BuildNode(LayoutNode _layout, IDeviceNode _parent, Vector3 _parentMin, bool _standalone)
        {
            Vector3 min = _parentMin + _layout.LocalMin;
            Vector3 center = min + _layout.Size * 0.5f;
            DeviceType kind = _layout.Kind;
            NextIdentity(_layout.Spec, out string id, out string name);
            prefabs.TryGetValue(kind, out GameObject prefab);

            IDeviceNode node;
            if (kind == DeviceType.cell)
            {
                CellDataModel source = (CellDataModel)_layout.Spec;
                CellDataModel data = new CellDataModel { cellName = name };
                data.CopySpecFrom(source);
                data.jsonPosition = source.jsonPosition;
                data.values.deviceId = id;
                node = packFactory.CreateCell(_parent as ParallelAssemblyDataModel, data, prefab, center, _standalone);
            }
            else
            {
                node = packFactory.Create(kind, _parent, name, prefab, default, null, id, null, _standalone);
                if (node != null && node.Position != null)
                {
                    // 컨테이너 프리팹은 몸체(Renderer)가 없으므로 pivot을 몸체 영역의 최소 모서리에 둔다.
                    node.Position.position = min;
                    CopySpec(node, _layout.Spec);
                }
            }

            if (node == null)
            {
                DebugLogManager.GenerateErrorMessage<PartBuilder>($"{kind} 생성에 실패했습니다. id={id}, name={name}");
                return null;
            }

            node.Values.Capture(node.Position);
            // Cell의 칸(_layout.Size)은 Tag가 나오는 길이까지 포함하므로, 모델에 기록하는 Cell 크기는 Body 크기로 따로 둔다.
            node.Values.size = node is CellDataModel cellNode ? cellNode.BodySize : _layout.Size;
            node.Values.bodyCenter = center;
            nodeCreated?.Invoke(node);

            for (int i = 0; i < _layout.Children.Count; i++)
            {
                if (BuildNode(_layout.Children[i], node, min, false) == null)
                {
                    return null;
                }
            }

            return node;
        }

        /// <summary>견본 하나가 여러 번 만들어질 때 첫 번째는 원래 id/이름, 이후는 "id#2", "이름_2"처럼 번호를 붙인다. id가 없으면 이름으로 센다.</summary>
        private void NextIdentity(IDeviceNode _spec, out string _id, out string _name)
        {
            string baseId = _spec.Values.deviceId;
            string baseName = _spec.Name;
            string key = string.IsNullOrEmpty(baseId) ? $"name:{_spec.Kind}:{baseName}" : baseId;

            idCounts.TryGetValue(key, out int count);
            count++;
            idCounts[key] = count;

            _id = string.IsNullOrEmpty(baseId) ? null : (count == 1 ? baseId : $"{baseId}#{count}");
            _name = count == 1 ? baseName : $"{baseName}_{count}";
        }

        /// <summary>새로 만든 컨테이너 모델에 견본의 JSON 사양(축, topology, 개수, 간격)을 복사한다.</summary>
        private static void CopySpec(IDeviceNode _node, IDeviceNode _spec)
        {
            switch (_node)
            {
                case PackDataModel pack: pack.CopySpecFrom((PackDataModel)_spec); break;
                case ModuleAssemblyDataModel moduleAssembly: moduleAssembly.CopySpecFrom((ModuleAssemblyDataModel)_spec); break;
                case ModuleDataModel module: module.CopySpecFrom((ModuleDataModel)_spec); break;
                case ParallelAssemblyDataModel parallelAssembly: parallelAssembly.CopySpecFrom((ParallelAssemblyDataModel)_spec); break;
            }
        }
    }
}
