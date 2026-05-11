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
using TrackerPro.Unity.HandTracking;

namespace TrackerPro
{
    //atatch this script with solution gameobject and drag&drop annotatable screen in screen field
    public class HandLandamrkPoints : TrackingEventHandler
    {
        public RectTransform screen;
        public override void OnPoseUpdate(Solution solution)
        {
            HandTrackingSolution handTrackingSolution = (HandTrackingSolution)solution;
            if (handTrackingSolution == null || handTrackingSolution.handLandmarksAnnotationController == null || handTrackingSolution.handLandmarksAnnotationController.currentHandLandmarkLists == null) return;
            //normalize landmark point

            Debug.Log("hand count:"+handTrackingSolution.handLandmarksAnnotationController.currentHandLandmarkLists.Count);
            Debug.Log("hand lanmark count:" + handTrackingSolution.handLandmarksAnnotationController.currentHandLandmarkLists[0].Landmark.Count);

            //for (int i = 0; i < handTrackingSolution.handLandmarksAnnotationController.currentHandLandmarkLists[0].Landmark.Count; i++)
            //{
            //    Debug.Log(handTrackingSolution.handLandmarksAnnotationController.currentHandLandmarkLists[0].Landmark[i]);
            //}

            //screen landmark point
            //usefull for place somethine on face
            //for (int i = 0; i < handTrackingSolution.handLandmarksAnnotationController.currentHandLandmarkLists[0].Landmark.Count; i++)
            //{
            //    Debug.Log(GetScreenPoint(handTrackingSolution.handLandmarksAnnotationController.currentHandLandmarkLists[0].Landmark[i]));
            //}

        }
        public Vector3 GetScreenPoint(NormalizedLandmark landmark)
        {
            var relative_position = screen.rect.GetPoint(landmark, 0, false);
            var local_position = screen.TransformPoint(relative_position);
            return local_position;
        }
    }


}

