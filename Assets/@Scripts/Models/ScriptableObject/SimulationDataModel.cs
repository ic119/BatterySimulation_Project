using Incheol.Models.Define;
using System;

[Serializable]
public class SimulationDataModel
{
    public string deviceName;
    public DeviceType deviceType;
    public SimulationType simulationType;
    public float duringTime;
}