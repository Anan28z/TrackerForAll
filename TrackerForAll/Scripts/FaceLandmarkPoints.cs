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
    public class FaceLandmarkPoints : TrackingEventHandler
    {
        public RectTransform screen;
        public override void OnPoseUpdate(Solution solution)
        {
            FaceMeshSolution faceMeshSolution = (FaceMeshSolution)solution;
            if (faceMeshSolution == null || faceMeshSolution.multiFaceLandmarksAnnotationController == null || faceMeshSolution.multiFaceLandmarksAnnotationController.currentTarget == null) return;
            //normalize landmark point
            //there 478 face landmark point
            // to know about index check the link https://drive.google.com/file/d/1VowfQNj0mVteJeyBlMnlUNT5F_hX2bWf/view?usp=sharing
            //Debug.Log("face count:"+faceMeshSolution.multiFaceLandmarksAnnotationController.currentTarget.Count);
            //Debug.Log("face lanmark count:" + faceMeshSolution.multiFaceLandmarksAnnotationController.currentTarget[0].Landmark.Count);

            //for (int i = 0; i < faceMeshSolution.multiFaceLandmarksAnnotationController.currentTarget[0].Landmark.Count; i++)
            //{
            //    Debug.Log(faceMeshSolution.multiFaceLandmarksAnnotationController.currentTarget[0].Landmark[i]);
            //}

            //screen landmark point
            //usefull for place somethine on face
            //for (int i = 0; i < faceMeshSolution.multiFaceLandmarksAnnotationController.currentTarget[0].Landmark.Count; i++)
            //{
            //    Debug.Log(GetScreenPoint(faceMeshSolution.multiFaceLandmarksAnnotationController.currentTarget[0].Landmark[i]));
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

