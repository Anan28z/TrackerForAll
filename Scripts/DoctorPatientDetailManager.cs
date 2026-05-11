using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Linq;
using UnityEngine.UI;

// ============================================================
// DoctorPatientDetailManager
// ============================================================
// Attach ONE copy to each panel: Panel_DoctorCurl,
// Panel_DoctorSquat, Panel_DoctorBalance.
//
// WINDOW STATS (top section, changes with 7D/30D/1Y button):
//   Curl/Squat → Total green/yellow/red across the window
//   Balance    → Total best hold + total hold across window
//
// ALL-TIME STATS (bottom section, fixed after load):
//   Curl/Squat → Lifetime green/yellow/red + session count
//   Balance    → Lifetime best/avg/total hold + session count
// ============================================================

public class DoctorPatientDetailManager : MonoBehaviour
{
    // ===================== INSPECTOR FIELDS =====================

    [Header("Header")]
    public TextMeshProUGUI patientNameText;    // Name GameObject
    public TextMeshProUGUI exerciseTitleText;  // Header GameObject

    [Header("Graph")]
    public LineGraphRenderer graphRenderer;    // Graph child (RawImage + LineGraphRenderer)

    [Header("Time Range Buttons")]
    public Button btn7Days;    // Btn_7Days  → OnClick: On7Days()
    public Button btn30Days;   // Btn_30Days → OnClick: On30Days()
    public Button btn1Year;    // Btn_1Year  → OnClick: On1Year()

    // ---- WINDOW STATS — update every time the time button changes ----
    // Each parent (e.g. WindowTotalGreen) has a BestGreen child TMP text
    // Drag the TMP text child into these slots, NOT the parent object

    [Header("Window Stats — Curl & Squat (drag TMP text children)")]
    public TextMeshProUGUI windowTotalGreenText;   // WindowTotalGreen  → BestGreen TMP
    public TextMeshProUGUI windowTotalYellowText;  // WindowTotalYellow → BestGreen TMP
    public TextMeshProUGUI windowTotalRedText;     // WindowTotalRed    → BestGreen TMP

    [Header("Window Stats — Balance (drag TMP text children)")]
    public TextMeshProUGUI windowTotalBestHoldText;  // total best hold in window
    public TextMeshProUGUI windowTotalHoldText;      // total hold time in window

    // ---- ALL-TIME STATS — calculated once on load, never change ----

    [Header("All-Time Stats — Curl & Squat (drag TMP text children)")]
    public TextMeshProUGUI allTimeGreenText;    // AllTotalGreen  → BestGreen TMP
    public TextMeshProUGUI allTimeYellowText;   // AllTotalYellow → BestGreen TMP
    public TextMeshProUGUI allTimeRedText;      // AllTotalRed    → BestGreen TMP
    public TextMeshProUGUI allTimeSessionsText; // AllTimeSessions TMP

    [Header("All-Time Stats — Balance (drag TMP text children)")]
    public TextMeshProUGUI allTimeBestHoldText;
    public TextMeshProUGUI allTimeAvgHoldText;
    public TextMeshProUGUI allTimeTotalHoldText;
    public TextMeshProUGUI allTimeBalanceSessionsText;

    // ===================== INTERNAL STATE =====================

    private string patientUid   = "";
    private string patientName  = "";
    private string exerciseType = ""; // "Curl", "Squat", or "Balance"

    private List<SessionRecord> allSessions = new List<SessionRecord>();

    private enum TimeRange { Week, Month, Year }
    private TimeRange currentRange = TimeRange.Week;

    private readonly Color colorActive   = new Color(0.20f, 0.55f, 1.00f, 1.00f);
    private readonly Color colorInactive = new Color(1.00f, 1.00f, 1.00f, 0.15f);

    // ===================== PUBLIC ENTRY POINT =====================

    // Called by DoctorDashboardManager before this panel is shown
    public void LoadPatient(string uid, string name, string exercise)
    {
        patientUid   = uid;
        patientName  = name;
        exerciseType = exercise;

        if (patientNameText   != null) patientNameText.text   = name;
        if (exerciseTitleText != null) exerciseTitleText.text = exercise;

        ResetAllUI();
        FetchSessions();
    }

    // ===================== TIME RANGE BUTTONS =====================

    public void On7Days()
    {
        currentRange = TimeRange.Week;
        HighlightBtn(btn7Days);
        RefreshGraphAndWindowStats();
    }

    public void On30Days()
    {
        currentRange = TimeRange.Month;
        HighlightBtn(btn30Days);
        RefreshGraphAndWindowStats();
    }

    public void On1Year()
    {
        currentRange = TimeRange.Year;
        HighlightBtn(btn1Year);
        RefreshGraphAndWindowStats();
    }

    // ===================== FETCH FROM FIRESTORE =====================

    private void FetchSessions()
    {
        if (string.IsNullOrEmpty(patientUid)) return;

        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        db.Collection("Users").Document(patientUid).Collection("Sessions")
            .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("DoctorDetail: Fetch failed — " + task.Exception);
                    return;
                }

                allSessions.Clear();

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    var data = doc.ToDictionary();

