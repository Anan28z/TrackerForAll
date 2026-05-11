using UnityEngine;
using UnityEngine.UI;

public class ScreenToggler : MonoBehaviour
{
    [Header("Assign References")]
    public RawImage screenRawImage;   // Drag the "Screen" object here
    public Material materialToShow;   // Drag your "Background" material here

    public void ToggleMaterial()
    {
        // Check if the current material is the one we want to show
        if (screenRawImage.material == materialToShow)
        {
            // "Hide" it: Set material to null (resets to default white UI)
            screenRawImage.material = null; 
        }
        else
        {
            // "Show" it: Assign your specific Background material
            screenRawImage.material = materialToShow;
        }
    }
}