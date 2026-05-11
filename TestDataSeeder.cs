using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Auth;
using Firebase.Extensions;
using TMPro;

// ============================================================
// TestDataSeeder
// ============================================================
// Attach to any GameObject in the scene.
// Wire a Button's OnClick() to SeedData().
//
// IMPORTANT: Set targetPatientEmail in the Inspector to the
// email of the patient account you want to seed.
// The script will look up that patient's UID from the Users
// collection (by matching username or email) and insert all
// 93 sessions (31 days × 3 exercises) into their Sessions
// subcollection.
//
// To target a specific UID directly, leave targetPatientEmail
// blank and fill in targetPatientUID instead.
// ============================================================

public class TestDataSeeder : MonoBehaviour
{
    [Header("Target Patient — fill ONE of these")]
    [Tooltip("The username saved in Firestore (e.g. Test1)")]
    public string targetPatientUsername = "Test1";

    [Tooltip("Or paste the UID directly to skip the lookup")]
    public string targetPatientUID = "";

    [Header("Optional Status Text")]
    public TextMeshProUGUI statusText;

    private FirebaseFirestore db;

    // ===================== ENTRY POINT =====================

    // Wire this to your Seed button's OnClick()
    public void SeedData()
    {
        db = FirebaseFirestore.DefaultInstance;

        if (!string.IsNullOrEmpty(targetPatientUID))
        {
            Log("Seeding directly to UID: " + targetPatientUID);
            StartCoroutine(UploadAllSessions(targetPatientUID));
        }
        else
        {
            Log("Looking up patient: " + targetPatientUsername + "...");
            FindPatientAndSeed();
        }
    }

    // ===================== FIND PATIENT =====================

    private void FindPatientAndSeed()
    {
        db.Collection("Users").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Log("ERROR: Could not fetch users — " + task.Exception);
                return;
            }

            string foundUID = "";
            foreach (var doc in task.Result.Documents)
            {
                string username = doc.ContainsField("username") ? doc.GetValue<string>("username") : "";
                if (username == targetPatientUsername)
                {
                    foundUID = doc.Id;
                    break;
                }
            }

            if (string.IsNullOrEmpty(foundUID))
            {
                Log("ERROR: No patient found with username '" + targetPatientUsername + "'");
                return;
            }

            Log("Found patient UID: " + foundUID + " — uploading sessions...");
            StartCoroutine(UploadAllSessions(foundUID));
        });
    }

    // ===================== UPLOAD SESSIONS =====================

    private IEnumerator UploadAllSessions(string uid)
    {
        int uploaded = 0;
        int failed   = 0;
        var sessions = BuildAllSessions();

        foreach (var session in sessions)
        {
            bool done = false;
            bool ok   = false;

            string docId = "session_" + session.timestamp.ToString("yyyyMMdd_HHmmss");
            DocumentReference docRef = db.Collection("Users").Document(uid)
                                         .Collection("Sessions").Document(docId);

            docRef.SetAsync(session.data).ContinueWithOnMainThread(task =>
            {
                ok   = task.IsCompleted && !task.IsFaulted;
                done = true;
            });

            yield return new WaitUntil(() => done);

            if (ok) uploaded++;
            else    failed++;

            Log($"Progress: {uploaded + failed}/{sessions.Count} uploaded={uploaded} failed={failed}");
        }

        Log($"DONE. {uploaded} sessions uploaded, {failed} failed.");
    }

    // ===================== SESSION DATA =====================

    private class SessionEntry
    {
        public System.DateTime date;
        public System.DateTime timestamp;
        public Dictionary<string, object> data;
    }

    private List<SessionEntry> BuildAllSessions()
    {
        var list = new List<SessionEntry>();

        // Each day: Curl at 10:15, Squat at 10:30, Balance at 10:45
        // Progression: green improves, red decreases, balance hold time increases

        // Day 1 starts: Curl green=2 yellow=5 red=3
        // Day 31 ends:  Curl green=10 yellow=0 red=0
        // Balance starts ~4s, ends ~27s

        int[] greens  = { 2,3,3,4,4,5,5,6,6,6, 7,7,7,8,8,8,8,9,9,9, 9,9,9,10,10,10,10,10,10,10,10 };
        int[] yellows = { 5,5,5,4,5,4,4,3,3,4, 3,3,3,2,2,2,2,1,1,1, 1,1,1,0,0,0,0,0,0,0,0 };
        int[] reds    = { 3,2,2,2,1,1,1,1,1,0, 0,0,0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,0,0,0 };

        float[] holds = {
            4.125987654f,  4.876123456f,  5.543987123f,  6.129845678f,  6.874512345f,
            7.451298765f,  8.215678901f,  8.987654321f,  9.654321098f, 10.345678901f,
           11.123456789f, 11.876543210f, 12.543210987f, 13.219876543f, 13.987654321f,
           14.654321098f, 15.432109876f, 16.109876543f, 16.876543210f, 17.654321098f,
           18.432109876f, 19.109876543f, 19.876543210f, 20.654321098f, 21.432109876f,
           22.109876543f, 22.876543210f, 23.654321098f, 24.432109876f, 25.109876543f,
           26.876543210f
        };

        for (int day = 1; day <= 31; day++)
        {
            int i = day - 1;
            System.DateTime date = new System.DateTime(2026, 3, day, 0, 0, 0, System.DateTimeKind.Utc);

            // ---- Curl ----
            System.DateTime curlTime = date.AddHours(10).AddMinutes(15);
            list.Add(new SessionEntry
            {
                timestamp = curlTime,
                data = new Dictionary<string, object>
                {
                    { "exercise",   "Curl" },
                    { "timestamp",  curlTime.ToString("O") },
                    { "greenCount",  greens[i]  },
                    { "yellowCount", yellows[i] },
                    { "redCount",    reds[i]    },
                    { "totalReps",   10         }
                }
            });

            // ---- Squat ----
            System.DateTime squatTime = date.AddHours(10).AddMinutes(30);
            list.Add(new SessionEntry
            {
                timestamp = squatTime,
                data = new Dictionary<string, object>
                {
                    { "exercise",   "Squat" },
                    { "timestamp",  squatTime.ToString("O") },
                    { "greenCount",  greens[i]  },
                    { "yellowCount", yellows[i] },
                    { "redCount",    reds[i]    },
                    { "totalReps",   10         }
                }
            });

            // ---- Balance ----
            System.DateTime balTime = date.AddHours(10).AddMinutes(45);
            float best  = holds[i];
            float total = best * 1.6f; // Simulate 3 tries: best + 2 shorter attempts
            float try1  = best * 0.55f;
            float try2  = best * 0.45f;
            float try3  = best;

            list.Add(new SessionEntry
            {
                timestamp = balTime,
                data = new Dictionary<string, object>
                {
                    { "exercise",  "Balance"           },
                    { "timestamp", balTime.ToString("O") },
                    { "bestTime",  best                },
                    { "totalTime", total               },
                    { "try1",      try1                },
                    { "try2",      try2                },
                    { "try3",      try3                }
                }
            });
        }

        return list;
    }

    // ===================== HELPER =====================

    private void Log(string msg)
    {
        Debug.Log("[Seeder] " + msg);
        if (statusText != null) statusText.text = msg;
    }
}
