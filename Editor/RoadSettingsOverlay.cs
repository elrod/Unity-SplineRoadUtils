using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.UIElements;

namespace SplineRoadUtils.Editor
{
    [Overlay(typeof(SceneView), "Road Settings", true)]
    public class RoadSettingsOverlay : Overlay, ITransientOverlay
    {
        private SplineRoad _targetSplineRoad;
        private VisualElement _splineControlsContainer;
        private IntegerField _resolutionControlField;
        private FloatField _widthControlField;

        private int _currentSplineIndex = -1;
        
        public bool visible
        {
            get
            {
                var selectedObject = Selection.activeGameObject;
                if (!selectedObject) return false;
                _targetSplineRoad = selectedObject.GetComponent<SplineRoad>();
                return ToolManager.activeContextType == typeof(SplineToolContext) && _targetSplineRoad;
            }
        }
        
        public override VisualElement CreatePanelContent()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SplineSelection.changed += OnSplineSelectionChanged;
            var root = new VisualElement() { name = "Road Settings" };
            var label = new Label("Select a spline");
            _splineControlsContainer = new VisualElement();
            _resolutionControlField = new IntegerField("Road resolution");
            _widthControlField = new FloatField("Road width");

            _resolutionControlField.RegisterValueChangedCallback(OnResolutionChanged);
            _widthControlField.RegisterValueChangedCallback(OnWidthChanged);
            
            _splineControlsContainer.Add(_resolutionControlField);
            _splineControlsContainer.Add(_widthControlField);
            
            root.Add(label);
            root.Add(_splineControlsContainer);
            return root;
        }

        private void OnWidthChanged(ChangeEvent<float> evt)
        {
            if(!_targetSplineRoad) return;
            _targetSplineRoad.OverrideRoadWidth(_currentSplineIndex, evt.newValue);
        }

        private void OnResolutionChanged(ChangeEvent<int> evt)
        {
            if(!_targetSplineRoad) return;
            _targetSplineRoad.OverrideRoadResolution(_currentSplineIndex,evt.newValue);
        }

        public override void OnWillBeDestroyed()
        {
            SplineSelection.changed -= OnSplineSelectionChanged;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void UpdateOverlay()
        {
            _splineControlsContainer.visible = _targetSplineRoad != null;
            if (!_splineControlsContainer.visible)
            {
                return;
            }
            List<SelectedSplineElementInfo> infos = SplineToolUtility.GetSelection();
            if (infos.Count > 0)
            {
                _currentSplineIndex = infos[^1].targetIndex;
                var settings = _targetSplineRoad.GetSettings(_currentSplineIndex);
                _resolutionControlField.SetValueWithoutNotify(settings.RoadResolution);
                _widthControlField.SetValueWithoutNotify(settings.RoadWidth);
            }
        }
        
        private void OnSplineSelectionChanged()
        {
            UpdateOverlay();
        }
        
        private void OnSelectionChanged()
        {
            var selectedObject = Selection.activeGameObject;
            if (!selectedObject) return;
            _targetSplineRoad = selectedObject.GetComponent<SplineRoad>();
        }
    }
}