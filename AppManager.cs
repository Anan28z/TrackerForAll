using UnityEngine;
using TrackerPro.Unity;
using System.Collections;
using UnityEngine.Video; 
using TMPro;           
using UnityEngine.UI;

public class AppManager : MonoBehaviour
{
    public static AppManager instance;

    [Header("UI Panels")]
    public GameObject panelLogin;
    public GameObject panelRegister;
    public GameObject panelMainMenu;
    public GameObject panelSelection;
    public GameObject panelSettings;
    public GameObject panelVideo;      
    public GameObject panelCountdown;  
    public GameObject panelOutcome;
    public GameObject panelProfile; 
    public GameObject panelTimeOutcome; 
    public GameObject panelDoctorDashboard;  // Doctor CRM panel
    public GameObject panelDoctorCurl;       // Lives outside Panel_DoctorDashboard
    public GameObject panelDoctorSquat;
    public GameObject panelDoctorBalance;
    private GameObject activeDoctorDetailPanel = null;

    [Header("Global Exercise UI")]
    public TextMeshProUGUI globalRepText; 

    [Header("Outcome Panel Setup")]
    public Image outcomeHeaderImage;
    public TextMeshProUGUI outcomeOverallText;
    public TextMeshProUGUI outcomeGreenText;
    public TextMeshProUGUI outcomeYellowText;
    public TextMeshProUGUI outcomeRedText;

    [Header("Time Outcome Panel Setup")]
    public Image timeOutcomeHeaderImage;
    public TextMeshProUGUI timeOutcomeTotalText;
    public TextMeshProUGUI timeOutcomeSessionBestText;
    public TextMeshProUGUI timeOutcomeOverallBestText;

    [Header("Outcome Header Sprites")]
    public Sprite headerCurl;
    public Sprite headerSquat;
    public Sprite headerBalance;

    [Header("Camera Auto-Switch")]
    public Solution trackerSolution;
    public int targetCameraIndex = 2;

    [Header("Exercise Managers")]
    public CurlEvaluator curlEvaluator;
    public SquatManager squatManager;
    public BalanceManager balanceManager; 

    [Header("Video Setup")]
    public VideoPlayer videoPlayer;
    public VideoClip clipCurl;
    public VideoClip clipSquat;
    public VideoClip clipBalance;
    private Coroutine flowCoroutine;

    [Header("Countdown Setup")]
    public TextMeshProUGUI countdownText;

