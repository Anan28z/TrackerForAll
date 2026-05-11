using UnityEngine;

public class SquatManager : MonoBehaviour
{
    [Header("External Managers")]
    public BodyFeedbackManager feedbackManager; 
    private BodyMirrorSystem mirrorSystem;

    [Header("Right Leg Bones")]
    public Transform R_Hip;       
    public Transform R_Knee;      
    public Transform R_Ankle;     

    [Header("Left Leg Bones")]
    public Transform L_Hip;       
    public Transform L_Knee;      
    public Transform L_Ankle;     

    [Header("Traffic Light Settings")]
    public float redThreshold = 145f;    // Above 145 (Standing) = RED (Resting)
    public float squatTarget = 100f;     // Below 100 (Deep) = GREEN (Perfect Rep)
                                         // Between 100 and 145 = YELLOW (Partial Rep)

    [Header("Rep Counting & Smoothing")]
    public float smoothSpeed = 8f;       
    
    [Header("Timeout Settings")]
    public float maxRestTime = 10f; 

    private bool isExerciseActive = false; 

    // Internal State Tracker
    private int repsRemaining = 10; 
    private int greenCount = 0;
    private int yellowCount = 0;
    private int redCount = 0;

    private bool isSquatting = false; 
    private BodyFeedbackManager.FeedbackState currentRepBestState = BodyFeedbackManager.FeedbackState.Wrong;
    
    private float idleTimer = 0f;
    private BodyFeedbackManager.FeedbackState currentState = BodyFeedbackManager.FeedbackState.Neutral;
    private float currentSmoothAngle = 180f; 

    void Start()
    {
        mirrorSystem = GetComponent<BodyMirrorSystem>();
    }

    public void StartExercise()
    {
        isExerciseActive = true;
        this.enabled = true;
        
        repsRemaining = 10;
        greenCount = 0;
        yellowCount = 0;
        redCount = 0;
        
        isSquatting = false;
        currentRepBestState = BodyFeedbackManager.FeedbackState.Wrong;
        idleTimer = 0f;
        currentState = BodyFeedbackManager.FeedbackState.Neutral; 
        currentSmoothAngle = 180f; 
        
        if(mirrorSystem != null) mirrorSystem.isSquatActive = true;
        
        UpdateUI();

        if (feedbackManager != null)
        {
            feedbackManager.UpdateZoneColor("Legs", BodyFeedbackManager.FeedbackState.Neutral);
            feedbackManager.UpdateZoneColor("Shoes", BodyFeedbackManager.FeedbackState.Neutral);
        }
    }

    public void StopExercise()
    {
        isExerciseActive = false;
        this.enabled = false;
        
        if(mirrorSystem != null) mirrorSystem.isSquatActive = false;
        
        if (feedbackManager != null)
        {
            feedbackManager.UpdateZoneColor("Legs", BodyFeedbackManager.FeedbackState.Neutral);
            feedbackManager.UpdateZoneColor("Shoes", BodyFeedbackManager.FeedbackState.Neutral);
        }
    }

    public void ForceStopAssessment()
    {
        if (isExerciseActive)
        {
            FinishAssessment();
        }
    }

    void Update()
    {
        if (!isExerciseActive) return;

        // --- 1. Timeout Logic (Timer only runs if standing in Red zone and not mid-rep) ---
        if (currentState == BodyFeedbackManager.FeedbackState.Wrong && !isSquatting)
        {
            idleTimer += Time.deltaTime; 

            if (idleTimer >= maxRestTime)
            {
                redCount++;
                repsRemaining--;
                idleTimer = 0f; 
                UpdateUI();

                if (repsRemaining <= 0)
                {
                    FinishAssessment();
                    return; 
                }
            }
        }

        // --- 2. Calculate Leg Angles ---
        if (R_Hip == null || L_Hip == null) return;

        float rAngle = GetLegAngle(R_Hip, R_Knee, R_Ankle);
        float lAngle = GetLegAngle(L_Hip, L_Knee, L_Ankle);
        float rawAverage = (rAngle + lAngle) / 2f;

        currentSmoothAngle = Mathf.Lerp(currentSmoothAngle, rawAverage, Time.deltaTime * smoothSpeed);

        // --- 3. Determine Current State ---
        BodyFeedbackManager.FeedbackState state;
        
        if (currentSmoothAngle > redThreshold) state = BodyFeedbackManager.FeedbackState.Wrong; 
        else if (currentSmoothAngle <= squatTarget) state = BodyFeedbackManager.FeedbackState.Correct; 
        else state = BodyFeedbackManager.FeedbackState.Warning; 

        currentState = state;
        SetColor(state);

        // --- 4. Rep Evaluation Logic ---
        // A. If they start squatting down (leave the Red standing zone)
        if (state != BodyFeedbackManager.FeedbackState.Wrong)
        {
            idleTimer = 0f; 

            if (!isSquatting)
            {
                isSquatting = true; 
                currentRepBestState = BodyFeedbackManager.FeedbackState.Warning; 
            }

            if (state == BodyFeedbackManager.FeedbackState.Correct)
            {
                currentRepBestState = BodyFeedbackManager.FeedbackState.Correct;
            }
        }
        // B. If they return to standing (Red zone) after a squat
        else if (isSquatting && state == BodyFeedbackManager.FeedbackState.Wrong)
        {
            if (currentRepBestState == BodyFeedbackManager.FeedbackState.Correct) greenCount++;
            else if (currentRepBestState == BodyFeedbackManager.FeedbackState.Warning) yellowCount++;
            else redCount++; 

            repsRemaining--; 
            isSquatting = false; 
            currentRepBestState = BodyFeedbackManager.FeedbackState.Wrong;
            idleTimer = 0f; 

            UpdateUI();

            if (repsRemaining <= 0)
            {
                FinishAssessment();
            }
        }
    }

    private void FinishAssessment()
    {
        int completedReps = greenCount + yellowCount + redCount;

        if (DatabaseManager.instance != null)
        {
            DatabaseManager.instance.SaveExerciseData("Squat", completedReps, greenCount, yellowCount, redCount);
        }

        if (AppManager.instance != null)
        {
            AppManager.instance.TriggerOutcomeDelay("Squat", greenCount, yellowCount, redCount, 10);
        }

        StopExercise();
    }

    void SetColor(BodyFeedbackManager.FeedbackState state)
    {
        if (feedbackManager != null)
        {
            feedbackManager.UpdateZoneColor("Legs", state);
            feedbackManager.UpdateZoneColor("Shoes", state); 
        }
    }

    float GetLegAngle(Transform hip, Transform knee, Transform ankle)
    {
        Vector3 thigh = hip.position - knee.position;
        Vector3 shin = ankle.position - knee.position;
        return Vector3.Angle(thigh, shin);
    }

    void UpdateUI()
    {
        if (AppManager.instance != null)
        {
            AppManager.instance.UpdateGlobalRepText(repsRemaining.ToString());
        }
    }
}