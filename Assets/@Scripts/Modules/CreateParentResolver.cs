using System;
using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Modules
{
    /// <summary>
    /// 새로 만들 Pack / Module / Cell의 부모를 정하는 규칙. 로그나 팝업은 띄우지 않고 결과만 돌려주며, 어떻게 알릴지는 부르는 쪽이 정한다.
    /// - 트리에서 선택된 항목이 없으면 가장 최근 Pack / Module이 부모다.
    /// - Container가 선택되어 있으면 Pack은 그 Container에, Module은 그 Container의 마지막 Pack에(Pack이 없으면 단독 Module로) 만든다.
    /// - Pack이 선택되어 있으면 Module은 그 Pack에, Cell은 그 Pack의 마지막 Module에 만든다.
    /// - Module이 선택되어 있으면 Cell은 그 Module에 만든다.
    /// - 선택된 항목 아래에 만들 수 없는 종류거나 부모를 찾지 못하면 실패다. 선택된 항목의 모델을 찾지 못하면 선택을 무시한다.
    /// </summary>
    public static class CreateParentResolver
    {
        public struct Result
        {
            public bool success;
            public PackDataModel parentPack;
            public ModuleDataModel parentModule;
            public ContainerDataModel parentContainer;

            /// <summary>실패했을 때 이유. 성공이면 비어 있다.</summary>
            public string message;

            /// <summary>실패 이유를 사용자에게 팝업으로도 알려야 하는지. false면 로그만 남기면 된다.</summary>
            public bool showAlarm;
        }

        /// <summary>
        /// _type(Pack / Module / Cell)의 부모를 정한다.
        /// _getPackFullMessage는 Container에 Pack을 더 놓을 수 없을 때 그 안내 문구를, 놓을 수 있으면 null을 돌려준다(없으면 검사하지 않는다).
        /// </summary>
        public static Result Resolve(DeviceType _type, DeviceSelection _selection, PackFactory _packFactory, Func<ContainerDataModel, string> _getPackFullMessage = null)
        {
            Result result = new Result
            {
                success = true,
                parentPack = _packFactory.LastPack,
                parentModule = _packFactory.LastModule,
            };

            if (_selection == null || !_selection.HasSelection)
            {
                return result;
            }

            switch (_selection.Kind)
            {
                case DeviceType.Container:
                    {
                        ContainerDataModel selectedContainer = _packFactory.FindContainer(_selection.Target);
                        if (selectedContainer == null)
                        {
                            break;
                        }

                        if (_type == DeviceType.Pack)
                        {
                            string fullMessage = _getPackFullMessage?.Invoke(selectedContainer);
                            if (fullMessage != null)
                            {
                                return Fail(result, fullMessage, true);
                            }

                            result.parentContainer = selectedContainer;
                            return result;
                        }

                        if (_type == DeviceType.Module)
                        {
                            // 하위에 Pack이 있으면 마지막 Pack의 자식으로, 없을 때만 Pack 없는 Module로 만든다.
                            result.parentPack = selectedContainer.packsList.Count > 0 ? selectedContainer.packsList[selectedContainer.packsList.Count - 1] : null;
                            result.parentContainer = selectedContainer;
                            return result;
                        }

                        return Fail(result, "Container 아래에는 Cell을 직접 만들 수 없습니다. Module을 선택하세요.", false);
                    }
                case DeviceType.Pack:
                    {
                        PackDataModel selectedPack = _packFactory.FindPack(_selection.Target);
                        if (selectedPack == null)
                        {
                            break;
                        }

                        if (_type == DeviceType.Module)
                        {
                            result.parentPack = selectedPack;
                            return result;
                        }

                        if (_type == DeviceType.Cell)
                        {
                            if (selectedPack.modulesList.Count == 0)
                            {
                                return Fail(result, "선택한 Pack에 Module이 없어 Cell을 만들 수 없습니다.", false);
                            }

                            result.parentModule = selectedPack.modulesList[selectedPack.modulesList.Count - 1];
                            return result;
                        }

                        return Fail(result, "Pack 아래에는 Pack을 만들 수 없습니다.", false);
                    }
                case DeviceType.Module:
                    {
                        ModuleDataModel selectedModule = _packFactory.FindModule(_selection.Target);
                        if (selectedModule == null)
                        {
                            break;
                        }

                        if (_type == DeviceType.Cell)
                        {
                            result.parentModule = selectedModule;
                            return result;
                        }

                        return Fail(result, $"Module 아래에는 {_type}을(를) 만들 수 없습니다.", false);
                    }
                case DeviceType.Cell:
                    return Fail(result, $"Cell 아래에는 {_type}을(를) 만들 수 없습니다.", false);
            }

            // 선택된 항목의 데이터 모델을 찾지 못하면 선택을 무시하고 가장 최근 오브젝트를 부모로 쓴다.
            return result;
        }

        private static Result Fail(Result _result, string _message, bool _showAlarm)
        {
            _result.success = false;
            _result.message = _message;
            _result.showAlarm = _showAlarm;
            return _result;
        }
    }
}
