#define Graph_And_Chart_PRO
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChartAndGraph
{

    partial class EditorMenu
    {
        private static void InstanciateWorldSpace(string path)
        {
            GameObject obj = Resources.Load<GameObject>(path);
            //  GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject newObj = (GameObject)GameObject.Instantiate(obj);
            newObj.name = newObj.name.Replace("(Clone)", "");
            Undo.RegisterCreatedObjectUndo(newObj, "Create Object");
        }

        [MenuItem(MenuItemBase + "Radar/3D", priority = MenuItemPriority)]
        public static void AddRadarChartWorldSpace()
        {
            InstanciateWorldSpace("MenuPrefabs/3DRadar");
        }

        [MenuItem(MenuItemBase + "Bar/3D/Simple", priority = MenuItemPriority)]
        public static void AddBarChartSimple3D()
        {
            
            InstanciateWorldSpace("MenuPrefabs/Bar3DSimple");
        }

        [MenuItem(MenuItemBase + "Bar/3D/Multiple Groups", priority = MenuItemPriority)]
        public static void AddBarChartMultiple3D()
        {
            InstanciateWorldSpace("MenuPrefabs/Bar3DMultiple");
        }

        [MenuItem(MenuItemBase + "Torus/3D", priority = MenuItemPriority)]
        public static void AddTorusChart3D()
        {
            InstanciateWorldSpace("MenuPrefabs/Torus3D");
        }

        [MenuItem(MenuItemBase + "Pie/3D", priority = MenuItemPriority)]
        public static void AddPieChart3D()
        {
            InstanciateWorldSpace("MenuPrefabs/Pie3D");
        }

        [MenuItem(MenuItemBase + "Graph/3D", priority = MenuItemPriority)]
        public static void AddGraph3D()
        {
            InstanciateWorldSpace("MenuPrefabs/3DGraph");
        }

    }
}
