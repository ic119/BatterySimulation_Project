using System;
using System.Collections.Generic;
using UnityEngine;

namespace Incheol.Models
{
    /// <summary>
    /// React → Unity 부품 JSON({ requestId, part })의 DTO. JsonUtility용이라 부품 종류(kind)마다 클래스를 나눴다.
    /// - requestId: React가 보낼 때마다 1씩 키운다. Unity는 응답에 그대로 돌려준다.
    /// - JsonUtility는 값이 없는 객체 필드를 null이 아닌 기본 인스턴스로 채우므로, part 존재 여부는 kind로 판단한다.
    /// </summary>
    [Serializable]
    public class PartHeaderJson
    {
        public string kind;
    }

    /// <summary>kind만 먼저 읽어 부품 종류를 판별하기 위한 최상위.</summary>
    [Serializable]
    public class PartHeaderRoot
    {
        public int requestId;
        public PartHeaderJson part;
    }

    /// <summary>종류별 최상위. T는 part의 실제 타입.</summary>
    [Serializable]
    public class PartRoot<T>
    {
        public int requestId;
        public T part;
    }

    [Serializable]
    public class PartGeometryJson
    {
        public string shape;
        public float length;
        public float thickness;
        public float height;
    }

    [Serializable]
    public class PartNodeJson
    {
        public string kind;
        public string id;
        public string name;
        public Vector3 position;
        public string stackingAxis; // "X" | "Y"
    }

    [Serializable]
    public class CellPartJson : PartNodeJson
    {
        public PartGeometryJson geometry;
    }

    [Serializable]
    public class ParallelAssemblyPartJson : PartNodeJson
    {
        public string topology;
        public int numParallelCells; // ≥ 1
        public int rows;             // 1 ~ numParallelCells
        public float interCellGap;   // ≥ 0
        public CellPartJson cell;
    }

    [Serializable]
    public class ModulePartJson : PartNodeJson
    {
        public string topology; // None | Hexagonal | NStack | SingleStack | Square
        public int numSeriesAssemblies;       // ≥ 1
        public float interParallelAssemblyGap; // ≥ 0
        public ParallelAssemblyPartJson parallelAssembly;
    }

    [Serializable]
    public class ModuleAssemblyPartJson : PartNodeJson
    {
        public float interModuleGap; // ≥ 0
        public int numLevels;        // 1 ~ modules 개수
        public List<ModulePartJson> modules = new List<ModulePartJson>();
    }

    [Serializable]
    public class PackPartJson : PartNodeJson
    {
        public float interModuleAssemblyGap; // ≥ 0
        public List<ModuleAssemblyPartJson> moduleAssemblies = new List<ModuleAssemblyPartJson>();
    }
}
