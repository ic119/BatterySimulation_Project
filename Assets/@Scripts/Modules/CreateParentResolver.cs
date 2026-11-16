using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Modules
{
    /// <summary>
    /// 새로 만들 장치(Pack / ModuleAssembly / Module / ParallelAssembly / Cell)의 부모를 정하는 규칙. 로그나 팝업은 띄우지 않고 결과만 돌려주며, 어떻게 알릴지는 부르는 쪽이 정한다.
    /// - Pack은 부모가 없다(항상 성공).
    /// - 트리에서 선택된 항목이 없으면 부모 종류의 가장 최근 항목이 부모다.
    /// - 선택된 항목이 부모 종류이면 그 항목이 부모다.
    /// - 선택된 항목이 부모 종류보다 위(조상)이면 그 아래로 마지막 자식을 따라 내려가 부모 종류의 항목을 찾는다. 중간에 자식이 없으면 실패다.
    /// - 선택된 항목이 부모 종류보다 아래이거나 같은 종류이면 실패다.
    /// </summary>
    public static class CreateParentResolver
    {
        public struct Result
        {
            public bool success;

            /// <summary>정해진 부모 모델. Pack을 만들 때는 null이다.</summary>
            public IDeviceNode parent;

            /// <summary>실패했을 때 이유. 성공이면 비어 있다.</summary>
            public string message;

            /// <summary>실패 이유를 사용자에게 팝업으로도 알려야 하는지. false면 로그만 남기면 된다.</summary>
            public bool showAlarm;
        }

        public static Result Resolve(DeviceType _type, DeviceSelection _selection, PackFactory _packFactory)
        {
            Result result = new Result { success = true };

            if (!DeviceHierarchy.TryGetParentKind(_type, out DeviceType parentKind))
            {
                return result;
            }

            if (_selection == null || !_selection.HasSelection)
            {
                result.parent = _packFactory.GetLast(parentKind);
                return result.parent != null ? result : Fail(result, $"{parentKind}이(가) 없어 {_type}을(를) 만들 수 없습니다.");
            }

            int parentDepth = DeviceHierarchy.GetDepth(parentKind);
            int selectedDepth = DeviceHierarchy.GetDepth(_selection.Kind);
            if (selectedDepth > parentDepth)
            {
                return Fail(result, $"{_selection.Kind} 아래에는 {_type}을(를) 만들 수 없습니다. {parentKind}을(를) 선택하세요.");
            }

            IDeviceNode node = _selection.Model;
            for (int depth = selectedDepth; depth < parentDepth; depth++)
            {
                if (node.Children.Count == 0)
                {
                    return Fail(result, $"선택한 {_selection.Kind}에 {parentKind}이(가) 없어 {_type}을(를) 만들 수 없습니다.");
                }

                node = node.Children[node.Children.Count - 1];
            }

            result.parent = node;
            return result;
        }

        private static Result Fail(Result _result, string _message)
        {
            _result.success = false;
            _result.message = _message;
            _result.showAlarm = false;
            return _result;
        }
    }
}