    private int activeExerciseIndex = 0; 

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
    }

    // ===================== HELPERS =====================

    // Turns off every panel at once — used internally before showing any one panel
    private void HideAllPanels()
    {
        panelLogin.SetActive(false);
        panelRegister.SetActive(false);
        panelMainMenu.SetActive(false);
        panelSelection.SetActive(false);
        panelSettings.SetActive(false);
        if (panelVideo) panelVideo.SetActive(false);
        if (panelCountdown) panelCountdown.SetActive(false);
        if (panelOutcome) panelOutcome.SetActive(false);
        if (panelTimeOutcome) panelTimeOutcome.SetActive(false);
        if (panelProfile) panelProfile.SetActive(false);
        if (panelDoctorDashboard) panelDoctorDashboard.SetActive(false);
        if (panelDoctorCurl)      panelDoctorCurl.SetActive(false);
        if (panelDoctorSquat)     panelDoctorSquat.SetActive(false);
        if (panelDoctorBalance)   panelDoctorBalance.SetActive(false);
        activeDoctorDetailPanel = null;
    }

    public void UpdateGlobalRepText(string textToDisplay)
    {
        if (globalRepText != null) globalRepText.text = textToDisplay;
    }

    public void ClearGlobalRepText()
    {
        if (globalRepText != null) globalRepText.text = "";
    }

    // ===================== PANEL ROUTING =====================

    void Start()
    {
        ShowLogin();
    }

    public void ShowLogin()
    {
        HideAllPanels();
        panelLogin.SetActive(true);
    }

    public void ShowRegister()
    {
        HideAllPanels();
        panelRegister.SetActive(true);
    }

    public void ShowMainMenu()
    {
        HideAllPanels();
        panelMainMenu.SetActive(true);
        StopAllExercises(); 
    }

    public void ShowProfile()
    {
        HideAllPanels();
        if (panelProfile) panelProfile.SetActive(true);
        StopAllExercises();
    }

    // --- NEW: Routes a Doctor straight to their CRM dashboard after login ---
    public void ShowDoctorDashboard()
    {
        HideAllPanels();
        if (panelDoctorDashboard) panelDoctorDashboard.SetActive(true);
        else Debug.LogWarning("AppManager: panelDoctorDashboard is not assigned in the Inspector!");
    }

    // --- NEW: Opens a specific exercise detail panel, closes everything else ---
    public void ShowDoctorPatientDetail(GameObject detailPanel)
    {
        HideAllPanels();
        activeDoctorDetailPanel = detailPanel;
        if (detailPanel != null) detailPanel.SetActive(true);
    }

    // --- Called by the Back button on any detail panel ---
    public void OnDoctorDetailBackClicked()
    {
        ShowDoctorDashboard();
    }

    // ===================== EXERCISE FLOW =====================

    public void OnStartClicked()
    {
        panelMainMenu.SetActive(false);
        panelSelection.SetActive(true);
        panelSettings.SetActive(false);

        if (videoPlayer != null) videoPlayer.Prepare(); 

        if (trackerSolution != null) StartCoroutine(SwitchCameraOnStart());
    }

    private IEnumerator SwitchCameraOnStart()
    {
        var imageSource = ImageSourceProvider.ImageSource;

        if (imageSource != null && imageSource.sourceCandidateNames != null && imageSource.sourceCandidateNames.Length > targetCameraIndex)
        {
            imageSource.SelectSource(targetCameraIndex);
            trackerSolution.Pause();
            yield return new WaitForSecondsRealtime(1.0f);
            trackerSolution.Play();
        }
    }

    public void OnBackToMenuClicked()
    {
        ShowMainMenu();
    }

    public void OnBackToSelectionClicked()
    {
        StopAllExercises();
        HideAllPanels();
        panelSelection.SetActive(true);
    }

    public void OnQuitClicked()
    {
        Application.Quit();
    }

    public void OnCurlSelected() { StartTutorialFlow(0, clipCurl); }
    public void OnSquatSelected() { StartTutorialFlow(1, clipSquat); }
    public void OnFullBodySelected() { StartTutorialFlow(2, clipBalance); }

    private void StartTutorialFlow(int index, VideoClip clip)
    {
        activeExerciseIndex = index;
        if (trackerSolution != null) trackerSolution.Pause();

        panelSelection.SetActive(false);
        panelVideo.SetActive(true);

        if (videoPlayer != null)
        {
            videoPlayer.clip = clip;
            videoPlayer.Play();
            flowCoroutine = StartCoroutine(WaitForTutorialEnd());
        }
    }

    public void OnSkipClicked()
    {
        if (flowCoroutine != null) StopCoroutine(flowCoroutine);
        if (videoPlayer) videoPlayer.Stop();
        StartCoroutine(RunCountdown());
    }

    private IEnumerator WaitForTutorialEnd()
    {
        yield return new WaitUntil(() => videoPlayer.isPlaying);
        while (videoPlayer.isPlaying) yield return null;
        StartCoroutine(RunCountdown());
    }

    private IEnumerator RunCountdown()
    {
        panelVideo.SetActive(false);
        panelCountdown.SetActive(true);

        string[] sequence = { "3", "2", "1", "Begin!" };
        foreach (string val in sequence)
        {
            if (countdownText) countdownText.text = val;
            yield return new WaitForSeconds(1.0f);
        }

        panelCountdown.SetActive(false);
        if (trackerSolution != null) trackerSolution.Play();

        SwitchToExercise(activeExerciseIndex == 0, activeExerciseIndex == 1, activeExerciseIndex == 2);
    }

    private void SwitchToExercise(bool curlOn, bool squatOn, bool balanceOn)
    {
        StopAllExercises();
        panelSelection.SetActive(false);
        panelSettings.SetActive(true);

        ClearGlobalRepText();

        if (curlOn && curlEvaluator != null) curlEvaluator.StartExercise();
        if (squatOn && squatManager != null) squatManager.StartExercise();
        if (balanceOn && balanceManager != null) balanceManager.StartExercise();
    }

    private void StopAllExercises()
    {
        if (curlEvaluator != null) curlEvaluator.StopExercise();
        if (squatManager != null) squatManager.StopExercise();
        if (balanceManager != null) balanceManager.StopExercise();
        if (videoPlayer && videoPlayer.isPlaying) videoPlayer.Stop();

        ClearGlobalRepText();
    }

    // ===================== OUTCOME PANELS =====================

    public void TriggerOutcomeDelay(string exerciseName, int green, int yellow, int red, int total = 10)
    {
        UpdateGlobalRepText("Done!");
        StartCoroutine(ShowOutcomeCoroutine(exerciseName, green, yellow, red, total));
    }

    private IEnumerator ShowOutcomeCoroutine(string exerciseName, int green, int yellow, int red, int total)
    {
        yield return new WaitForSeconds(1.0f);

        StopAllExercises();
        panelSettings.SetActive(false); 

        int completedReps = green + yellow + red;

        if (outcomeOverallText != null) outcomeOverallText.text = $"{completedReps} / {total}";
        if (outcomeGreenText != null) outcomeGreenText.text = green.ToString();
        if (outcomeYellowText != null) outcomeYellowText.text = yellow.ToString();
        if (outcomeRedText != null) outcomeRedText.text = red.ToString();

        if (outcomeHeaderImage != null)
        {
            if (exerciseName == "Curl" && headerCurl != null) outcomeHeaderImage.sprite = headerCurl;
            else if (exerciseName == "Squat" && headerSquat != null) outcomeHeaderImage.sprite = headerSquat;
            else if (exerciseName == "Balance" && headerBalance != null) outcomeHeaderImage.sprite = headerBalance;
        }

        panelOutcome.SetActive(true);
    }

    public void TriggerTimeOutcomeDelay(string exerciseName, float totalTime, float sessionBest)
    {
        UpdateGlobalRepText("Done!");
        StartCoroutine(ShowTimeOutcomeCoroutine(exerciseName, totalTime, sessionBest));
    }

    private IEnumerator ShowTimeOutcomeCoroutine(string exerciseName, float totalTime, float sessionBest)
    {
        yield return new WaitForSeconds(1.0f);

        StopAllExercises();
        panelSettings.SetActive(false); 

        if (timeOutcomeTotalText != null) timeOutcomeTotalText.text = Mathf.FloorToInt(totalTime).ToString() + "s";
        if (timeOutcomeSessionBestText != null) timeOutcomeSessionBestText.text = Mathf.FloorToInt(sessionBest).ToString() + "s";
        
        if (timeOutcomeOverallBestText != null) 
        {
            timeOutcomeOverallBestText.text = Mathf.FloorToInt(sessionBest).ToString() + "s";

            if (DatabaseManager.instance != null)
            {
                DatabaseManager.instance.GetOverallBestBalanceTime((overallBest) => 
                {
                    float finalBest = Mathf.Max(overallBest, sessionBest); 
                    timeOutcomeOverallBestText.text = Mathf.FloorToInt(finalBest).ToString() + "s";
                });
            }
        }

        if (timeOutcomeHeaderImage != null && exerciseName == "Balance" && headerBalance != null)
            timeOutcomeHeaderImage.sprite = headerBalance;

        if (panelTimeOutcome != null) panelTimeOutcome.SetActive(true);
    }

    public void OnOutcomeContinueClicked()
    {
        OnBackToSelectionClicked();
    }

    public void OnEarlyExitClicked()
    {
        if (activeExerciseIndex == 0 && curlEvaluator != null) 
            curlEvaluator.ForceStopAssessment();
        else if (activeExerciseIndex == 1 && squatManager != null) 
            squatManager.ForceStopAssessment(); 
        else if (activeExerciseIndex == 2 && balanceManager != null) 
            balanceManager.ForceStopAssessment(); 
        else
            OnBackToSelectionClicked();
    }
}