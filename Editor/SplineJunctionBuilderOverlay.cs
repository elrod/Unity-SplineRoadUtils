using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UIElements;

namespace SplineRoadUtils.Editor
{
    [Overlay(typeof(SceneView), "Junction Builder", true)]
    public class SplineJunctionBuilderOverlay : Overlay, ITransientOverlay
    {
        private Label _selectionInfoLabel;
        private Button _buildJunctionButton;
        private Button _removeIntersectionButton;
        private Button _clearAllJunctionsButton;
        private VisualElement _sliderArea;

        private SplineRoad _targetSplineRoad;

        private Intersection _currentTargetIntersection;

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
            var root = new VisualElement() { name = "Junction Builder" };
            _selectionInfoLabel = new Label();
            _sliderArea = new VisualElement();
            _buildJunctionButton = new Button
            {
                text = "Build Junction"
            };
            _buildJunctionButton.clicked += OnBuildJunction;
            _buildJunctionButton.SetEnabled(false);
            _removeIntersectionButton = new Button()
            {
                text = "Remove Intersection"
            };
            _removeIntersectionButton.clicked += OnRemoveIntersection;
            _clearAllJunctionsButton = new Button()
            {
                text = "Clear All Junctions",
                style = { backgroundColor = new Color(.7f,0f,0f)}
            };
            _clearAllJunctionsButton.clicked += OnClearAllJunctions;
            root.Add(_selectionInfoLabel);
            root.Add(_sliderArea);
            root.Add(_removeIntersectionButton);
            root.Add(_buildJunctionButton);
            root.Add(_clearAllJunctionsButton);
            return root;
        }

        private void OnSelectionChanged()
        {
            var selectedObject = Selection.activeGameObject;
            if (!selectedObject) return;
            _targetSplineRoad = selectedObject.GetComponent<SplineRoad>();
        }

        private void OnClearAllJunctions()
        {
            if (_targetSplineRoad)
            {
                _targetSplineRoad.ClearAllIntersections();
            }
        }
        
        private void OnRemoveIntersection()
        {
            if (_currentTargetIntersection != null)
            {
                _targetSplineRoad.RemoveIntersection(_currentTargetIntersection);
                _currentTargetIntersection = null;
                UpdateOverlay();
            }
        }
     
        public override void OnWillBeDestroyed()
        {
            SplineSelection.changed -= OnSplineSelectionChanged;
            Selection.selectionChanged -= OnSelectionChanged;
        }
    
        private void OnSplineSelectionChanged()
        {
            UpdateOverlay();
        }
    
        private void ClearSelectionInfo()
        {
            _selectionInfoLabel.text = "";
        }
    
        private void UpdateOverlay()
        {
            List<SelectedSplineElementInfo> infos = SplineToolUtility.GetSelection();

            bool junctionSelected = false;
            _currentTargetIntersection = null;
            
            foreach (SelectedSplineElementInfo element in infos)
            {
                SplineContainer container = (SplineContainer)element.target;
                Spline spline = container.Splines[element.targetIndex];
                if (_currentTargetIntersection == null)
                {
                    var junction =  new JunctionInfo(element.targetIndex, element.knotIndex, spline,
                        spline.Knots.ToArray()[element.knotIndex]);
                    if (_targetSplineRoad.GetIntersection(junction, out _currentTargetIntersection))
                    {
                        junctionSelected = true;
                    }
                }
            }

            if (junctionSelected)
            {
                ShowIntersection(_currentTargetIntersection);
            }
            else
            {
                ShowSelection();
            }
        }

        private void ShowIntersection(Intersection intersection)
        {
            ClearSelectionInfo();
            _buildJunctionButton.SetEnabled(false);
            _removeIntersectionButton.SetEnabled(true);
            _selectionInfoLabel.text = "Selected Intersection";
            
            _sliderArea.Clear();
            for (int i = 0; i < intersection.Curves.Count; i++)
            {
                int value = i;
                Slider slider = new Slider($"Curve {i}", 0, 1, SliderDirection.Horizontal);
                slider.labelElement.style.minWidth = 60;
                slider.labelElement.style.maxWidth = 80;
                slider.value = intersection.Curves[i];
                slider.RegisterValueChangedCallback((x) =>
                {
                    intersection.Curves[value] = x.newValue;
                    _targetSplineRoad.RebuildMesh();
                });
                _sliderArea.Add(slider);
            }
        }

        private void ShowSelection()
        {
            ClearSelectionInfo();
            _sliderArea.Clear();
            _removeIntersectionButton.SetEnabled(false);
            List<SelectedSplineElementInfo> infos = SplineToolUtility.GetSelection();
            foreach (SelectedSplineElementInfo element in infos)
            {
                _selectionInfoLabel.text += $"Spline {element.targetIndex}, Knot {element.knotIndex}\n";
            }

            _buildJunctionButton.SetEnabled(infos.Count > 1);
            
        }
        
        private void OnBuildJunction()
        {
            List<SelectedSplineElementInfo> selection = SplineToolUtility.GetSelection();
            Intersection intersection = new Intersection();

            foreach (SelectedSplineElementInfo item in selection)
            {
                SplineContainer container = (SplineContainer)item.target;
                Spline spline = container.Splines[item.targetIndex];
                intersection.AddJunction(item.targetIndex, item.knotIndex, spline, spline.Knots.ToArray()[item.knotIndex]);
                intersection.AddCurve(0.5f);
            }

            if (_targetSplineRoad)
            {
                _targetSplineRoad.AddIntersections(intersection);
                UpdateOverlay();
            }
        }
    }
}

