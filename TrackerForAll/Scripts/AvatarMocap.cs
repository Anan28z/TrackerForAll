using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrackerPro.Unity;
using TrackerPro.Unity.CoordinateSystem;
using TrackerPro.Unity.PoseTracking;

namespace TrackerPro
{
    public class AvatarMocap : TrackingEventHandler
    {
        [Header("Visibility Control")]
        [Tooltip("Drag ALL the mesh parts you want to hide here (Eyes, Body, Hair, Outfit, etc).")]
        public List<GameObject> modelsToHide; 

        [Header("Mocap Settings")]
        private Transform _bonePrefab;
        public float scaleFactor = 1;
        [Range(0, 1)]
        public float visibilityThreshold = .6f;
        [Range(0, 1)]
        public float smoothness = .5f;
        [SerializeField]
        private bool inPlace = true;
        [SerializeField]
        private bool scaleMatch = false;

        // --- NEW: SQUAT FIX (FOOT ANCHOR METHOD) ---
        [Header("Squat Fix (Foot Anchor)")]
        public bool useSquatFix = true;
        private Transform leftFoot;
        private Transform rightFoot;
        private float baselineFloorY = 0f;
        private bool hasBaseline = false;
        // ---------------------------------
        
        [HideInInspector]
        public List<LandmarkData> bones = new List<LandmarkData>();
        [HideInInspector]
        public CorePoseTrackingSolution corePoseTrackingSolution;
        public RectTransform skeletonParent;
        private List<BoneMapper> boneMappers = new List<BoneMapper>();
        [HideInInspector]
        private int _numberOfBones = 33;
        private Animator avatar;
        [HideInInspector]
        public LandmarkData virtualHip;
        [HideInInspector]
        public LandmarkData virtualNeck;
        private float _roiWidth = 1;
        private Transform head;
        private Transform hip;
        private Vector3 hipPosition;
        private RectTransform screen;

        private void Start()
        {
            virtualNeck = new LandmarkData { transform = new GameObject().transform, visibility = 0 };
            virtualNeck.transform.parent = skeletonParent;
            virtualNeck.transform.localEulerAngles = Vector3.zero;
            virtualNeck.transform.name = "NECK";
            virtualHip = new LandmarkData { transform = new GameObject().transform, visibility = 0 };
            virtualHip.transform.parent = skeletonParent;
            virtualHip.transform.name = "HIP";
            virtualHip.transform.localEulerAngles = Vector3.zero;

            _bonePrefab = new GameObject().transform;
            screen = skeletonParent.GetComponent<RectTransform>();
            avatar = GetComponent<Animator>();
            for (int i = 0; i < _numberOfBones; i++)
            {
                Transform trans = Instantiate(_bonePrefab, skeletonParent);
                LandmarkData landmarkData = new LandmarkData { transform = trans, visibility = 0 };
                trans.name = ((Body)i).ToString();
                bones.Add(landmarkData);
            }
            Destroy(_bonePrefab.gameObject);
            MapBones();
        }

        public void MapBones()
        {
            head = avatar.GetBoneTransform(HumanBodyBones.Head);
            hip = avatar.GetBoneTransform(HumanBodyBones.Hips);
            
            // --- Grab the feet so we can glue them to the floor later ---
            leftFoot = avatar.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = avatar.GetBoneTransform(HumanBodyBones.RightFoot);

            AddBoneMapper(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, Body.RIGHT_SHOULDER, Body.RIGHT_ELBOW);
            AddBoneMapper(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, Body.RIGHT_ELBOW, Body.RIGHT_WRIST);
            AddBoneMapper(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, Body.RIGHT_HIP, Body.RIGHT_KNEE);
            AddBoneMapper(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, Body.RIGHT_KNEE, Body.RIGHT_ANKLE);

            AddBoneMapper(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, Body.LEFT_SHOULDER, Body.LEFT_ELBOW);
            AddBoneMapper(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, Body.LEFT_ELBOW, Body.LEFT_WRIST);
            AddBoneMapper(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, Body.LEFT_HIP, Body.LEFT_KNEE);
            AddBoneMapper(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, Body.LEFT_KNEE, Body.LEFT_ANKLE);

            BoneMapper spineMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.Spine), avatar.GetBoneTransform(HumanBodyBones.Neck));
            spineMapper.refParent = virtualHip;
            spineMapper.refChild = virtualNeck;
            boneMappers.Add(spineMapper);

