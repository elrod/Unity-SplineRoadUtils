using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineRoadUtils
{
    [ExecuteInEditMode]
    public class SplineSampler : MonoBehaviour
    {
        [SerializeField] 
        private SplineContainer _SplineContainer;

        public int NumSplines => _SplineContainer.Splines.Count;

        private float3 _position;
        private float3 _forward;
        private float3 _upVector;

        private Vector3 _roadRight;
        private Vector3 _roadLeft;
        
        public void SampleSplineWidth(int splineIndex, float t, float desiredWidth, out Vector3 rightPt, out Vector3 leftPt)
        {
            _SplineContainer.Evaluate(splineIndex, t, out _position, out _forward, out _upVector);
            float3 right = Vector3.Cross(_forward, _upVector).normalized;
            rightPt = _position + (right * desiredWidth);
            leftPt = _position + (-right * desiredWidth);
        }
    }
}

