using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 트리에 놓이고 선택할 수 있는 장치 모델(Pack / ModuleAssembly / Module / ParallelAssembly / Cell)의 공통 형태.
/// 선택 / 트리 / 삭제처럼 종류를 가리지 않는 곳에서 object 대신 이 타입을 쓴다.
/// 계층은 Pack > ModuleAssembly > Module > ParallelAssembly > Cell 이다.
/// </summary>
public interface IDeviceNode
{
    /// <summary>이 모델의 종류.</summary>
    Incheol.Models.Define.DeviceType Kind { get; }

    /// <summary>종류 접두어("Pack " 등)가 없는 이름. packName / moduleAssemblyName / moduleName / parallelAssemblyName / cellName을 그대로 읽고 쓴다.</summary>
    string Name { get; set; }

    /// <summary>이 모델의 씬 오브젝트. 저장하지 않으며 만들어지기 전에는 null.</summary>
    Transform Position { get; }

    /// <summary>이 모델이 들고 있는 위치, 크기, 몸체 중심, 자식 간격. 생성할 때 채워지고 Capture로 갱신한다.</summary>
    DeviceNodeValues Values { get; }

    /// <summary>바로 위 계층의 모델. Pack이면 null.</summary>
    IDeviceNode Parent { get; }

    /// <summary>바로 아래 계층의 모델 목록(복사본). Cell이면 빈 목록.</summary>
    IReadOnlyList<IDeviceNode> Children { get; }
}
