#define Graph_And_Chart_PRO
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChartAndGraph
{
    public class ChartGradientController : MonoBehaviour
    {
        public Gradient gradient; 
        void Start()
        {

        }

        void Update()
        {
            if (gradient == null)
                return;
            var info = GetComponent<BarInfo>();
            var chart = GetComponentInParent<BarChart>();
            var matControl = GetComponent<ChartMaterialController>();
            if (info == null || chart == null || matControl == null)
                return;
            var min = chart.DataSource.GetMinValue();
            var max = chart.DataSource.GetMaxValue();
            double factor = (info.Value - min) / (max - min);
            if (double.IsFinite(factor) == false)
                return;

            var color = gradient.Evaluate((float)factor);
            matControl.SetBaseColorOverride(color);
        }
    }
}