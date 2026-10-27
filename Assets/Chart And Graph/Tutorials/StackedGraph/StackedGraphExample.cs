#define Graph_And_Chart_PRO
using ChartAndGraph;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class StackedGraphExample : MonoBehaviour
{
    public int NumCategories = 3;
    public Text infoText;
    void Start()
    {
        StartCoroutine(AddPoints());
    }

    IEnumerator AddPoints()
    {
        int x = 100;
        var manager = GetComponent<StackedGraphManager>();
        manager.Chart.PointHovered.AddListener(GraphHoverd);
        manager.Chart.NonHovered.AddListener(NonHovered);
        double[] xArr = Enumerable.Range(0, x).Select(num=>(double)num).ToArray();
        double[,] yArr = new double[xArr.Length, NumCategories];
        for(int i=0; i< yArr.GetLength(0); i++)
            for(int j=0; j<yArr.GetLength(1); j++)
            {
                yArr[i,j] = Random.value * 3;
            }
        manager.InitialData(xArr, yArr);
        double[] newPointYArr = new double[NumCategories];
        while (true)
        {
            yield return new WaitForSeconds(5.0f);
            x++;
            for (int i = 0; i < newPointYArr.Length; i++)
                newPointYArr[i] = Random.value * 3;
            manager.AddPointRealtime(x, newPointYArr, 1f);

        }
    }

    public void Toogle(string name)
    {
        var manager = GetComponent<StackedGraphManager>();
        if(manager != null)
        {
            manager.ToggleCategoryEnabled(name);
        }
    }
    public void GraphHoverd(GraphChartBase.GraphEventArgs args)
    {
        if (infoText == null)
            return;
        var manager = GetComponent<StackedGraphManager>();
        var point = manager.GetPointValue(args.Category,args.Index);
        infoText.text = string.Format("{0} : {1},{2:0.##}", args.Category, point.x, point.y);
    }
    public void NonHovered()
    {
        if (infoText == null)
            return;
        infoText.text = "";
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