            BoneMapper neckMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.Neck), avatar.GetBoneTransform(HumanBodyBones.Head));
            neckMapper.refParent = virtualNeck;
            neckMapper.refChild = bones[(int)Body.NOSE];
            boneMappers.Add(neckMapper);

            BoneMapper lFootMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.LeftFoot), avatar.GetBoneTransform(HumanBodyBones.Neck));
            lFootMapper.refParent = bones[(int)Body.LEFT_ANKLE];
            lFootMapper.refChild = bones[(int)Body.LEFT_INDEX];
            boneMappers.Add(lFootMapper);

            BoneMapper rFootMapper = new BoneMapper(avatar.GetBoneTransform(HumanBodyBones.RightFoot), avatar.GetBoneTransform(HumanBodyBones.Neck));
            rFootMapper.refParent = bones[(int)Body.RIGHT_ANKLE];
            rFootMapper.refChild = bones[(int)Body.RIGHT_INDEX];
            boneMappers.Add(rFootMapper);
        }

        private void AddBoneMapper(HumanBodyBones parent, HumanBodyBones child, Body refParentBone, Body refChildBone)
        {
            BoneMapper mapper = new BoneMapper(avatar.GetBoneTransform(parent), avatar.GetBoneTransform(child));
            mapper.refParent = bones[(int)refParentBone];
            mapper.refChild = bones[(int)refChildBone];
            boneMappers.Add(mapper);
        }

        public override void OnPoseUpdate(Solution solution)
        {
            corePoseTrackingSolution = (CorePoseTrackingSolution)solution;
            if (!isTracking()) return;

            for (int i = 0; i < _numberOfBones; i++)
            {
                var position = new Vector3(-corePoseTrackingSolution.poseWorldLandmarks.Landmark[i].X, -corePoseTrackingSolution.poseWorldLandmarks.Landmark[i].Y, -corePoseTrackingSolution.poseWorldLandmarks.Landmark[i].Z);
                if (i == (int)Body.NOSE) position.z *= .3f;
                else position.z *= .9f;
                bones[i].visibility = corePoseTrackingSolution.poseWorldLandmarks.Landmark[i].Visibility;
                bones[i].transform.position = position;
            }

            if (corePoseTrackingSolution.roiFromLandmarks != null) _roiWidth = corePoseTrackingSolution.roiFromLandmarks.Width;
            if (_roiWidth == 0) _roiWidth = 1;

            virtualHip.transform.position = (bones[(int)Body.LEFT_HIP].transform.position + bones[(int)Body.RIGHT_HIP].transform.position) / 2;
            virtualNeck.transform.position = (bones[(int)Body.LEFT_SHOULDER].transform.position + bones[(int)Body.RIGHT_SHOULDER].transform.position) / 2;
            virtualNeck.transform.position = new Vector3(virtualNeck.transform.position.x, virtualNeck.transform.position.y, virtualNeck.transform.position.z * .0f);
            virtualHip.visibility = bones[(int)Body.LEFT_HIP].visibility;
            virtualNeck.visibility = bones[(int)Body.LEFT_SHOULDER].visibility;
        }

        private void LateUpdate()
        {
            bool trackingActive = isTracking();

            if (modelsToHide != null)
            {
                foreach (GameObject part in modelsToHide)
                {
                    if (part != null && part.activeSelf != trackingActive)
                    {
                        part.SetActive(trackingActive);
                    }
                }
            }

            if (!trackingActive)
            {
                hasBaseline = false; // Reset the squat baseline if tracking is lost
                return;
            }

            foreach (BoneMapper boneMapper in boneMappers)
            {
                boneMapper.parent.localRotation = boneMapper.initialRotation;
            }

            FixHipRotation();
            SolvePoseTracking();
            FixHeadRotation();

            if (!inPlace) Move();
            if (scaleMatch) FixScale();

            // --- NEW SQUAT FIX: FOOT ANCHOR ---
            // This runs at the very end to forcefully push the hips down if the feet lift
            if (useSquatFix && leftFoot != null && rightFoot != null)
            {
                if (!hasBaseline)
                {
                    // Record the floor height when tracking starts (using the lowest foot)
                    baselineFloorY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
                    hasBaseline = true;
                }
                else
                {
                    // Calculate how far the lowest foot has floated off the floor
                    float currentLowestFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
                    float floatAmount = currentLowestFootY - baselineFloorY;

                    // If the feet are floating up, push the hip down by that exact amount
                    if (floatAmount > 0.01f)
                    {
                        Vector3 fixedHipPos = hip.position;
                        fixedHipPos.y -= floatAmount; 
                        hip.position = fixedHipPos;
                    }
                }
            }
        }

        private void SolvePoseTracking()
        {
            foreach (BoneMapper boneMapper in boneMappers)
            {
                if (boneMapper.refChild.visibility < visibilityThreshold) continue;
                if (boneMapper.refParent.transform == null || boneMapper.refChild.transform == null || boneMapper.parent == null || boneMapper.child == null) continue;

                Vector3 vOld = boneMapper.parent.InverseTransformDirection((boneMapper.child.position - boneMapper.parent.position).normalized);
                Vector3 vNew = boneMapper.parent.InverseTransformDirection((boneMapper.refChild.transform.position - boneMapper.refParent.transform.position).normalized);

                if (vOld.sqrMagnitude < 0.0001f || vNew.sqrMagnitude < 0.0001f) continue;

                Quaternion rotOld = boneMapper.previousRotation;
                Quaternion rotNew = boneMapper.parent.localRotation * Quaternion.FromToRotation(vOld, vNew).normalized;
                boneMapper.parent.localRotation = Quaternion.Slerp(rotOld, rotNew, smoothness);
                boneMapper.previousRotation = boneMapper.parent.localRotation;
            }
        }

        private void FixHipRotation()
        {
            float zDiff = bones[(int)Body.RIGHT_SHOULDER].transform.position.z - bones[(int)Body.LEFT_SHOULDER].transform.position.z;
            zDiff = Mathf.Clamp(zDiff, -20, 20);
            zDiff = Mathf.Abs(zDiff) > .02f ? zDiff - .02f * (zDiff / Mathf.Abs(zDiff)) : 0;
            float yAngle = zDiff * 300;
            hip.rotation = Quaternion.Lerp(hip.rotation, Quaternion.Euler(hip.eulerAngles.x, -yAngle, hip.eulerAngles.z), smoothness);
        }

        private void FixHeadRotation()
        {
            float zDiff = bones[(int)Body.RIGHT_EYE].transform.position.z - bones[(int)Body.LEFT_EYE].transform.position.z;
            float yAngle = zDiff * 3000;
            yAngle = Mathf.Clamp(yAngle, -90, 90);
            yAngle = Mathf.Abs(yAngle) < 5 ? 0 : yAngle;
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(head.localEulerAngles.x, -yAngle, head.localEulerAngles.z), smoothness);
        }

        private void Move()
        {
            var leftHipPosition = screen.rect.GetPoint(corePoseTrackingSolution.poseLandmarks.Landmark[(int)Body.LEFT_HIP], 0, false);
            var rightHipPosition = screen.rect.GetPoint(corePoseTrackingSolution.poseLandmarks.Landmark[(int)Body.RIGHT_HIP], 0, false);
            hipPosition = (screen.TransformPoint(leftHipPosition) + screen.TransformPoint(rightHipPosition)) / 2;
            hipPosition.z = hip.position.z;
            hip.position = hipPosition;
        }

        private void FixScale()
        {
            Vector3 newScale = Vector3.one * (_roiWidth * scaleFactor);
            hip.localScale = Vector3.Lerp(hip.localScale, newScale, Time.deltaTime * 10);
        }

        private bool isTracking()
        {
            return (corePoseTrackingSolution != null && corePoseTrackingSolution.poseWorldLandmarks != null);
        }
    }

    public class BoneMapper
    {
        public Transform parent, child, hint;
        public LandmarkData refParent, refChild;
        public Quaternion initialRotation, previousRotation;
        public BoneMapper(Transform parent, Transform child) { this.parent = parent; this.child = child; initialRotation = parent.localRotation; previousRotation = initialRotation; }
    }
    public class LandmarkData { public Transform transform; public float visibility; }
    public enum BoneType { LEFT_SHOULDER, RIGHT_SHOULDER, LEFT_HAND, RIGHT_HAND, LEFT_LEG, LEFT_ANKLE, RIGHT_ANKLE, RIGHT_LEG, MIDDLE }
}