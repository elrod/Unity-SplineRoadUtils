using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace SplineRoadUtils.Editor
{
    [Overlay(typeof(SceneView), "Conform Terrain To Road", true)]
    public class ConformTerrainToRoadOverlay : Overlay, ITransientOverlay
    {
        public bool visible {
            get
            {
                ParseSelection();
                return _selectedTerrain != null && _splineRoad != null;
            }
        }
        
        private Label _selectionInfoLabel;
        private Button _conformTerrainButton;
        private FloatField _yOffsetField;
        private FloatField _edgeFallOffSizeField;

        private Terrain _selectedTerrain;
        private SplineRoad _splineRoad;
        
        public override VisualElement CreatePanelContent()
        {
            Selection.selectionChanged += ParseSelection;
            var root = new VisualElement() { name = "Conform terrain to road" };
            
            _selectionInfoLabel = new Label("Click to conform terrain heights to selected road...");
            _conformTerrainButton = new Button
            {
                text = "Conform terrain to selected"
            };
            _conformTerrainButton.clicked += OnConformButtonClicked;
            _yOffsetField = new FloatField("Y Offset");
            _edgeFallOffSizeField = new FloatField("Edge Fall-Off Size");
            _yOffsetField.value = 0f;
            _edgeFallOffSizeField.value = 1f;
            
            root.Add(_selectionInfoLabel);
            root.Add(_yOffsetField);
            root.Add(_edgeFallOffSizeField);
            root.Add(_conformTerrainButton);

            return root;
        }

        private void OnConformButtonClicked()
        {
            if (_selectedTerrain == null || _splineRoad == null)
            {
                return;
            }

            MeshFilter mf = _splineRoad.GetComponent<MeshFilter>();
            if (mf == null)
            {
                return;
            }
            Mesh mesh = mf.sharedMesh;

            TerrainData terrainData = _selectedTerrain.terrainData;
            Vector3 terrainPos = _selectedTerrain.transform.position;
            Vector3 terrainSize = terrainData.size;

            int res = terrainData.heightmapResolution;
            float[,] heights = terrainData.GetHeights(0, 0, res, res);

            float falloffRadius = _edgeFallOffSizeField.value;

            Undo.RegisterCompleteObjectUndo(terrainData, "Conform terrain to road");
        
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Transform t = mf.transform;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = t.TransformPoint(vertices[triangles[i]]);
                Vector3 v1 = t.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = t.TransformPoint(vertices[triangles[i + 2]]);

                // Bounding box 2D (XZ)
                float minX = Mathf.Min(v0.x, v1.x, v2.x);
                float maxX = Mathf.Max(v0.x, v1.x, v2.x);
                float minZ = Mathf.Min(v0.z, v1.z, v2.z);
                float maxZ = Mathf.Max(v0.z, v1.z, v2.z);

                int minI = Mathf.Max(0, Mathf.FloorToInt((minX - terrainPos.x - falloffRadius) / terrainSize.x * (res - 1)));
                int maxI = Mathf.Min(res - 1, Mathf.CeilToInt((maxX - terrainPos.x + falloffRadius) / terrainSize.x * (res - 1)));
                int minJ = Mathf.Max(0, Mathf.FloorToInt((minZ - terrainPos.z - falloffRadius) / terrainSize.z * (res - 1)));
                int maxJ = Mathf.Min(res - 1, Mathf.CeilToInt((maxZ - terrainPos.z + falloffRadius) / terrainSize.z * (res - 1)));

                for (int j = minJ; j <= maxJ; j++)
                {
                    for (int iX = minI; iX <= maxI; iX++)
                    {
                        float worldX = terrainPos.x + (iX / (float)(res - 1)) * terrainSize.x;
                        float worldZ = terrainPos.z + (j / (float)(res - 1)) * terrainSize.z;
                        Vector2 p = new Vector2(worldX, worldZ);

                        if (PointInTriangleXZ(p, v0, v1, v2, out Vector3 bary))
                        {
                            float interpolatedY = v0.y * bary.x + v1.y * bary.y + v2.y * bary.z;
                            float normalizedY = (interpolatedY - terrainPos.y) / terrainSize.y;

                            // Calcolo del falloff
                            float distance = DistanceToTriangleEdge(p, v0, v1, v2);
                            float falloff = Mathf.Clamp01(distance / falloffRadius);
                            float weight = 1f - falloff;

                            heights[j, iX] = Mathf.Lerp(heights[j, iX], normalizedY + _yOffsetField.value, weight);
                        }
                    }
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }

        public override void OnWillBeDestroyed()
        {
            Selection.selectionChanged -= ParseSelection;
        }
        
        private bool PointInTriangleXZ(Vector2 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
        {
            Vector2 a2 = new Vector2(a.x, a.z);
            Vector2 b2 = new Vector2(b.x, b.z);
            Vector2 c2 = new Vector2(c.x, c.z);

            Vector2 v0 = b2 - a2;
            Vector2 v1 = c2 - a2;
            Vector2 v2 = p - a2;

            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);

            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 1e-6f)
            {
                bary = Vector3.zero;
                return false;
            }

            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1.0f - v - w;
            bary = new Vector3(u, v, w);

            return (u >= 0) && (v >= 0) && (w >= 0);
        }
        
        private float DistanceToTriangleEdge(Vector2 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector2 a2 = new Vector2(a.x, a.z);
            Vector2 b2 = new Vector2(b.x, b.z);
            Vector2 c2 = new Vector2(c.x, c.z);

            float d0 = DistancePointToSegment(p, a2, b2);
            float d1 = DistancePointToSegment(p, b2, c2);
            float d2 = DistancePointToSegment(p, c2, a2);

            return Mathf.Min(d0, Mathf.Min(d1, d2));
        }

        private float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab);
            t = Mathf.Clamp01(t);
            Vector2 projection = a + t * ab;
            return Vector2.Distance(p, projection);
        }
        
        private void ParseSelection()
        {
            var selectedGameObjects = Selection.gameObjects;
            _selectedTerrain = null;
            foreach (var gameObject in selectedGameObjects)
            {
                if (_selectedTerrain == null)
                {
                    _selectedTerrain = gameObject.GetComponent<Terrain>();
                    if (_selectedTerrain != null)
                    {
                        continue;
                    }
                }

                var splineRoad = gameObject.GetComponent<SplineRoad>();
                if (splineRoad != null)
                {
                    _splineRoad = splineRoad;
                }
            }
        }
    }
}