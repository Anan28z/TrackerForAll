using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Firebase.Firestore;
using Firebase.Auth;
using Firebase.Extensions;
using System.Linq;
using UnityEngine.UI;

public class DoctorDashboardManager : MonoBehaviour
{
    [Header("Search & Add Patient (Top Section)")]
    public TMP_InputField searchBarAdd;
    public TMP_Dropdown dropdownAdd;

    [Header("View & Delete Patient (Middle Section)")]
    public TMP_InputField searchBarView;
    public TMP_Dropdown dropdownView;

    [Header("Exercise Detail Buttons")]
    // Assign all 3 buttons here — they start greyed out (interactable = false)
    // and unlock only after a patient is selected via View Patient
    public Button btnCurl;
    public Button btnSquat;
    public Button btnBalance;

    [Header("Exercise Detail Panels")]
    // Assign the 3 separate panels. DoctorPatientDetailManager lives on each one.
    public GameObject panelDoctorCurl;
    public GameObject panelDoctorSquat;
    public GameObject panelDoctorBalance;

    [Header("Dropdown Scroll Settings")]
    public int maxVisibleItems = 6;

    // Master data buckets
    private Dictionary<string, string> unassignedPatients = new Dictionary<string, string>();
    private Dictionary<string, string> myPatients         = new Dictionary<string, string>();

    // Filtered lists that mirror what the dropdowns show
    private List<string> currentAddNames  = new List<string>();
    private List<string> currentViewNames = new List<string>();

    private string currentDoctorId;
    private FirebaseFirestore db;

    // The patient the doctor last clicked View on
    private string viewedPatientUid  = "";
    private string viewedPatientName = "";

    // ===================== LIFECYCLE =====================

    void OnEnable()
    {
        var currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (currentUser == null)
        {
            Debug.LogWarning("DoctorDashboard: No user logged in.");
            return;
        }

        currentDoctorId = currentUser.UserId;
        db = FirebaseFirestore.DefaultInstance;

        // Exercise buttons are locked until a patient is viewed
        SetExerciseButtonsInteractable(false);

        FetchAllPatients();

        if (searchBarAdd  != null) searchBarAdd.onValueChanged.AddListener(FilterAddDropdown);
        if (searchBarView != null) searchBarView.onValueChanged.AddListener(FilterViewDropdown);
    }

    void OnDisable()
    {
        if (searchBarAdd  != null) searchBarAdd.onValueChanged.RemoveListener(FilterAddDropdown);
        if (searchBarView != null) searchBarView.onValueChanged.RemoveListener(FilterViewDropdown);
    }

    // ===================== FETCH ALL PATIENTS =====================

    public void FetchAllPatients()
    {
        Debug.Log("DoctorDashboard: Fetching all users...");

        db.Collection("Users").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("DoctorDashboard: Fetch FAILED — " + task.Exception);
                return;
            }

            unassignedPatients.Clear();
            myPatients.Clear();

            foreach (DocumentSnapshot doc in task.Result.Documents)
            {
                if (!doc.ContainsField("role")) continue;
                if (doc.GetValue<string>("role") != "Patient") continue;

                string pName      = doc.ContainsField("username")          ? doc.GetValue<string>("username")          : "Unknown";
                string pUid       = doc.Id;
                string assignedTo = doc.ContainsField("assignedDoctorId")  ? doc.GetValue<string>("assignedDoctorId")  : "";
                string requested  = doc.ContainsField("requestedDoctorId") ? doc.GetValue<string>("requestedDoctorId") : "";

                // Only show in "Search & Add" if the patient explicitly requested this doctor
                if (string.IsNullOrEmpty(assignedTo) && requested == currentDoctorId)
                    unassignedPatients[pName] = pUid;
                else if (assignedTo == currentDoctorId)
                    myPatients[pName] = pUid;
            }

            Debug.Log($"DoctorDashboard: Unassigned={unassignedPatients.Count} | Mine={myPatients.Count}");

            if (searchBarAdd  != null) searchBarAdd.text  = "";
            if (searchBarView != null) searchBarView.text = "";

            FilterAddDropdown("");
            FilterViewDropdown("");

