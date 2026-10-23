using Incheol.Modules;
using UnityEngine;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Presenter
{
    /// <summary>
    /// 선택된 항목(DeviceSelection)에 따라 달라지는 패널(속성 / 크기 / 간격 / 구조 / 이름 / 생성 수량 / 시뮬레이션 이름, 축 그래프)을 갱신하고,
    /// 선택된 항목의 이름을 바꾼다. 선택을 바꾸거나 카메라를 움직이는 일은 하지 않고(MainSceneManager가 한다), 이미 정해진 선택을 화면에 반영만 한다.
    /// 프리팹이 만들어지기 전에는 뷰가 null이므로 각 뷰는 null이면 건너뛴다.
    /// </summary>
    public class SelectionPanels
    {
        private readonly DeviceSelection selection;

        private UI_PropertiesView propertiesView;
        private UI_DeviceScaleView scaleView;
        private UI_SubDeviceSpacingView spacingView;
        private UI_DeviceStructureView structureView;
        private UI_SelectedDeviceNameView nameView;
        private UI_CreateDeviceCount createCountView;
        private UI_SimulationControl simulationControl;
        private UI_EditorView editorView;
        private UI_EditorView simulationTreeView;

        public SelectionPanels(DeviceSelection _selection)
        {
            selection = _selection;
        }

        /// <summary>인스턴스화한 UI에서 찾은 뷰를 넘겨받는다. 없는 뷰는 null이어도 된다.</summary>
        public void Bind(
            UI_PropertiesView _propertiesView,
            UI_DeviceScaleView _scaleView,
            UI_SubDeviceSpacingView _spacingView,
            UI_DeviceStructureView _structureView,
            UI_SelectedDeviceNameView _nameView,
            UI_CreateDeviceCount _createCountView,
            UI_SimulationControl _simulationControl,
            UI_EditorView _editorView,
            UI_EditorView _simulationTreeView)
        {
            propertiesView = _propertiesView;
            scaleView = _scaleView;
            spacingView = _spacingView;
            structureView = _structureView;
            nameView = _nameView;
            createCountView = _createCountView;
            simulationControl = _simulationControl;
            editorView = _editorView;
            simulationTreeView = _simulationTreeView;
        }

        /// <summary>선택이 바뀌었을 때(선택 / 해제) 선택에 따라 달라지는 패널을 모두 갱신한다.</summary>
        public void RefreshAll()
        {
            RefreshGraph();
            RefreshProperties();
            RefreshName();
            RefreshStructure();
        }

        /// <summary>구조 / 속성 패널만 갱신한다. 선택은 그대로이고 트리만 바뀌었을 때 쓴다.</summary>
        public void RefreshTree()
        {
            RefreshStructure();
            RefreshProperties();
        }

        /// <summary>간격 패널에 알람을 띄운다.</summary>
        public void ShowSpacingAlarm(string _message)
        {
            spacingView?.ShowAlarm(_message);
        }

        /// <summary>
        /// 선택된 항목의 이름을 _newName으로 바꾼다. 모델 / 씬 오브젝트 / 트리 줄이 즉시 바뀌고 속성 패널 이름도 갱신된다.
        /// 선택이 없거나 이름이 비어 있으면 바꾸지 않고 입력 필드를 현재 이름으로 되돌린다.
        /// </summary>
        public void RenameSelected(string _newName)
        {
            if (selection.Model == null)
            {
                return;
            }

            if (editorView != null && editorView.Rename(selection.Model, _newName))
            {
                simulationTreeView?.Rename(selection.Model, _newName);
                RefreshProperties();
            }

            RefreshName();
        }

        /// <summary>
        /// 선택이 없으면 모든 축 그래프를 숨기고, 있으면 그 항목(selection.Target)의 그래프만 보이게 한다.
        /// 그래프는 DeviceAxisGraphManager가 선택된 항목에서만 만들고 그린다.
        /// </summary>
        private void RefreshGraph()
        {
            if (selection.Model == null)
            {
                DeviceAxisGraphManager.SetSelectedOwner(null);
                return;
            }

            DeviceAxisGraphManager.SetSelectedOwner(selection.Target);
        }

        /// <summary>선택된 항목에 트리가 표시하는 이름. 선택이 없거나 트리 뷰가 없으면 빈 문자열.</summary>
        private string GetSelectedLabel()
        {
            return editorView != null ? editorView.GetLabel(selection.Model) : string.Empty;
        }

        /// <summary>
        /// 선택된 항목 본인의 자식 구조(Pack이면 Module / Cell, Module이면 Cell)를 구조 패널에 보여준다. 선택이 없으면 패널을 비운다.
        /// 선택한 항목 아래에 새 자식이 생길 때도 UI_EditorView가 호출해 갱신한다. 패널과 트리 뷰는 비동기로 생성되므로 필요할 때 찾는다.
        /// </summary>
        private void RefreshStructure()
        {
            if (structureView == null)
            {
                return;
            }

            if (selection.Model == null)
            {
                structureView.Clear();
                return;
            }

            if (editorView == null)
            {
                structureView.Clear();
                return;
            }

            structureView.Show(editorView.GetLabel(selection.Model), editorView.GetChildStructure(selection.Model));
        }

        /// <summary>
        /// 선택된 항목의 이름과 위치를 속성 패널에, 실제 크기를 크기 패널에 반영한다. 선택이 없으면 패널을 비운다.
        /// 선택한 항목 아래에 자식이 생겨 크기가 변할 수 있어 TreeChanged를 받을 때도 호출한다.
        /// </summary>
        public void RefreshProperties()
        {
            bool hasSelection = selection.HasSelection;

            // Container는 빈 오브젝트라 몸체 크기가 없으므로 크기 패널은 비워 둔다(자식 Pack 사이의 간격은 간격 패널로 조절한다).
            bool hasBody = hasSelection && selection.Kind != DeviceType.Container;

            if (propertiesView != null)
            {
                if (hasSelection)
                {
                    propertiesView.ShowSelected(GetSelectedLabel(), selection.Target.position);
                }
                else
                {
                    propertiesView.Clear();
                }
            }

            if (scaleView != null)
            {
                if (hasBody)
                {
                    scaleView.ShowSelected(selection.Target);
                }
                else
                {
                    scaleView.Clear();
                }
            }

            if (spacingView != null)
            {
                if (hasSelection)
                {
                    spacingView.ShowSelected(selection.Target);
                }
                else
                {
                    spacingView.Clear();
                }
            }

            // Container가 선택됐을 때만 Pack 생성 수량 패널을 보여준다.
            createCountView?.Refresh();
        }

        /// <summary>
        /// 선택이 없으면 이름 변경 입력을 비우고 막고, 있으면 그 항목의 이름을 changeNameInputField에 채운다.
        /// 입력 중인 글자를 덮어쓰지 않도록 선택이 바뀔 때와 이름을 바꾼 뒤에만 호출한다.
        /// </summary>
        private void RefreshName()
        {
            simulationControl?.RefreshName();

            if (nameView == null)
            {
                return;
            }

            if (selection.Model == null)
            {
                nameView.Clear();
                return;
            }

            nameView.ShowSelected(GetSelectedLabel());
        }
    }
}
