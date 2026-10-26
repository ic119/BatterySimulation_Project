using Incheol.Models.Define;
using System;
using System.Collections.Generic;

/// <summary>
/// 시뮬레이션 작업 1개의 정보. SimulationDataModel의 stepList에 실행 순서대로 담긴다.
/// </summary>
[Serializable]
public class SimulationStepModel
{
    public SimulationType simulationType;
    public float duringTime;
}

/// <summary>
/// 장비 1개에 저장되는 시뮬레이션 정보. 시뮬레이션 작업을 n개 가질 수 있으며 stepList의 순서가 실행 순서이다.
/// (Unity가 직렬화하지 못하는 Queue 대신 List를 쓰고, 실행할 때 필요하면 Queue로 복사해서 쓴다.)
/// </summary>
[Serializable]
public class SimulationDataModel
{
    public string deviceName;
    public DeviceType deviceType;
    public List<SimulationStepModel> stepList = new List<SimulationStepModel>();

    /// <summary>작업 개수</summary>
    public int StepCount => stepList.Count;

    /// <summary>작업 전체의 지속 시간 합</summary>
    public float TotalDuringTime
    {
        get
        {
            float total = 0f;
            foreach (SimulationStepModel step in stepList)
            {
                total += step.duringTime;
            }

            return total;
        }
    }

    /// <summary>작업을 맨 뒤에 추가한다.</summary>
    public void AddStep(SimulationType _simulationType, float _duringTime)
    {
        stepList.Add(new SimulationStepModel { simulationType = _simulationType, duringTime = _duringTime });
    }

    /// <summary>_index번째 작업을 지운다. 범위 밖이면 false.</summary>
    public bool RemoveStepAt(int _index)
    {
        if (_index < 0 || _index >= stepList.Count)
        {
            return false;
        }

        stepList.RemoveAt(_index);
        return true;
    }

    /// <summary>
    /// _step을 지운다(뒤의 작업은 앞으로 당겨져 stepList의 순서가 다시 정렬된다). stepList에 없으면 false.
    /// 화면의 줄이 만들어진 뒤 stepList가 바뀌어 인덱스가 어긋나도 엉뚱한 작업을 지우지 않도록, 인덱스 대신 작업 자체로 찾는다.
    /// </summary>
    public bool RemoveStep(SimulationStepModel _step)
    {
        return RemoveStepAt(stepList.IndexOf(_step));
    }

    /// <summary>작업을 모두 지운다.</summary>
    public void ClearSteps()
    {
        stepList.Clear();
    }

    /// <summary>실행용으로 작업을 순서대로 담은 Queue 복사본을 만든다. 원본 stepList는 바뀌지 않는다.</summary>
    public Queue<SimulationStepModel> ToQueue()
    {
        return new Queue<SimulationStepModel>(stepList);
    }
}
