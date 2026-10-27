#define Graph_And_Chart_PRO
using UnityEngine;
using System.Collections;
using ChartAndGraph;

public class BarChartFeed2 : MonoBehaviour {
	void Start () {
        BarChart barChart = GetComponent<BarChart>();
        if (barChart != null)
        {
            barChart.DataSource.SlideValue("Player 1", "Value 1", Random.value * 20,10f);
            barChart.DataSource.SlideValue("Player 2", "Value 1", Random.value * 20, 40f);
            barChart.DataSource.SlideValue("Player 3", "Value 1", Random.value * 20, 10f);
            barChart.DataSource.SlideValue("Player 1", "Value 2", Random.value * 20, 40f);
            barChart.DataSource.SlideValue("Player 2", "Value 2", Random.value * 20, 10f);
            barChart.DataSource.SlideValue("Player 3", "Value 2", Random.value * 20, 40f);
            barChart.DataSource.SlideValue("Player 1", "Value 3", Random.value * 20, 10f);
            barChart.DataSource.SlideValue("Player 2", "Value 3", Random.value * 20, 40f);
            barChart.DataSource.SlideValue("Player 3", "Value 3", Random.value * 20, 40f);
        }
    }
    private void Update()
    {
    }
}
