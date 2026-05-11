using UnityEngine;
using System.Collections.Generic; // NEW: Needed for lists

public class BalanceManager : MonoBehaviour
{
    [Header("External Managers")]
    public BodyFeedbackManager feedbackManager;
    private BodyMirrorSystem mirrorSystem; 

    [Header("Bones")]
    public Transform Spine;     
    public Transform LeftFoot;
    public Transform RightFoot;

    [Header("Traffic Light Settings (Legs)")]
    public float legRedThreshold = 65f;   
    public float legGreenThreshold = 85f; 

    [Header("Settings")]
    public float smoothSpeed = 8f; 
    
    // --- 3 Tries and 10 Sec Timeout ---
    public int maxTries = 3; 
    public float maxRestTime = 10f;

    // Internal State
    private bool isExerciseActive = false;
    
    private int currentTry = 1;
    private float currentHoldTimer = 0f;
    private float sessionBestTimer = 0f;
    private float sessionTotalTimer = 0f;
    private float idleTimer = 0f;
    private float currentHeightDiff = 0f;

    // --- NEW: Tracks the exact time of each separate try ---
    private List<float> tryTimes = new List<float>();

    void Start()
    {
        mirrorSystem = GetComponent<BodyMirrorSystem>();
    }

    public void StartExercise()
    {
        isExerciseActive = true;
        this.enabled = true;
        
        currentTry = 1;
        currentHoldTimer = 0f;
        sessionBestTimer = 0f;
        sessionTotalTimer = 0f;
        idleTimer = 0f;
        currentHeightDiff = 0f;
        
        tryTimes.Clear(); // NEW: Reset the list for a fresh session
        
        if(mirrorSystem != null) mirrorSystem.isBalanceActive = true;
        
        UpdateUI();
    }

    public void StopExercise()
    {
        isExerciseActive = false;
        this.enabled = false;
        
        if(mirrorSystem != null) mirrorSystem.isBalanceActive = false;
        
        ResetColors();
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
        if (Spine == null || RightFoot == null || LeftFoot == null) return;

        // --- 1. CALCULATE LEG DIFFERENCE ---
        float rawHeightDiff = Mathf.Abs(LeftFoot.position.y - RightFoot.position.y);
        currentHeightDiff = Mathf.Lerp(currentHeightDiff, rawHeightDiff, Time.deltaTime * smoothSpeed);

        BodyFeedbackManager.FeedbackState legState;

        // --- 2. TRAFFIC LIGHT LOGIC ---
        if (currentHeightDiff <= legRedThreshold) legState = BodyFeedbackManager.FeedbackState.Wrong; 
        else if (currentHeightDiff >= legGreenThreshold) legState = BodyFeedbackManager.FeedbackState.Correct; 
        else legState = BodyFeedbackManager.FeedbackState.Warning; 
            
        SetColor("Legs", legState);

        // --- 3. 3-TRY STOPWATCH LOGIC ---
        if (legState == BodyFeedbackManager.FeedbackState.Correct)
        {
            idleTimer = 0f; 
            currentHoldTimer += Time.deltaTime;
            
            if (currentHoldTimer > sessionBestTimer)
            {
                sessionBestTimer = currentHoldTimer;
            }
        }
        else if (legState == BodyFeedbackManager.FeedbackState.Wrong) 
        {
            idleTimer += Time.deltaTime;

            if (currentHoldTimer > 0.5f) 
            {
                tryTimes.Add(currentHoldTimer); // NEW: Save this try's time
                sessionTotalTimer += currentHoldTimer;
                currentHoldTimer = 0f;
                currentTry++;
                
                if (currentTry > maxTries)
                {
                    FinishAssessment();
                    return;
                }
            }
            else if (currentHoldTimer > 0f)
            {
                currentHoldTimer = 0f; 
            }

            if (idleTimer >= maxRestTime)
            {
                FinishAssessment();
                return;
            }
        }

        UpdateUI();
    }

    private void FinishAssessment()
    {
        if (currentHoldTimer > 0)
        {
            tryTimes.Add(currentHoldTimer); // NEW: Save the final try if exiting early
            sessionTotalTimer += currentHoldTimer;
        }

        if (DatabaseManager.instance != null)
        {
            // NEW: Pass the array of tries, total, and best to the database
            DatabaseManager.instance.SaveBalanceData("Balance", tryTimes, sessionTotalTimer, sessionBestTimer);
        }

        if (AppManager.instance != null)
        {
            AppManager.instance.TriggerTimeOutcomeDelay("Balance", sessionTotalTimer, sessionBestTimer);
        }

        StopExercise();
    }

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

    // --- REWRITTEN UI LOGIC ---
    void UpdateUI()
    {
        if (AppManager.instance != null)
        {
            string timeDisplay = "";

            if (currentHoldTimer > 0f)
            {
                timeDisplay = $"{Mathf.FloorToInt(currentHoldTimer)}s";
            }
            else
            {
                int displayTry = Mathf.Min(currentTry, maxTries);
                timeDisplay = $"{displayTry}/{maxTries}";
            }

            AppManager.instance.UpdateGlobalRepText(timeDisplay);
        }
    }
}