            // If the viewed patient was just released, lock the exercise buttons again
            if (!string.IsNullOrEmpty(viewedPatientUid) && !myPatients.ContainsValue(viewedPatientUid))
            {
                viewedPatientUid  = "";
                viewedPatientName = "";
                SetExerciseButtonsInteractable(false);
            }
        });
    }

    // ===================== BTN 1 — ADD PATIENT =====================

    // Called by a Refresh button — re-fetches all patient data from Firestore
    public void OnRefreshClicked()
    {
        FetchAllPatients();
    }

    private void FilterAddDropdown(string searchTerm)
    {
        currentAddNames = FilterList(new List<string>(unassignedPatients.Keys), searchTerm);
        UpdateDropdown(dropdownAdd, currentAddNames);
    }

    public void OnAddPatientClicked()
    {
        if (currentAddNames.Count == 0) return;
        if (dropdownAdd.options.Count == 0) return;
        if (dropdownAdd.options[0].text == "No patients found") return;

        int idx = Mathf.Clamp(dropdownAdd.value, 0, currentAddNames.Count - 1);
        string selectedName = currentAddNames[idx];
        if (!unassignedPatients.ContainsKey(selectedName)) return;

        string selectedUid = unassignedPatients[selectedName];
        Debug.Log($"DoctorDashboard: Claiming '{selectedName}'...");

        db.Collection("Users").Document(selectedUid)
            .UpdateAsync("assignedDoctorId", currentDoctorId)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"DoctorDashboard: Claimed '{selectedName}'.");
                    FetchAllPatients();
                }
                else
                    Debug.LogError("DoctorDashboard: Claim failed — " + task.Exception);
            });
    }

    // ===================== BTN 2 — VIEW PATIENT =====================

    private void FilterViewDropdown(string searchTerm)
    {
        currentViewNames = FilterList(new List<string>(myPatients.Keys), searchTerm);
        UpdateDropdown(dropdownView, currentViewNames);
    }

    public void OnViewPatientClicked()
    {
        if (currentViewNames.Count == 0) return;
        if (dropdownView.options.Count == 0) return;
        if (dropdownView.options[0].text == "No patients found") return;

        int idx = Mathf.Clamp(dropdownView.value, 0, currentViewNames.Count - 1);
        string selectedName = currentViewNames[idx];
        if (!myPatients.ContainsKey(selectedName)) return;

        viewedPatientName = selectedName;
        viewedPatientUid  = myPatients[selectedName];

        Debug.Log($"DoctorDashboard: Selected patient '{viewedPatientName}' — exercise buttons unlocked.");

        // Unlock the 3 exercise buttons now that a patient is chosen
        SetExerciseButtonsInteractable(true);
    }

    // ===================== BTN 3 — DELETE (RELEASE) PATIENT =====================

    public void OnDeletePatientClicked()
    {
        if (currentViewNames.Count == 0) return;
        if (dropdownView.options.Count == 0) return;
        if (dropdownView.options[0].text == "No patients found") return;

        int idx = Mathf.Clamp(dropdownView.value, 0, currentViewNames.Count - 1);
        string selectedName = currentViewNames[idx];
        if (!myPatients.ContainsKey(selectedName)) return;

        string selectedUid = myPatients[selectedName];
        Debug.Log($"DoctorDashboard: Releasing '{selectedName}'...");

        db.Collection("Users").Document(selectedUid)
            .UpdateAsync("assignedDoctorId", "")
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"DoctorDashboard: Released '{selectedName}'.");
                    FetchAllPatients();
                }
                else
                    Debug.LogError("DoctorDashboard: Release failed — " + task.Exception);
            });
    }

    // ===================== EXERCISE PANEL BUTTONS =====================

    // Each of these is called by its button's OnClick() in the Inspector.
    // They pass the patient's UID and name into the target panel's detail manager.

    public void OnOpenCurlPanel()
    {
        if (string.IsNullOrEmpty(viewedPatientUid)) return;
        OpenDetailPanel(panelDoctorCurl, viewedPatientUid, viewedPatientName, "Curl");
    }

    public void OnOpenSquatPanel()
    {
        if (string.IsNullOrEmpty(viewedPatientUid)) return;
        OpenDetailPanel(panelDoctorSquat, viewedPatientUid, viewedPatientName, "Squat");
    }

    public void OnOpenBalancePanel()
    {
        if (string.IsNullOrEmpty(viewedPatientUid)) return;
        OpenDetailPanel(panelDoctorBalance, viewedPatientUid, viewedPatientName, "Balance");
    }

    private void OpenDetailPanel(GameObject panel, string uid, string name, string exerciseType)
    {
        if (panel == null)
        {
            Debug.LogWarning($"DoctorDashboard: Panel for {exerciseType} is not assigned.");
            return;
        }

        // Hand the patient context to the detail manager on that panel
        DoctorPatientDetailManager detail = panel.GetComponent<DoctorPatientDetailManager>();
        if (detail != null)
        {
            detail.LoadPatient(uid, name, exerciseType);
        }
        else
        {
            Debug.LogWarning($"DoctorDashboard: No DoctorPatientDetailManager found on {panel.name}");
        }

        // Show the panel via AppManager so all other panels close cleanly
        if (AppManager.instance != null)
            AppManager.instance.ShowDoctorPatientDetail(panel);
        else
            panel.SetActive(true);
    }

    // ===================== HELPERS =====================

    private void SetExerciseButtonsInteractable(bool state)
    {
        if (btnCurl    != null) btnCurl.interactable    = state;
        if (btnSquat   != null) btnSquat.interactable   = state;
        if (btnBalance != null) btnBalance.interactable = state;
    }

    private List<string> FilterList(List<string> fullList, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm)) return fullList;
        return fullList.Where(n => n.ToLower().Contains(searchTerm.ToLower())).ToList();
    }

    private void UpdateDropdown(TMP_Dropdown dropdown, List<string> names)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();

        if (names.Count == 0)
        {
            dropdown.AddOptions(new List<string> { "No patients found" });
            SetDropdownScrollHeight(dropdown, 1);
            return;
        }

        dropdown.AddOptions(names);
        SetDropdownScrollHeight(dropdown, Mathf.Min(names.Count, maxVisibleItems));
    }

    private void SetDropdownScrollHeight(TMP_Dropdown dropdown, int visibleCount)
    {
        Transform template = dropdown.transform.Find("Template");
        if (template == null) return;
        RectTransform templateRect = template.GetComponent<RectTransform>();
        if (templateRect == null) return;

        float itemHeight = 30f;
        Transform vp = template.Find("Viewport");
        if (vp != null)
        {
            Transform content = vp.Find("Content");
            if (content != null)
            {
                Transform item = content.Find("Item");
                if (item != null)
                {
                    RectTransform ir = item.GetComponent<RectTransform>();
                    if (ir != null) itemHeight = ir.rect.height;
                }
            }
        }
        templateRect.sizeDelta = new Vector2(templateRect.sizeDelta.x, itemHeight * visibleCount);
    }
}