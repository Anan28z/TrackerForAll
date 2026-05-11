using UnityEngine;
using System.Collections.Generic;

public class BodyFeedbackManager : MonoBehaviour
{
    // Define our states
    public enum FeedbackState { Correct, Warning, Wrong, Neutral }

    [System.Serializable]
    public class BodyZone
    {
        public string zoneName;       // e.g., "LeftHand"
        public Renderer renderer;     // The mesh renderer
        public int materialIndex = 0; // The specific material slot to swap
        
        // Internal storage for the original material (hidden in Inspector)
        [HideInInspector] 
        public Material originalMaterial; 
    }

    [Header("Highlight Materials Only")]
    public Material matCorrect; // Green
    public Material matWarning; // Yellow
    public Material matWrong;   // Red
    // Removed matNeutral - we now use the original material automatically

    [Header("Body Parts List")]
    public List<BodyZone> bodyZones = new List<BodyZone>();

    private void Start()
    {
        // 1. Auto-save the original materials when the game starts
        foreach (var zone in bodyZones)
        {
            if (zone.renderer != null && zone.renderer.materials.Length > zone.materialIndex)
            {
                // Cache the material currently on the character so we can revert to it later
                zone.originalMaterial = zone.renderer.materials[zone.materialIndex];
            }
        }
    }

    public void UpdateZoneColor(string zoneName, FeedbackState state)
    {
        BodyZone zone = bodyZones.Find(z => z.zoneName == zoneName);
        if (zone == null || zone.renderer == null) return;

        // Default to the cached original material
        Material targetMat = zone.originalMaterial;

        // Select override material based on state
        switch (state)
        {
            case FeedbackState.Correct: targetMat = matCorrect; break;
            case FeedbackState.Warning: targetMat = matWarning; break;
            case FeedbackState.Wrong:   targetMat = matWrong; break;
            case FeedbackState.Neutral: targetMat = zone.originalMaterial; break;
        }

        // Apply the swap
        Material[] mats = zone.renderer.materials;
        if (zone.materialIndex < mats.Length)
        {
            // Only apply if the material is actually different (optimization)
            if (mats[zone.materialIndex] != targetMat)
            {
                mats[zone.materialIndex] = targetMat;
                zone.renderer.materials = mats;
            }
        }
    }
}