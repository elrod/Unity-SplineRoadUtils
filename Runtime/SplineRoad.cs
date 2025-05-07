using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace SplineRoadUtils
{
    [Serializable]
    public struct JunctionInfo
    {
        public int splineIndex;
        public int knotIndex;
        public Spline spline;
        public BezierKnot knot;

        public JunctionInfo(int splineIndex, int knotIndex, Spline spline, BezierKnot knot)
        {
            this.splineIndex = splineIndex;
            this.knotIndex = knotIndex;
            this.spline = spline;
            this.knot = knot;
        }
    }

    [Serializable]
    public struct JunctionEdge
    {
        public Vector3 Left;
        public Vector3 Right;

        public Vector3 Center => (Left + Right) / 2f;

        public JunctionEdge(Vector3 p1, Vector3 p2)
        {
            Left = p1;
            Right = p2;
        }
    }

    [Serializable]
    public class Intersection
    {
        public List<JunctionInfo> Junctions;
        public List<float> Curves;

        public void AddCurve(float value)
        {
            if (Curves == null)
            {
                Curves = new List<float>();
            }
            Curves.Add(value);
        }
        

        public void AddJunction(int splineIndex, int knotIndex, Spline spline, BezierKnot knot)
        {
            if (Junctions == null)
            {
                Junctions = new List<JunctionInfo>();
            }
            
            Junctions.Add(new JunctionInfo(splineIndex, knotIndex, spline, knot));
        }

        internal IEnumerable<JunctionInfo> GetJunctions()
        {
            return Junctions;
        }
    }

    [Serializable]
    public class SplineRoadOverrideSettings
    {
        public int SplineIndex;
        public float RoadWidth;
        public int RoadResolution;

        public SplineRoadOverrideSettings(int index, float width, int resolution)
        {
            SplineIndex = index;
            RoadWidth = width;
            RoadResolution = resolution;
        }
    }
    
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    public class SplineRoad : MonoBehaviour
    {
        [SerializeField] 
        private SplineSampler _SplineSampler;
        [SerializeField]
        [Min(0)]
        private int _RoadResolution;
        [SerializeField] 
        [Min(0f)]
        private float _RoadWidth;

        [SerializeField] 
        [Min(1)] 
        private int _CurveSteps;

        [SerializeField]
        private List<SplineRoadOverrideSettings> _SplineOverridesVector;
        
        private Dictionary<int, SplineRoadOverrideSettings> _splineOverrides;
        
        private List<Vector3> _rightPoints;
        private List<Vector3> _leftPoints;

        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;

        private int _currentResolution;
        private float _currentRoadWidth;
        private int _currentCurveSteps;

        [SerializeField]
        [HideInInspector]
        // ReSharper disable once InconsistentNaming
        private List<Intersection> _intersections;

        public void AddIntersections(Intersection intersection)
        {
            if (_intersections == null)
            {
                _intersections = new List<Intersection>();
            }
            _intersections.Add(intersection);
            RebuildMesh();
        }

        public bool GetIntersection(JunctionInfo junctionInfo, out Intersection intersection)
        {
            foreach (var intersect in _intersections)
            {
                foreach (var junction in intersect.GetJunctions())
                {
                    if (junction.knotIndex == junctionInfo.knotIndex &&
                        junction.splineIndex == junctionInfo.splineIndex)
                    {
                        intersection = intersect;
                        return true;
                    }
                }
            }
            intersection = null;
            return false;
        }

        public void RemoveIntersection(Intersection intersection)
        {
            _intersections.Remove(intersection);
            RebuildMesh();
        }

        public void ClearAllIntersections()
        {
            if (_intersections != null)
            {
                _intersections.Clear();
                RebuildMesh();
            }
        }

        public SplineRoadOverrideSettings GetSettings(int splineIndex)
        {
            if (splineIndex < 0 || splineIndex > _SplineSampler.NumSplines)
            {
                Debug.LogWarning("Invalid spline index");
                return null;
            }
            if (_splineOverrides == null)
            {
                _splineOverrides = new Dictionary<int, SplineRoadOverrideSettings>();
                foreach (var serializedOverride in _SplineOverridesVector)
                {
                    _splineOverrides.Add(serializedOverride.SplineIndex, serializedOverride);
                }
            }

            if (!_splineOverrides.ContainsKey(splineIndex))
            {
                var newOverride = new SplineRoadOverrideSettings(splineIndex, _RoadWidth, _RoadResolution);
                _splineOverrides.Add(splineIndex, newOverride);
                if (_SplineOverridesVector == null)
                {
                    _SplineOverridesVector = new List<SplineRoadOverrideSettings>();
                    _splineOverrides.Add(splineIndex, newOverride);
                }

                bool found = false;
                foreach (var serializedOverride in _SplineOverridesVector)
                {
                    if (serializedOverride.SplineIndex == splineIndex)
                    {
                        _splineOverrides.Add(serializedOverride.SplineIndex, serializedOverride);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    _SplineOverridesVector.Add(newOverride);
                }
            }

            return _splineOverrides[splineIndex];
        }

        public void OverrideRoadResolution(int splineIndex, int splineResolution)
        {
            var splineOverride = GetSettings(splineIndex);
            splineOverride.RoadResolution = splineResolution;
            RebuildMesh();
        }
        
        public void OverrideRoadWidth(int splineIndex, float splineWidth)
        {
            var splineOverride = GetSettings(splineIndex);
            splineOverride.RoadWidth = splineWidth;
            RebuildMesh();
        }
        
        public void RebuildMesh()
        {
            if(!_meshFilter) return;
            
            Debug.Log($"[{nameof(SplineRoad)}] rebuilding mesh...");
            GetVertices();
            BuildMesh();
            _currentCurveSteps = _CurveSteps;
            _currentRoadWidth = _RoadWidth;
            if (!_meshCollider)
            {
                _meshCollider = GetComponent<MeshCollider>();
                if (_meshCollider)
                {
                    _meshCollider.sharedMesh = _meshFilter.sharedMesh;
                }
            }
            else
            {
                _meshCollider.sharedMesh = _meshFilter.sharedMesh;
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(gameObject);
            EditorUtility.SetDirty(this);
#endif
        }

        private void OnEnable()
        {
            _meshFilter = GetComponent<MeshFilter>();
            Spline.Changed += OnSplineChanged;
            _currentRoadWidth = _RoadWidth;
            _currentCurveSteps = _CurveSteps;
            _currentResolution = _RoadResolution;
            GetVertices();
        }
        
        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }
        
        private void OnValidate()
        {
            if (_currentCurveSteps != _CurveSteps || _currentResolution != _RoadResolution || !Mathf.Approximately(_currentRoadWidth, _RoadWidth))
            {
                RebuildMesh();
            }
        }

        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            RebuildMesh();
        }


        private void GetVertices()
        {
            _rightPoints = new List<Vector3>();
            _leftPoints = new List<Vector3>();
            
            Vector3 rightPt;
            Vector3 leftPt;
            
            for (int j = 0; j < _SplineSampler.NumSplines; j++)
            {
                var splineOverride = GetSettings(j);
                float step = 1f / splineOverride.RoadResolution;
                
                for (int i = 0; i < splineOverride.RoadResolution; i++)
                {
                    float t = step * i;
                    _SplineSampler.SampleSplineWidth(j, t, splineOverride.RoadWidth, out rightPt, out leftPt);
                    _rightPoints.Add(rightPt);
                    _leftPoints.Add(leftPt);
                }
                
                _SplineSampler.SampleSplineWidth(j, 1f, splineOverride.RoadWidth, out rightPt, out leftPt);
                _rightPoints.Add(rightPt);
                _leftPoints.Add(leftPt);
            }

        }

        private void BuildMesh()
        {
            Mesh m = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<int> trisB = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            
            GenerateRoadSubMesh(verts, tris, uvs);
            GenerateIntersectionsSubMesh(verts, trisB, uvs);

            m.name = "Road";
            m.subMeshCount = 2;
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.SetTriangles(trisB, 1);
            m.SetUVs(0, uvs);
            _meshFilter.sharedMesh = m;
        }

        private void GenerateRoadSubMesh(List<Vector3> verts, List<int> tris, List<Vector2> uvs)
        {
            int offset = 0;
            int splineOffset = 0;
            for (int currentSplineIndex = 0; currentSplineIndex < _SplineSampler.NumSplines; currentSplineIndex++)
            {
                var splineOverride = GetSettings(currentSplineIndex);
                
                float uvOffset = 0f;
                for (int currentSplinePoint = 1; currentSplinePoint < splineOverride.RoadResolution + 1; currentSplinePoint++)
                {
                    int vertOffset = splineOffset + currentSplinePoint + currentSplineIndex;
                    Vector3 p1 = _rightPoints[vertOffset - 1];
                    Vector3 p2 = _leftPoints[vertOffset - 1];
                    Vector3 p3 = _rightPoints[vertOffset];
                    Vector3 p4 = _leftPoints[vertOffset];

                    offset = 4 * splineOffset;
                    offset += 4 * (currentSplinePoint - 1);
                    
                    int t1 = offset + 0;
                    int t2 = offset + 2;
                    int t3 = offset + 3;

                    int t4 = offset + 3;
                    int t5 = offset + 1;
                    int t6 = offset + 0;
                
                    verts.AddRange(new List<Vector3> {p1, p2, p3, p4});
                    tris.AddRange(new List<int>{t1, t2, t3, t4, t5, t6});

                    float distance = Vector3.Distance(p1, p3) / 4f;
                    float uvDistance = uvOffset + distance;
                    uvs.AddRange(new List<Vector2>{
                        new(0, uvOffset),
                        new(1, uvOffset),
                        new(0, uvDistance), 
                        new(1, uvDistance)
                        
                    });
                    uvOffset += distance;
                }
                splineOffset += splineOverride.RoadResolution;
            }
        }

        private void GenerateIntersectionsSubMesh(List<Vector3> verts, List<int> tris, List<Vector2> uvs)
        {
            if (_intersections != null)
            {
                for (int i = 0; i < _intersections.Count; i++)
                {
                    Intersection intersection = _intersections[i];
                    int count = 0;
                    // List<Vector3> points = new List<Vector3>();
                    List<JunctionEdge> junctionEdges = new List<JunctionEdge>();
                    Vector3 center = new Vector3();

                    foreach (JunctionInfo junction in intersection.GetJunctions())
                    {
                        int splineIndex = junction.splineIndex;
                        var splineOverride = GetSettings(splineIndex);
                        float t = junction.knotIndex == 0 ? 0f : 1f;
                        _SplineSampler.SampleSplineWidth(splineIndex, t, splineOverride.RoadWidth, out Vector3 p1, out Vector3 p2);
                        if (junction.knotIndex == 0)
                        {
                            junctionEdges.Add(new JunctionEdge(p2, p1));
                        }
                        else
                        {
                            junctionEdges.Add(new JunctionEdge(p1,p2));
                        }
                        center += p1;
                        center += p2;
                        count++;
                    }

                    center /= junctionEdges.Count * 2f;
                    
                    junctionEdges.Sort((x, y) => SortPoints(center, x.Center, y.Center));

                    List<Vector3> curvePoints = new List<Vector3>();
                    Vector3 mid;
                    Vector3 c = Vector3.zero;
                    Vector3 b = Vector3.zero;
                    Vector3 a = Vector3.zero;
                    BezierCurve curve;

                    for (int j = 1; j <= junctionEdges.Count; j++)
                    {
                        a = junctionEdges[j - 1].Left;
                        curvePoints.Add(a);
                        b = (j < junctionEdges.Count) ? junctionEdges[j].Right : junctionEdges[0].Right;
                        mid = Vector3.Lerp(a, b, 0.5f);
                        Vector3 dir = center - mid;
                        mid -= dir;
                        c = Vector3.Lerp(mid, center, intersection.Curves[j - 1]);

                        curve = new BezierCurve(a, c, b);

                        var stepSize = 1f / _CurveSteps;
                        
                        for (float t = 0f; t < 1f; t += stepSize)
                        {
                            Vector3 pos = CurveUtility.EvaluatePosition(curve, t);
                            curvePoints.Add(pos);
                        }
                        
                        curvePoints.Add(b);
                    }
                    
                    int pointsOffset = verts.Count;
                    for (int j = 1; j <= curvePoints.Count; j++)
                    {
                        verts.Add(center);
                        verts.Add(curvePoints[j - 1]);
                        if (j == curvePoints.Count)
                        {
                            verts.Add(curvePoints[0]);
                        }
                        else
                        {
                            verts.Add(curvePoints[j]);
                        }
                        
                        tris.Add(pointsOffset + ((j-1) * 3) + 0);
                        tris.Add(pointsOffset + ((j-1) * 3) + 1);
                        tris.Add(pointsOffset + ((j-1) * 3) + 2);
                        
                        uvs.Add(new Vector2(center.z, center.x));
                        uvs.Add(new Vector2(a.z, a.x));
                        uvs.Add(new Vector2(b.z, b.x));
                    }
                }
            }
        }

        private int SortPoints(Vector3 center, Vector3 x, Vector3 y)
        {

            Vector3 xDir = x - center;
            Vector3 yDir = y - center;
            
            float angleA = Vector3.SignedAngle(center.normalized, xDir.normalized, Vector3.up);
            float angleB = Vector3.SignedAngle(center.normalized, yDir.normalized, Vector3.up);
            
            if (angleA > angleB)
            {
                return 1;
            }
            
            if (angleA < angleB)
            {
                return -1;
            }
            return 0;

        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Handles.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < _rightPoints.Count; i++)
            {
                Handles.SphereHandleCap(0, _rightPoints[i], Quaternion.identity, 1f, EventType.Repaint);
                Handles.SphereHandleCap(1, _leftPoints[i], Quaternion.identity, 1f, EventType.Repaint);
                Handles.DrawLine(_leftPoints[i], _rightPoints[i], 1f);
            }

            int counter = 2;
            foreach (var intersection in _intersections)
            {
                foreach (var junction in intersection.GetJunctions())
                {
                    int splineIndex = junction.splineIndex;
                    var splineOverride = GetSettings(splineIndex);
                    float t = junction.knotIndex == 0 ? 0f : 1f;
                    _SplineSampler.SampleSplineWidth(splineIndex, t, splineOverride.RoadWidth, out Vector3 p1, out Vector3 p2);
                    JunctionEdge edge;
                    if (junction.knotIndex == 0)
                    {
                        edge = new JunctionEdge(p2, p1);
                    }
                    else
                    {
                        edge = new JunctionEdge(p2, p1);
                    }
                    Handles.color = Color.blue;
                    Handles.SphereHandleCap(counter++, edge.Left, Quaternion.identity, 2f, EventType.Repaint);
                    Handles.color = Color.red;
                    Handles.SphereHandleCap(counter++, edge.Right, Quaternion.identity, 2f, EventType.Repaint);
                    Handles.color = Color.yellow;
                    Handles.SphereHandleCap(counter++, edge.Center, Quaternion.identity, 2f, EventType.Repaint);
                }
            }
            
        }
#endif
    }
}