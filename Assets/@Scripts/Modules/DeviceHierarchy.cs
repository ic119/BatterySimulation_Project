using System.Collections.Generic;

namespace Incheol.Modules
{
    /// <summary>
    /// Container -> Pack -> Module -> Cell 데이터 계층을 순회하는 모음. 목록이나 항목이 null이면 건너뛴다.
    /// 읽기만 하며 씬 오브젝트는 건드리지 않는다.
    /// </summary>
    public static class DeviceHierarchy
    {
        public static IEnumerable<PackDataModel> GetPacks(ContainerDataModel _container)
        {
            if (_container == null || _container.packsList == null)
            {
                yield break;
            }

            foreach (PackDataModel pack in _container.packsList)
            {
                if (pack != null)
                {
                    yield return pack;
                }
            }
        }

        public static IEnumerable<ModuleDataModel> GetModules(PackDataModel _pack)
        {
            if (_pack == null || _pack.modulesList == null)
            {
                yield break;
            }

            foreach (ModuleDataModel module in _pack.modulesList)
            {
                if (module != null)
                {
                    yield return module;
                }
            }
        }

        public static IEnumerable<CellDataModel> GetCells(ModuleDataModel _module)
        {
            if (_module == null || _module.cellsList == null)
            {
                yield break;
            }

            foreach (CellDataModel cell in _module.cellsList)
            {
                if (cell != null)
                {
                    yield return cell;
                }
            }
        }

        /// <summary>_container 전체의 Module / Cell 수량.</summary>
        public static void CountModulesAndCells(ContainerDataModel _container, out int _moduleCount, out int _cellCount)
        {
            _moduleCount = 0;
            _cellCount = 0;

            foreach (PackDataModel pack in GetPacks(_container))
            {
                foreach (ModuleDataModel module in GetModules(pack))
                {
                    _moduleCount++;
                    foreach (CellDataModel cell in GetCells(module))
                    {
                        _cellCount++;
                    }
                }
            }
        }
    }
}
