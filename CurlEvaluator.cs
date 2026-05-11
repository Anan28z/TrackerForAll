using UnityEngine;
using TrackerPro.Unity;
using TrackerPro.Unity.PoseTracking;

public class CurlEvaluator : TrackingEventHandler
{
    public BodyFeedbackManager feedbackManager;

    [Header("Settings")]
    public float targetAngle = 85f;      
    public float warningThreshold = 110f; 
    
    [Header("Timeout Settings")]
    public float maxRestTime = 10f; 

    private bool isExerciseActive = false; 

    private int repsRemaining = 10; 
    private int greenCount = 0;
    private int yellowCount = 0;
    private int redCount = 0;

    private bool isCurling = false; 
    private BodyFeedbackManager.FeedbackState currentRepBestState = BodyFeedbackManager.FeedbackState.Wrong;
    
    private float idleTimer = 0f;
    private BodyFeedbackManager.FeedbackState currentState = BodyFeedbackManager.FeedbackState.Neutral;

    private const int LEFT_SHOULDER = 11;
    private const int LEFT_ELBOW = 13;
    private const int LEFT_WRIST = 15;

    public void StartExercise()
    {
        isExerciseActive = true;
        this.enabled = true;
        
        repsRemaining = 10;
        greenCount = 0;
        yellowCount = 0;
        redCount = 0;
        isCurling = false;
        
        currentRepBestState = BodyFeedbackManager.FeedbackState.Wrong;
        idleTimer = 0f;
        currentState = BodyFeedbackManager.FeedbackState.Neutral; 
        
        UpdateUI();

        if (feedbackManager != null)
            feedbackManager.UpdateZoneColor("Left/RightHand", BodyFeedbackManager.FeedbackState.Neutral);
    }

    public void StopExercise()
    {
        isExerciseActive = false;
        this.enabled = false;
        if (feedbackManager != null)
            feedbackManager.UpdateZoneColor("Left/RightHand", BodyFeedbackManager.FeedbackState.Neutral);
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

        // --- FIXED: If they are not actively in the middle of a rep, the clock ticks! ---
        if (!isCurling) 
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
                }
            }
        }
    }

    public override void OnPoseUpdate(Solution solution)
    {
        if (!isExerciseActive) return;

        var poseSolution = solution as CorePoseTrackingSolution;
        if (poseSolution == null || poseSolution.poseWorldLandmarks == null) return;

        var landmarks = poseSolution.poseWorldLandmarks.Landmark;
        if (landmarks.Count <= LEFT_WRIST) return;

        Vector3 shoulder = new Vector3(-landmarks[LEFT_SHOULDER].X, -landmarks[LEFT_SHOULDER].Y, -landmarks[LEFT_SHOULDER].Z);
        Vector3 elbow    = new Vector3(-landmarks[LEFT_ELBOW].X,    -landmarks[LEFT_ELBOW].Y,    -landmarks[LEFT_ELBOW].Z);
        Vector3 wrist    = new Vector3(-landmarks[LEFT_WRIST].X,    -landmarks[LEFT_WRIST].Y,    -landmarks[LEFT_WRIST].Z);

        float currentAngle = CalculateAngle(shoulder, elbow, wrist);

        BodyFeedbackManager.FeedbackState state;
        
        if (currentAngle <= targetAngle) state = BodyFeedbackManager.FeedbackState.Correct; 
        else if (currentAngle <= warningThreshold) state = BodyFeedbackManager.FeedbackState.Warning; 
        else state = BodyFeedbackManager.FeedbackState.Wrong;   

        currentState = state;

        if (feedbackManager != null)
        {
            feedbackManager.UpdateZoneColor("Left/RightHand", state);
        }

        if (state != BodyFeedbackManager.FeedbackState.Wrong)
        {
            idleTimer = 0f; // Reset the idle timer because they are making an effort!

            if (!isCurling)
            {
                isCurling = true; 
                currentRepBestState = BodyFeedbackManager.FeedbackState.Warning; 
            }

            if (state == BodyFeedbackManager.FeedbackState.Correct)
            {
                currentRepBestState = BodyFeedbackManager.FeedbackState.Correct;
            }
        }
        else if (isCurling && state == BodyFeedbackManager.FeedbackState.Wrong)
        {
            if (currentRepBestState == BodyFeedbackManager.FeedbackState.Correct) greenCount++;
            else if (currentRepBestState == BodyFeedbackManager.FeedbackState.Warning) yellowCount++;
            else redCount++; 

            repsRemaining--; 
            isCurling = false; 
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
            DatabaseManager.instance.SaveExerciseData("Curl", completedReps, greenCount, yellowCount, redCount);
        }

        if (AppManager.instance != null)
        {
            AppManager.instance.TriggerOutcomeDelay("Curl", greenCount, yellowCount, redCount, 10);
        }

        StopExercise();
    }

    float CalculateAngle(Vector3 shoulder, Vector3 elbow, Vector3 wrist)
    {
        Vector3 upperArm = shoulder - elbow;
        Vector3 lowerArm = wrist - elbow;
        return Vector3.Angle(upperArm, lowerArm);
    }

    void UpdateUI()
    {
        if (AppManager.instance != null)
        {
            AppManager.instance.UpdateGlobalRepText(repsRemaining.ToString());
        }
    }
}