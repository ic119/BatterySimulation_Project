#define Graph_And_Chart_PRO
using ChartAndGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PriceMarker : MonoBehaviour
{
    public GraphChartBase Chart;
    public float lineThickness = 1.0f;
    public RectTransform LineObject;
    public Text PriceIndicator;
    public RectTransform PriceIndicatorImage;
    // Start is called before the first frame update
    void Start()
    {
        if (Chart != null)
        {
            Chart.OnRedraw.AddListener(Redraw);
        }
    }
    void Redraw()
    {
        if (Chart == null)
            return;
        DoubleVector3 last;
        if (Chart.DataSource.GetLastPoint("Player 1", out last))
        {
            if (PriceIndicator != null)
                PriceIndicator.text = string.Format("{0:0.00}",last.y);
            DoubleRect rect = new DoubleRect(last.x - Chart.DataSource.HorizontalViewSize, last.y - lineThickness * 0.5f, Chart.DataSource.HorizontalViewSize, lineThickness);
            DoubleRect trimRect;

            if (LineObject != null)
            {
                Vector3 res;
                if (Chart.PointToWorldSpace(out res, rect.min.x, rect.min.y))
                {
                    if (PriceIndicatorImage != null)
                    {
                        var anchored = PriceIndicatorImage.position;
                        PriceIndicatorImage.position = new Vector2(anchored.x, res.y);
                    }
                    res.y += lineThickness;
                    double x, y;
                    if (Chart.PointToClient(res, out x, out y))
                    {
                        double thick = Math.Abs(rect.min.y - y);
                        rect = new DoubleRect(last.x - Chart.DataSource.HorizontalViewSize, last.y - thick * 0.5f, Chart.DataSource.HorizontalViewSize, thick);
                    }
                }
                if (Chart.TrimRect(rect, out trimRect)) // if the rect is in display
                {
                    if (LineObject.gameObject.activeSelf == false)    // we draw it
                        LineObject.gameObject.SetActive(true);
                    Chart.RectToCanvas(LineObject, trimRect);
                    //       Area.sizeDelta = new Vector2(Area.sizeDelta.x, lineThickness);
                }
                else
                {
                    if (LineObject.gameObject.activeSelf == true) // otherwise it is set as incative
                        LineObject.gameObject.SetActive(false);
                }
            }
        }

    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
