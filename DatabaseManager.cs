using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;

public class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager instance;
    private FirebaseFirestore db;

    public AuthManager authManager;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        try {
            db = FirebaseFirestore.DefaultInstance;
        } catch (System.Exception e) {
            Debug.LogError("Firebase init delayed on mobile: " + e.Message);
        }
    }

    // --- NEW: Safe DB Getter for Mobile ---
    private FirebaseFirestore GetDB()
    {
        if (db == null)
        {
            try {
                db = FirebaseFirestore.DefaultInstance;
            } catch (System.Exception e) {
                Debug.LogError("Firestore not ready on mobile: " + e.Message);
            }
        }
        return db;
    }

    public void SaveExerciseData(string exerciseName, int totalReps, int greenReps, int yellowReps, int redReps)
    {
        try 
        {
            if (authManager == null || authManager.User == null)
            {
                Debug.LogError("Cannot save data: User is not logged in.");
                return;
            }

            FirebaseFirestore currentDB = GetDB();
            if (currentDB == null) return; // Prevent mobile crash

            string uid = authManager.User.UserId;
            string sessionId = "session_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            Dictionary<string, object> sessionData = new Dictionary<string, object>
            {
                { "timestamp", System.DateTime.UtcNow.ToString("O") }, 
                { "exercise", exerciseName },
                { "totalReps", totalReps },
                { "greenCount", greenReps },
                { "yellowCount", yellowReps },
                { "redCount", redReps }
            };

            Debug.Log($"Saving {exerciseName} data...");

            DocumentReference docRef = currentDB.Collection("Users").Document(uid).Collection("Sessions").Document(sessionId);

            docRef.SetAsync(sessionData).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"SUCCESS! {exerciseName} data uploaded to Firestore.");
                }
                else
                {
                    Debug.LogError("FAILED to upload data: " + task.Exception);
                }
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError("CRASH PREVENTED in SaveExerciseData: " + e.Message);
        }
    }

    public void SaveBalanceData(string exerciseName, List<float> tryTimes, float totalTime, float bestTime)
    {
        try 
        {
            if (authManager == null || authManager.User == null)
            {
                Debug.LogError("Cannot save data: User is not logged in.");
                return;
            }

            FirebaseFirestore currentDB = GetDB();
            if (currentDB == null) return; // Prevent mobile crash

            string uid = authManager.User.UserId;
            string sessionId = "session_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            Dictionary<string, object> sessionData = new Dictionary<string, object>
            {
                { "timestamp", System.DateTime.UtcNow.ToString("O") }, 
                { "exercise", exerciseName },
                { "totalTime", totalTime },
                { "bestTime", bestTime }
            };

            for (int i = 0; i < tryTimes.Count; i++)
            {
                sessionData.Add($"try{i + 1}", tryTimes[i]);
            }

            Debug.Log($"Saving {exerciseName} data...");

            DocumentReference docRef = currentDB.Collection("Users").Document(uid).Collection("Sessions").Document(sessionId);

            docRef.SetAsync(sessionData).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"SUCCESS! {exerciseName} data uploaded to Firestore.");
                }
                else
                {
                    Debug.LogError("FAILED to upload data: " + task.Exception);
                }
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError("CRASH PREVENTED in SaveBalanceData: " + e.Message);
        }
    }

    public void GetOverallBestBalanceTime(System.Action<float> callback)
    {
        try 
        {
            if (authManager == null || authManager.User == null)
            {
                callback?.Invoke(0f);
                return;
            }

            FirebaseFirestore currentDB = GetDB();
            if (currentDB == null) 
            {
                callback?.Invoke(0f);
                return;
            }

            string uid = authManager.User.UserId;
            currentDB.Collection("Users").Document(uid).Collection("Sessions")
                .WhereEqualTo("exercise", "Balance")
                .GetSnapshotAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        callback?.Invoke(0f);
                        return;
                    }

                    float overallBest = 0f;

                    foreach (DocumentSnapshot doc in task.Result.Documents)
                    {
                        Dictionary<string, object> session = doc.ToDictionary();
                        
                        if (session.ContainsKey("bestTime"))
                        {
                            float bTime = System.Convert.ToSingle(session["bestTime"]);
                            if (bTime > overallBest) overallBest = bTime;
                        }
                        else if (session.ContainsKey("holdTime")) // Catch old tests
                        {
                            float hTime = System.Convert.ToSingle(session["holdTime"]);
                            if (hTime > overallBest) overallBest = hTime;
                        }
                    }

                    callback?.Invoke(overallBest);
                });
        }
        catch (System.Exception e)
        {
            Debug.LogError("CRASH PREVENTED in GetOverallBestBalanceTime: " + e.Message);
            callback?.Invoke(0f); // Ensures the Outcome Panel UI continues loading even on failure
        }
    }
}