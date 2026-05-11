using UnityEngine;

public class BodyMirrorSystem : MonoBehaviour
{
    [Header("Leg Bones")]
    public Transform Hips; 
    public Transform LeftUpLeg;  
    public Transform LeftLeg;    
    public Transform RightUpLeg; 
    public Transform RightLeg;   

    [Header("Arm Bones (NEW)")]
    public Transform LeftShoulder; 
    public Transform LeftElbow;    
    public Transform RightShoulder;
    public Transform RightElbow;   

    [Header("Settings")]
    public float sideTurnThreshold = 0.1f; 
    
    public bool isSquatActive = false; 
    public bool isBalanceActive = false; 

    void LateUpdate()
    {
        // 1. If neither exercise is active, do nothing
        if (!isSquatActive && !isBalanceActive) return;

        // 2. Determine if we should mirror the LEGS
        // We mirror legs for Squats, but NOT for Balance (needs independent movement)
        bool shouldMirrorLegs = isSquatActive && !isBalanceActive;

        float depthDiff = LeftUpLeg.position.z - RightUpLeg.position.z;

        if (Mathf.Abs(depthDiff) > sideTurnThreshold)
        {
            bool isLeftVisible = depthDiff < 0; 

            if (isLeftVisible)
            {
                // Mirror Legs only if Squatting
                if (shouldMirrorLegs)
                {
                    MirrorBone(LeftUpLeg, RightUpLeg);
                    MirrorBone(LeftLeg, RightLeg);
                }

                // Always mirror Arms for Balance if turned sideways
                if (isBalanceActive)
                {
                    MirrorBone(LeftShoulder, RightShoulder);
                    MirrorBone(LeftElbow, RightElbow);
                }
            }
            else
            {
                // Mirror Legs only if Squatting
                if (shouldMirrorLegs)
                {
                    MirrorBone(RightUpLeg, LeftUpLeg);
                    MirrorBone(RightLeg, LeftLeg);
                }

                // Always mirror Arms for Balance if turned sideways
                if (isBalanceActive)
                {
                    MirrorBone(RightShoulder, LeftShoulder);
                    MirrorBone(RightElbow, LeftElbow);
                }
            }
        }
    }

    void MirrorBone(Transform master, Transform slave)
    {
        slave.localRotation = master.localRotation;
    }
}