                    if (!data.ContainsKey("exercise")) continue;
                    if (data["exercise"].ToString() != exerciseType) continue;
                    if (!data.ContainsKey("timestamp")) continue;

                    System.DateTime date;
                    if (!System.DateTime.TryParse(data["timestamp"].ToString(), out date)) continue;

                    var rec = new SessionRecord { date = date.ToUniversalTime() };

                    if (exerciseType == "Balance")
                    {
                        rec.bestHold  = data.ContainsKey("bestTime")  ? System.Convert.ToSingle(data["bestTime"])  : 0f;
                        rec.totalHold = data.ContainsKey("totalTime") ? System.Convert.ToSingle(data["totalTime"]) : 0f;

                        // Fallback for old single-holdTime format
                        if (rec.bestHold == 0f && data.ContainsKey("holdTime"))
                        {
                            rec.bestHold  = System.Convert.ToSingle(data["holdTime"]);
                            rec.totalHold = rec.bestHold;
                        }
                    }
                    else
                    {
                        rec.green  = data.ContainsKey("greenCount")  ? System.Convert.ToInt32(data["greenCount"])  : 0;
                        rec.yellow = data.ContainsKey("yellowCount") ? System.Convert.ToInt32(data["yellowCount"]) : 0;
                        rec.red    = data.ContainsKey("redCount")    ? System.Convert.ToInt32(data["redCount"])    : 0;
                    }

