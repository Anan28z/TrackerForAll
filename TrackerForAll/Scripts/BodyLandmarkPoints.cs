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

namespace TrackerPro
{
    //atatch this script with solution gameobject and drag&drop annotatable screen in screen field
    public class BodyLandmarkPoints : TrackingEventHandler
    {
        public RectTransform screen;
        public override void OnPoseUpdate(Solution solution)
        {
            CorePoseTrackingSolution poseTrackingSolution = (CorePoseTrackingSolution)solution;
            if (poseTrackingSolution == null || poseTrackingSolution.poseWorldLandmarks == null) return;
            //normalize landmark point
            //pose landmark point
            //poseTrackingSolution.poseLandmarks
            // for more details check avatar mocap script
            for (int i = 0; i < poseTrackingSolution.poseWorldLandmarks.Landmark.Count; i++)
            {
                Debug.Log(poseTrackingSolution.poseWorldLandmarks.Landmark[i]);
            }

            //screen landmark point
            //usefull for place somethine on face
            //for (int i = 0; i < poseTrackingSolution.poseLandmarks.Landmark.Count; i++)
            //{
            //    Debug.Log(GetScreenPoint(poseTrackingSolution.poseLandmarks.Landmark[i]));
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

