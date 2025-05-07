
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Splines
{
    public struct SelectedSplineElementInfo
    {
        public Object target;
        public int targetIndex;
        public int knotIndex;

        public SelectedSplineElementInfo(Object Object, int index, int knot)
        {
            target = Object;
            targetIndex = index;
            knotIndex = knot;
        }
    }
    
    public static class SplineToolUtility
    {
        public static bool HasSelection()
        {
            return SplineSelection.HasActiveSplineSelection();
        }

        public static List<SelectedSplineElementInfo> GetSelection()
        {
            List<SelectableSplineElement> elements = SplineSelection.selection;
            List<SelectedSplineElementInfo> infos =  new List<SelectedSplineElementInfo>();
            
            // Converting data from internal struct to a public struct
            foreach (var element in elements)
            {
                infos.Add((new SelectedSplineElementInfo(element.target, element.targetIndex, element.knotIndex)));
            }

            return infos;
        }
    }
}
