using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class ProfileManager : MonoBehaviour
{
    [Header("User Info UI")]
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI emailText;

    [Header("Curl Stats UI")]
    public TextMeshProUGUI curlSessionsText;
    public TextMeshProUGUI curlGreenText;
    public TextMeshProUGUI curlYellowText;
    public TextMeshProUGUI curlRedText;

    [Header("Squat Stats UI")]
    public TextMeshProUGUI squatSessionsText;
    public TextMeshProUGUI squatGreenText;
    public TextMeshProUGUI squatYellowText;
    public TextMeshProUGUI squatRedText;

    [Header("Balance Stats UI")]
    public TextMeshProUGUI balanceSessionsText;
    public TextMeshProUGUI balanceAvgText;
    public TextMeshProUGUI balanceBestText;

    // This runs automatically every time the panel is opened
    void OnEnable() 
    {
        FetchAndCalculateProfileData();
    }

    public void FetchAndCalculateProfileData()
    {
        var currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (currentUser == null)
        {
            Debug.LogWarning("ProfileManager: No user logged in.");
            return;
        }

        // 1. Set User Info natively from Auth Token
        if (usernameText != null) 
        {
            usernameText.text = string.IsNullOrEmpty(currentUser.DisplayName) ? "Patient" : currentUser.DisplayName;
        }
        if (emailText != null) 
        {
            emailText.text = currentUser.Email;
        }

        ResetUI(); // Clear old numbers while it loads

        // 2. Access Firestore Database
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        CollectionReference sessionsRef = db.Collection("Users").Document(currentUser.UserId).Collection("Sessions");

        // 3. Download and tally the data
        sessionsRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Failed to fetch sessions: " + task.Exception);
                return;
            }

            // Math Trackers
            int curlSessions = 0, curlGreen = 0, curlYellow = 0, curlRed = 0;
            int squatSessions = 0, squatGreen = 0, squatYellow = 0, squatRed = 0;
            int balanceSessions = 0;
            float totalBalanceTime = 0f;
            float bestBalanceTime = 0f;

            // Loop through every single session document
            foreach (DocumentSnapshot document in task.Result.Documents)
            {
                Dictionary<string, object> session = document.ToDictionary();
                
                if (!session.ContainsKey("exercise")) continue;
                string exerciseType = session["exercise"].ToString();

                // Tally Curls
                if (exerciseType == "Curl")
                {
                    curlSessions++;
                    if (session.ContainsKey("greenCount")) curlGreen += System.Convert.ToInt32(session["greenCount"]);
                    if (session.ContainsKey("yellowCount")) curlYellow += System.Convert.ToInt32(session["yellowCount"]);
                    if (session.ContainsKey("redCount")) curlRed += System.Convert.ToInt32(session["redCount"]);
                }
                // Tally Squats
                else if (exerciseType == "Squat")
                {
                    squatSessions++;
                    if (session.ContainsKey("greenCount")) squatGreen += System.Convert.ToInt32(session["greenCount"]);
                    if (session.ContainsKey("yellowCount")) squatYellow += System.Convert.ToInt32(session["yellowCount"]);
                    if (session.ContainsKey("redCount")) squatRed += System.Convert.ToInt32(session["redCount"]);
                }
                // Tally Balance
                else if (exerciseType == "Balance")
                {
                    balanceSessions++;
                    
                    // FIXED: Now checks for the new 3-Try data format
                    if (session.ContainsKey("bestTime") && session.ContainsKey("totalTime"))
                    {
                        float bTime = System.Convert.ToSingle(session["bestTime"]);
                        float tTime = System.Convert.ToSingle(session["totalTime"]);
                        
                        totalBalanceTime += tTime;
                        if (bTime > bestBalanceTime) bestBalanceTime = bTime;
                    }
                    // FALLBACK: Keeps old test data from breaking the dashboard
                    else if (session.ContainsKey("holdTime"))
                    {
                        float time = System.Convert.ToSingle(session["holdTime"]);
                        totalBalanceTime += time;
                        if (time > bestBalanceTime) bestBalanceTime = time;
                    }
                }
            }

            // 4. Push final tallies to the UI
            if (curlSessionsText != null) curlSessionsText.text = curlSessions.ToString();
            if (curlGreenText != null) curlGreenText.text = curlGreen.ToString();
            if (curlYellowText != null) curlYellowText.text = curlYellow.ToString();
            if (curlRedText != null) curlRedText.text = curlRed.ToString();

            if (squatSessionsText != null) squatSessionsText.text = squatSessions.ToString();
            if (squatGreenText != null) squatGreenText.text = squatGreen.ToString();
            if (squatYellowText != null) squatYellowText.text = squatYellow.ToString();
            if (squatRedText != null) squatRedText.text = squatRed.ToString();

            if (balanceSessionsText != null) balanceSessionsText.text = balanceSessions.ToString();
            if (balanceAvgText != null) 
            {
                float avg = balanceSessions > 0 ? totalBalanceTime / balanceSessions : 0f;
                balanceAvgText.text = Mathf.RoundToInt(avg).ToString() + "s";
            }
            if (balanceBestText != null) balanceBestText.text = Mathf.RoundToInt(bestBalanceTime).ToString() + "s";
        });
    }

    private void ResetUI()
    {
        if (curlSessionsText != null) curlSessionsText.text = "0";
        if (curlGreenText != null) curlGreenText.text = "0";
        if (curlYellowText != null) curlYellowText.text = "0";
        if (curlRedText != null) curlRedText.text = "0";

        if (squatSessionsText != null) squatSessionsText.text = "0";
        if (squatGreenText != null) squatGreenText.text = "0";
        if (squatYellowText != null) squatYellowText.text = "0";
        if (squatRedText != null) squatRedText.text = "0";

        if (balanceSessionsText != null) balanceSessionsText.text = "0";
        if (balanceAvgText != null) balanceAvgText.text = "0s";
        if (balanceBestText != null) balanceBestText.text = "0s";
    }
}