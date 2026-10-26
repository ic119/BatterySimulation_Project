using UnityEngine;

/// <summary>
/// 시뮬레이션 설정(SimulationDataModel)을 장비 오브젝트(Pack / Module / Cell 등)에 붙여 저장하는 컴포넌트.
/// SimulationDataModel은 순수 데이터 클래스라 GameObject에 직접 붙일 수 없어 이 컴포넌트가 들고 있는다.
/// </summary>
public class SimulationData : MonoBehaviour
{
    [SerializeField] private SimulationDataModel model = new SimulationDataModel();

    public SimulationDataModel Model => model;
}
