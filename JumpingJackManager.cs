using UnityEngine;
using UnityEngine.UI;

public class JumpingJackManager : MonoBehaviour
{
    [Header("External Managers")]
    public BodyFeedbackManager feedbackManager;
    
    [Header("UI")]
    public Text repText;      
    public Text feedbackText;   

    [Header("Bones")]
    public Transform Head;
    public Transform LeftHand;
    public Transform RightHand;
    public Transform LeftFoot;
    public Transform RightFoot;

    [Header("Leg Settings (Traffic Light)")]
    public float legDistRed = 0.3f;     // Feet closer than 0.3m = RED (Standing)
    public float legDistGreen = 0.7f;   // Feet wider than 0.7m = GREEN (Jump Out)
                                        // Between 0.3 and 0.7 = YELLOW

    [Header("Arm Settings (Traffic Light)")]
    public float armYRed = -0.2f;       // Hands below shoulders = RED
    public float armYGreen = 0.1f;      // Hands above Head = GREEN
                                        // (Relative to Head position)

    // Internal State
    private int repCount = 0;
    private bool reachedStarPose = false; // Have we hit the "Out" pose yet?

    public void StartExercise()
    {
        this.enabled = true;
        repCount = 0;
        reachedStarPose = false;
        UpdateUI();
    }

    public void StopExercise()
    {
        this.enabled = false;
        ResetColors();
    }

    void Update()
    {
        if (Head == null || LeftFoot == null) return;

        // --- 1. CALCULATE METRICS ---
        float feetDistance = Mathf.Abs(LeftFoot.position.x - RightFoot.position.x);
        
        // Check if hands are above head (Positive) or below (Negative)
        float leftHandY = LeftHand.position.y - Head.position.y;
        float rightHandY = RightHand.position.y - Head.position.y;
        float avgHandY = (leftHandY + rightHandY) / 2f;

        // --- 2. TRAFFIC LIGHT COLORS (Continuous Updates) ---

        // LEGS LOGIC
        BodyFeedbackManager.FeedbackState legState;
        if (feetDistance < legDistRed)
        {
            legState = BodyFeedbackManager.FeedbackState.Wrong; // Red (Closed)
        }
        else if (feetDistance < legDistGreen)
        {
            legState = BodyFeedbackManager.FeedbackState.Warning; // Yellow (Spreading)
        }
        else
        {
            legState = BodyFeedbackManager.FeedbackState.Correct; // Green (Wide)
        }
        SetColor("Legs", legState);

        // ARMS LOGIC
        BodyFeedbackManager.FeedbackState armState;
        if (avgHandY < armYRed)
        {
            armState = BodyFeedbackManager.FeedbackState.Wrong; // Red (Down)
        }
        else if (avgHandY < armYGreen)
        {
            armState = BodyFeedbackManager.FeedbackState.Warning; // Yellow (Going Up)
        }
        else
        {
            armState = BodyFeedbackManager.FeedbackState.Correct; // Green (Up high)
        }
        SetColor("Left/RightHand", armState);
        SetColor("Body", armState); // Color body same as arms

        // --- 3. REP COUNTING LOGIC ---
        
        bool isStarPose = (legState == BodyFeedbackManager.FeedbackState.Correct && 
                           armState == BodyFeedbackManager.FeedbackState.Correct);

        bool isSoldierPose = (legState == BodyFeedbackManager.FeedbackState.Wrong && 
                              armState == BodyFeedbackManager.FeedbackState.Wrong);

        // Logic: Must hit Star Pose (Green), then return to Soldier Pose (Red) to count 1 rep
        if (!reachedStarPose)
        {
            // WAITING TO JUMP OUT
            if (isStarPose)
            {
                reachedStarPose = true;
                if (feedbackText) 
                {
                    feedbackText.text = "Now Return!";
                    feedbackText.color = Color.cyan;
                }
            }
            else
            {
                if (feedbackText) 
                {
                    feedbackText.text = "Jump Out!";
                    feedbackText.color = Color.white;
                }
            }
        }
        else
        {
            // WAITING TO RETURN
            if (isSoldierPose)
            {
                repCount++;
                reachedStarPose = false; // Reset for next rep
                UpdateUI();
                if (feedbackText) feedbackText.text = "Good!";
            }
        }
    }

    // --- HELPERS ---

    void SetColor(string zone, BodyFeedbackManager.FeedbackState state)
    {
        if (feedbackManager != null) feedbackManager.UpdateZoneColor(zone, state);
    }

    void ResetColors()
    {
        if (feedbackManager != null)
        {
            feedbackManager.UpdateZoneColor("Body", BodyFeedbackManager.FeedbackState.Neutral);
            feedbackManager.UpdateZoneColor("Legs", BodyFeedbackManager.FeedbackState.Neutral);
            feedbackManager.UpdateZoneColor("Left/RightHand", BodyFeedbackManager.FeedbackState.Neutral);
        }
    }

    void UpdateUI()
    {
        if (repText != null) repText.text = "Jacks: " + repCount;
    }
}