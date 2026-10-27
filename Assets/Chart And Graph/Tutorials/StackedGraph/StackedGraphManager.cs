#define Graph_And_Chart_PRO
using ChartAndGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StackedGraphManager : MonoBehaviour
{
    public int RealtimeDownSampleCount = 10;
    public int DownSampleToPoints = 100;

    public GraphChart Chart;

    class CategoryEntry
    {
        public List<double> mYValues = new List<double>();
        public List<DoubleVector2> mVectors = new List<DoubleVector2>();
        public LargeDataFeed mFeed = null;
        public bool mEnabled = true;
    }

    Dictionary<string, CategoryEntry> mData = new Dictionary<string, CategoryEntry>();
    List<double> mXValues = new List<double>();
    List<double> mAccumilated = new List<double>();
    void Start()
    {
    }
    public DoubleVector2 GetPointValue(string category,int inGraphIndex)
    {
        if (mData.ContainsKey(category) == false)
            throw new ArgumentException("Category does not exist");

        var entry = mData[category];
        int index = entry.mFeed.GetIndex(inGraphIndex);
        double y = entry.mYValues[index];
        double x = mXValues[index];
        return new DoubleVector2(x,y);
    }
    public void ToggleCategoryEnabled(string category)
    {
        VerifyCategories();
        if (mData.ContainsKey(category) == false)
            throw new ArgumentException("no such category");
        var entry = mData[category];
        entry.mEnabled = !entry.mEnabled;
        Chart.DataSource.SetCategoryEnabled(category, entry.mEnabled);
        ApplyData();
    }
    public void SetCategoryEnabled(string category,bool isEnabled)
    {
        VerifyCategories();
        if (mData.ContainsKey(category) == false)
            throw new ArgumentException("no such category");
        var entry = mData[category];
        entry.mEnabled = isEnabled;
        Chart.DataSource.SetCategoryEnabled(category, isEnabled);
        ApplyData();
    }
    void VerifyNewCategory(string name)
    {
        CategoryEntry data = null;
        if (mData.ContainsKey(name))
            data = mData[name];
        if(data == null)
        {
            data = new CategoryEntry();
            data.mYValues = new List<double>(mXValues.Count);
            data.mVectors = new List<DoubleVector2>(mXValues.Count);
            data.mFeed = gameObject.AddComponent<LargeDataFeed>();
            data.mFeed.LoadExample = false;
            data.mFeed.AlternativeGraph = Chart;
            data.mFeed.Category = name;
            mData[name] = data;
        }
        data.mVectors.Clear();
        data.mYValues.Clear();
        for (int i = 0; i < mXValues.Count; i++)
        {
            data.mVectors.Add(new DoubleVector2(mXValues[i], 0.0));
            data.mYValues.Add(0.0);
        }
        data.mFeed.DownSampleToPoints = DownSampleToPoints;
        data.mFeed.RealtimeDownSampleCount = RealtimeDownSampleCount;
    }
    private void OnValidate()
    {
        foreach(var feed in gameObject.GetComponents<LargeDataFeed>())
        {
            feed.DownSampleToPoints = DownSampleToPoints;
            feed.RealtimeDownSampleCount = RealtimeDownSampleCount;
        }
    }
    void VerifyRemoveCategory(string name)
    {
        if (mData.ContainsKey(name) == false)
            return;
        var data = mData[name];
        if(data != null)
        {
            data.mVectors = null;
            UnityEngine.Object.Destroy(data.mFeed);
        }
        mData.Remove(name);
    }
    
    void VerifyCategories()
    {
        var names = Chart.DataSource.CategoryNames;
        foreach(string name in names)
        {
            if(mData.ContainsKey(name) == false)
            {
                VerifyNewCategory(name);
            }
        }
        foreach(string name in mData.Keys)
        {
            if (names.Contains(name) == false)
                VerifyRemoveCategory(name);
        }
    }
    void ClearEntries()
    {
        mXValues.Clear();
        foreach (string name in Chart.DataSource.CategoryNames)
        {
            var entry = mData[name];
            entry.mVectors.Clear();
            entry.mFeed.SetData(new List<DoubleVector2>());
        }
    }
    public void InitialData(double[] x,double[,] y)
    {
        VerifyCategories();
        ClearEntries();
        if (x.Length != y.GetLength(0))
            throw new ArgumentException("x and y size should match");
        mXValues.Clear();
        mXValues.AddRange(x);
        mAccumilated.Clear();
        mAccumilated.AddRange(Enumerable.Repeat(0.0, mXValues.Count));
        int categoryIndex = Chart.DataSource.CategoryNames.Count()-1;

        foreach (string name in Chart.DataSource.CategoryNames.Reverse())
        {
            var entry = mData[name];


            if (entry.mEnabled)
            {
                for (int i = 0; i < x.Length; i++)
                    mAccumilated[i] += y[i, categoryIndex];
            }
            entry.mYValues.Clear();
            entry.mVectors.Clear();
            for (int i = 0; i < mXValues.Count; i++)
            {
                entry.mYValues.Add(y[i, categoryIndex]);
                entry.mVectors.Add(new DoubleVector2(mXValues[i], mAccumilated[i]));
            }

            entry.mFeed.SetData(entry.mVectors);
            categoryIndex--;
        }
    }
    void ApplyData()
    {
        mAccumilated.Clear();
        mAccumilated.AddRange(Enumerable.Repeat(0.0, mXValues.Count));
        int categoryIndex = Chart.DataSource.CategoryNames.Count() - 1;

        foreach (string name in Chart.DataSource.CategoryNames.Reverse())
        {
            var entry = mData[name];
            entry.mVectors.Clear();
            if (entry.mEnabled)
            {
                for (int i = 0; i < mXValues.Count; i++)
                    mAccumilated[i] += entry.mYValues[i];
            }
            entry.mVectors.Clear();
            for (int i = 0; i < mXValues.Count; i++)
                entry.mVectors.Add(new DoubleVector2(mXValues[i], mAccumilated[i]));
            entry.mFeed.SetData(entry.mVectors);
            categoryIndex--;
        }
    }
    public void AddPointRealtime(double x,double[] y,double slideTime = 0.0)
    {
        VerifyCategories();
        mXValues.Add(x);
        int categoryIndex = Chart.DataSource.CategoryNames.Count()-1;
        double accumilated = 0.0;
        foreach (string name in Chart.DataSource.CategoryNames.Reverse())
        {
            var entry = mData[name];
            double yValue = 0.0;
            if (categoryIndex < y.Length)
                yValue = y[categoryIndex];
            if (entry.mEnabled)
                accumilated += yValue;
            //entry.mVectors.Add(new DoubleVector2(x,yValue)); // this happens in AppendRealtimeWithDownSampling
            entry.mYValues.Add(yValue);
            entry.mFeed.AppendPointRealtime(x, accumilated, slideTime);
            categoryIndex--;
        }

    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
