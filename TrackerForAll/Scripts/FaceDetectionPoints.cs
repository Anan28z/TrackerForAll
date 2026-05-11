using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using TrackerPro.Unity;
using TrackerPro.Unity.CoordinateSystem;
using TrackerPro.Unity.PoseTracking;
using UnityEngine;
using TrackerPro.Unity.FaceMesh;
using UnityEngine.Device;
using TrackerPro.Unity.FaceDetection;

namespace TrackerPro
{
    //atatch this script with solution gameobject and drag&drop annotatable screen in screen field
    public class FaceDetectionPoints : TrackingEventHandler
    {
        public RectTransform screen;
        public override void OnPoseUpdate(Solution solution)
        {
            FaceDetectionSolution faceDetectionSolution = (FaceDetectionSolution)solution;
            if (faceDetectionSolution == null || faceDetectionSolution.faceDetectionsAnnotationController == null || faceDetectionSolution.faceDetectionsAnnotationController._currentTarget == null) return;
            //faceDetectionSolution.faceDetectionsAnnotationController._currentTarget.Count
        }
        public Vector3 GetScreenPoint(NormalizedLandmark landmark)
        {
            var relative_position = screen.rect.GetPoint(landmark, 0, false);
            var local_position = screen.TransformPoint(relative_position);
            return local_position;
        }
    }


}

