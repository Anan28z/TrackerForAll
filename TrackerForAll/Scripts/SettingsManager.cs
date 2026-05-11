using System.Collections.Generic;
using System.Linq;
using TMPro;
using TrackerPro.Unity;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public TMP_Dropdown cameraOptionDropdown;
    public TMP_Dropdown resulotionDropdown;
    [SerializeField]
    private Solution solution;
    [SerializeField]
    private GameObject settingsPanel;

    public void OpenSettingsPanel()
    {
        settingsPanel.SetActive(true);
        InitializeSource();
        InitializeResolution();
    }

    public void InitializeSource()
    {
        cameraOptionDropdown.ClearOptions();
        cameraOptionDropdown.onValueChanged.RemoveAllListeners();

        var imageSource = ImageSourceProvider.ImageSource;
        if (imageSource == null) return;

        var sourceNames = imageSource.sourceCandidateNames;
        if (sourceNames == null)
        {
            cameraOptionDropdown.enabled = false;
            return;
        }

        var options = new List<string>(sourceNames);
        cameraOptionDropdown.AddOptions(options);

        // This ensures the dropdown matches the camera set by AppManager
        var currentSourceName = imageSource.sourceName;
        int defaultValue = options.FindIndex(option => option == currentSourceName);
        if (defaultValue != -1)
        {
            cameraOptionDropdown.value = defaultValue;
        }

        cameraOptionDropdown.onValueChanged.AddListener(delegate
        {
            imageSource.SelectSource(cameraOptionDropdown.value);
            StartCoroutine(RestartSolution(true));
        });
    }

    private void InitializeResolution()
    {
        resulotionDropdown.ClearOptions();
        resulotionDropdown.onValueChanged.RemoveAllListeners();

        var imageSource = ImageSourceProvider.ImageSource;
        var resolutions = imageSource.availableResolutions;
        if (resolutions == null) return;

        var options = resolutions.Select(resolution => resolution.ToString()).ToList();
        resulotionDropdown.AddOptions(options);

        var currentResolutionStr = imageSource.resolution.ToString();
        var defaultValue = options.FindIndex(option => option == currentResolutionStr);

        if (defaultValue >= 0)
        {
            resulotionDropdown.value = defaultValue;
        }

        resulotionDropdown.onValueChanged.AddListener(delegate
        {
            imageSource.SelectResolution(resulotionDropdown.value);
            StartCoroutine(RestartSolution(true));
        });
    }

    public System.Collections.IEnumerator RestartSolution(bool forceRestart = false)
    {
        solution.Pause();
        yield return new WaitForSecondsRealtime(1);
        if (forceRestart)
        {
            Debug.Log("Restart Solution");
            solution.Play();
        }
        else
        {
            solution.Resume();
        }
    }
}