                    allSessions.Add(rec);
                }

                // Sort oldest → newest so graph reads left to right
                allSessions = allSessions.OrderBy(s => s.date).ToList();

                Debug.Log($"DoctorDetail: {allSessions.Count} {exerciseType} sessions loaded for {patientName}.");

                // All-time block is calculated once and never touched again
                CalculateAllTimeTotals();

                // Default to 7-day view
                currentRange = TimeRange.Week;
                HighlightBtn(btn7Days);
                RefreshGraphAndWindowStats();
            });
    }

    // ===================== GRAPH + WINDOW STATS =====================

    private void RefreshGraphAndWindowStats()
    {
        System.DateTime now = System.DateTime.UtcNow;
        float rangeDays;
        System.DateTime cutoff;

        switch (currentRange)
        {
            case TimeRange.Month: cutoff = now.AddDays(-30);  rangeDays = 30f;  break;
            case TimeRange.Year:  cutoff = now.AddDays(-365); rangeDays = 365f; break;
            default:              cutoff = now.AddDays(-7);   rangeDays = 7f;   break;
        }

        // Sessions inside this time window only
        List<SessionRecord> window = allSessions.Where(s => s.date >= cutoff).ToList();

        // ---- Graph ----
        var seriesList = new List<LineGraphRenderer.Series>();

        // Convert TimeRange enum
        LineGraphRenderer.TimeRange graphRange =
            currentRange == TimeRange.Month ? LineGraphRenderer.TimeRange.Month :
            currentRange == TimeRange.Year  ? LineGraphRenderer.TimeRange.Year  :
                                              LineGraphRenderer.TimeRange.Week;

        LineGraphRenderer.ExerciseKind graphKind = (exerciseType == "Balance")
            ? LineGraphRenderer.ExerciseKind.Balance
            : LineGraphRenderer.ExerciseKind.RepBased;

        if (exerciseType == "Balance")
        {
            seriesList.Add(BuildSeries("Best Hold",  window, cutoff, rangeDays, s => s.bestHold,  0, graphRange));
            seriesList.Add(BuildSeries("Total Hold", window, cutoff, rangeDays, s => s.totalHold, 1, graphRange));
        }
        else
        {
            seriesList.Add(BuildSeries("Green",  window, cutoff, rangeDays, s => s.green,  0, graphRange));
            seriesList.Add(BuildSeries("Yellow", window, cutoff, rangeDays, s => s.yellow, 1, graphRange));
            seriesList.Add(BuildSeries("Red",    window, cutoff, rangeDays, s => s.red,    2, graphRange));
        }

        if (graphRenderer != null)
            graphRenderer.DrawGraph(seriesList, graphRange, graphKind);

        // ---- Window stats ----
        UpdateWindowStats(window);
    }

    private LineGraphRenderer.Series BuildSeries(
        string name,
        List<SessionRecord> records,
        System.DateTime cutoff,
        float rangeDays,
        System.Func<SessionRecord, float> valueSelector,
        int colorIndex,
        LineGraphRenderer.TimeRange graphRange)
    {
        var s = new LineGraphRenderer.Series
        {
            name       = name,
            colorIndex = colorIndex,
            xValues    = new List<float>(),
            yValues    = new List<float>()
        };

        foreach (var rec in records)
        {
            float x;
            if (graphRange == LineGraphRenderer.TimeRange.Year)
            {
                // X = month number 1..12
                x = rec.date.Month;
            }
            else if (graphRange == LineGraphRenderer.TimeRange.Month)
            {
                // X = day within the 30-day window, 1..30
                x = Mathf.Clamp((float)(rec.date - cutoff).TotalDays + 1f, 1f, 30f);
            }
            else
            {
                // X = day within the 7-day window, 1..7
                x = Mathf.Clamp((float)(rec.date - cutoff).TotalDays + 1f, 1f, 7f);
            }

            s.xValues.Add(x);
            s.yValues.Add(valueSelector(rec));
        }

        return s;
    }

    // Updates the top window-stats section based on the currently filtered sessions
    private void UpdateWindowStats(List<SessionRecord> window)
    {
        if (exerciseType == "Balance")
        {
            if (window.Count == 0)
            {
                if (windowTotalBestHoldText != null) windowTotalBestHoldText.text = "No data";
                if (windowTotalHoldText     != null) windowTotalHoldText.text     = "No data";
                return;
            }

            // Total of all best-hold values across sessions in window
            float totalBest  = window.Sum(s => s.bestHold);
            // Total of all hold times across sessions in window
            float totalHold  = window.Sum(s => s.totalHold);

            if (windowTotalBestHoldText != null) windowTotalBestHoldText.text = Mathf.RoundToInt(totalBest) + "s";
            if (windowTotalHoldText     != null) windowTotalHoldText.text     = Mathf.RoundToInt(totalHold) + "s";
        }
        else
        {
            if (window.Count == 0)
            {
                if (windowTotalGreenText  != null) windowTotalGreenText.text  = "No data";
                if (windowTotalYellowText != null) windowTotalYellowText.text = "No data";
                if (windowTotalRedText    != null) windowTotalRedText.text    = "No data";
                return;
            }

            // Total reps of each colour across all sessions in the window
            int totalGreen  = window.Sum(s => s.green);
            int totalYellow = window.Sum(s => s.yellow);
            int totalRed    = window.Sum(s => s.red);

            if (windowTotalGreenText  != null) windowTotalGreenText.text  = totalGreen.ToString();
            if (windowTotalYellowText != null) windowTotalYellowText.text = totalYellow.ToString();
            if (windowTotalRedText    != null) windowTotalRedText.text    = totalRed.ToString();
        }
    }

    // ===================== ALL-TIME TOTALS =====================

    // Runs once after fetch — writes the bottom section, never touched again
    private void CalculateAllTimeTotals()
    {
        int count = allSessions.Count;

        if (exerciseType == "Balance")
        {
            float best  = count > 0 ? allSessions.Max(s => s.bestHold) : 0f;
            float total = allSessions.Sum(s => s.totalHold);
            float avg   = count > 0 ? total / count : 0f;

            if (allTimeBalanceSessionsText != null) allTimeBalanceSessionsText.text = count.ToString();
            if (allTimeBestHoldText        != null) allTimeBestHoldText.text        = Mathf.RoundToInt(best)  + "s";
            if (allTimeTotalHoldText       != null) allTimeTotalHoldText.text       = Mathf.RoundToInt(total) + "s";
            if (allTimeAvgHoldText         != null) allTimeAvgHoldText.text         = Mathf.RoundToInt(avg)   + "s";
        }
        else
        {
            int totalGreen  = allSessions.Sum(s => s.green);
            int totalYellow = allSessions.Sum(s => s.yellow);
            int totalRed    = allSessions.Sum(s => s.red);

            if (allTimeSessionsText != null) allTimeSessionsText.text = count.ToString();
            if (allTimeGreenText    != null) allTimeGreenText.text    = totalGreen.ToString();
            if (allTimeYellowText   != null) allTimeYellowText.text   = totalYellow.ToString();
            if (allTimeRedText      != null) allTimeRedText.text      = totalRed.ToString();
        }
    }

    // ===================== UI HELPERS =====================

    private void ResetAllUI()
    {
        SetDash(windowTotalGreenText, windowTotalYellowText, windowTotalRedText,
                windowTotalBestHoldText, windowTotalHoldText,
                allTimeGreenText, allTimeYellowText, allTimeRedText,
                allTimeSessionsText, allTimeBestHoldText, allTimeTotalHoldText,
                allTimeAvgHoldText, allTimeBalanceSessionsText);
    }

    private void SetDash(params TextMeshProUGUI[] fields)
    {
        foreach (var f in fields)
            if (f != null) f.text = "—";
    }

    private void HighlightBtn(Button active)
    {
        SetBtnColor(btn7Days,  active == btn7Days);
        SetBtnColor(btn30Days, active == btn30Days);
        SetBtnColor(btn1Year,  active == btn1Year);
    }

    private void SetBtnColor(Button btn, bool isActive)
    {
        if (btn == null) return;

        // Set Image color directly for the visual highlight
        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? colorActive : colorInactive;

        // IMPORTANT: always keep transition on ColorTint so clicks still register.
        // We just override normalColor in the ColorBlock so Unity's tint
        // multiplies on top of our chosen base color.
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor      = Color.white; // white = no tint, our Image color shows through
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        cb.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.selectedColor    = Color.white;
        btn.colors = cb;
    }

    // ===================== DATA MODEL =====================

    private class SessionRecord
    {
        public System.DateTime date;
        public int   green, yellow, red;   // Curl / Squat
        public float bestHold, totalHold;  // Balance
    }
}