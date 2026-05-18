using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Linq;

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

    [Header("Add Doctor UI")]
    public TMP_Dropdown doctorDropdown;
    public TextMeshProUGUI addDoctorStatusText;

    // Maps display name → doctor UID for the dropdown
    private Dictionary<string, string> availableDoctors = new Dictionary<string, string>();
    private List<string> doctorNames = new List<string>();

    // This runs automatically every time the panel is opened
    void OnEnable()
    {
        FetchAndCalculateProfileData();
        FetchAvailableDoctors();
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

    // ===================== DOCTOR SELECTION =====================

    private void FetchAvailableDoctors()
    {
        if (doctorDropdown == null) return;

        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        db.Collection("Users").WhereEqualTo("role", "Doctor").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("ProfileManager: Failed to fetch doctors — " + task.Exception);
                return;
            }

            availableDoctors.Clear();
            foreach (DocumentSnapshot doc in task.Result.Documents)
            {
                string dName = doc.ContainsField("username") ? doc.GetValue<string>("username") : "Unknown Doctor";
                availableDoctors[dName] = doc.Id;
            }

            doctorNames = new List<string>(availableDoctors.Keys);
            doctorDropdown.ClearOptions();
            if (doctorNames.Count == 0)
            {
                doctorDropdown.AddOptions(new List<string> { "No doctors available" });
            }
            else
            {
                doctorDropdown.AddOptions(doctorNames);
            }

            // Show current requested doctor if one is already set
            var currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            if (currentUser == null) return;

            db.Collection("Users").Document(currentUser.UserId).GetSnapshotAsync().ContinueWithOnMainThread(userTask =>
            {
                if (userTask.IsFaulted || !userTask.Result.Exists) return;

                string requestedId = userTask.Result.ContainsField("requestedDoctorId")
                    ? userTask.Result.GetValue<string>("requestedDoctorId") : "";

                if (!string.IsNullOrEmpty(requestedId))
                {
                    string requestedName = availableDoctors.FirstOrDefault(kv => kv.Value == requestedId).Key;
                    if (requestedName != null)
                    {
                        int idx = doctorNames.IndexOf(requestedName);
                        if (idx >= 0) doctorDropdown.value = idx;
                        if (addDoctorStatusText != null)
                            addDoctorStatusText.text = $"Requested: {requestedName}";
                    }
                }
            });
        });
    }

    public void OnAddDoctorClicked()
    {
        if (doctorDropdown == null || doctorNames.Count == 0) return;
        if (doctorDropdown.options.Count == 0) return;
        if (doctorDropdown.options[0].text == "No doctors available") return;

        int idx = Mathf.Clamp(doctorDropdown.value, 0, doctorNames.Count - 1);
        string selectedName = doctorNames[idx];
        if (!availableDoctors.ContainsKey(selectedName)) return;

        string selectedDoctorId = availableDoctors[selectedName];

        var currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (currentUser == null) return;

        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        db.Collection("Users").Document(currentUser.UserId)
            .SetAsync(new Dictionary<string, object> { { "requestedDoctorId", selectedDoctorId } }, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"ProfileManager: Requested doctor '{selectedName}'.");
                    if (addDoctorStatusText != null)
                        addDoctorStatusText.text = $"Requested: {selectedName}";
                }
                else
                {
                    Debug.LogError("ProfileManager: Failed to request doctor — " + task.Exception);
                    if (addDoctorStatusText != null)
                        addDoctorStatusText.text = "Error. Try again.";
                }
            });
    }

    // ===================== RESET UI =====================

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