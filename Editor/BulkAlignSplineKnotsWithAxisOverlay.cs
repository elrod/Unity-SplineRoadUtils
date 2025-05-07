using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.Splines;
using UnityEngine.UIElements;

namespace SplineRoadUtils.Editor
{
    [Overlay(typeof(SceneView), "Bulk align spline knots with axis", true)]
    public class BulkAlignSplineKnotsWithAxisOverlay : Overlay, ITransientOverlay
    {
        private Button _bulkRotateButton;
        private SplineContainer _selectedSplineContainer;
        private DropdownField _axisDropdown;

        private readonly List<string> _axes = new() { "X", "Y", "Z" };
        private int _selectedAxisIndex = 1; 
        
        public bool visible {
            get
            {
                ParseSelection();
                return _selectedSplineContainer != null;
            }
        }
        
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement() { name = "Bulk align spline knots with axis" };
            
            _axisDropdown = new DropdownField("Fix Axis", _axes, _selectedAxisIndex);
            _axisDropdown.RegisterValueChangedCallback(evt =>
            {
                _selectedAxisIndex = _axes.IndexOf(evt.newValue);
            });
            
            _bulkRotateButton = new Button
            {
                text = "Bulk Align Spline Knots"
            };
            _bulkRotateButton.clicked += OnBulkRotateButtonClicked;
            
            root.Add(_axisDropdown);
            root.Add(_bulkRotateButton);
            return root;
        }

        private void OnBulkRotateButtonClicked()
        {
            if (_selectedSplineContainer == null) return;

            Undo.RecordObject(_selectedSplineContainer, "Bulk align spline knots");

            foreach (var spline in _selectedSplineContainer.Splines)
            {
                for (int i = 0; i < spline.Count; i++)
                {
                    var knot = spline[i];

                    float3 forward = math.mul(knot.Rotation, new float3(0, 0, 1));

                    switch (_selectedAxisIndex)
                    {
                        case 0: forward.x = 0; break;
                        case 1: forward.y = 0; break;
                        case 2: forward.z = 0; break;
                    }

                    if (math.lengthsq(forward) < 0.0001f)
                        continue;

                    forward = math.normalizesafe(forward);
                    
                    float3 up = _selectedAxisIndex switch
                    {
                        0 => new float3(1, 0, 0),
                        1 => new float3(0, 1, 0),
                        2 => new float3(0, 0, 1),
                        _ => new float3(0, 1, 0)
                    };

                    quaternion newRotation = quaternion.LookRotationSafe(forward, up);

                    knot.Rotation = newRotation;
                    spline.SetKnot(i, knot);
                }
            }

            EditorUtility.SetDirty(_selectedSplineContainer);
        }
        
        public override void OnWillBeDestroyed()
        {
            _bulkRotateButton.clicked -= OnBulkRotateButtonClicked;
        }

        private void ParseSelection()
        {
            var selectedGameObject = Selection.activeGameObject;
            if (selectedGameObject)
            {
                _selectedSplineContainer = selectedGameObject.GetComponent<SplineContainer>();
            }
            else
            {
                _selectedSplineContainer = null;
            }
            
        }
    }
}