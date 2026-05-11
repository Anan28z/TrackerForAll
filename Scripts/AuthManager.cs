using System.Collections;
using System.Collections.Generic; 
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore; 
using Firebase.Extensions;
using TMPro;
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    [Header("Firebase")]
    public DependencyStatus dependencyStatus;
    public FirebaseAuth auth;
    public FirebaseUser User;

    [Header("Login")]
    public TMP_InputField emailLoginField;
    public TMP_InputField passwordLoginField;
    public TMP_Text warningLoginText;

    [Header("Register")]
    public TMP_Dropdown roleDropdown; 
    public TMP_InputField usernameRegisterField; 
    public TMP_InputField emailRegisterField;
    public TMP_InputField passwordRegisterField;
    public TMP_InputField passwordRegisterVerifyField;
    public TMP_Text warningRegisterText;

    void Awake()
    {
        Debug.Log("Initializing Firebase...");
        InitializeFirebase();
    }

    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync()
        .ContinueWithOnMainThread(task =>
        {
            dependencyStatus = task.Result;

            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;

                if (auth == null)
                    Debug.LogError("FirebaseAuth DefaultInstance is NULL");
                else
                    Debug.Log("Firebase Ready");
            }
            else
            {
                Debug.LogError("Firebase NOT Ready : " + dependencyStatus);
            }
        });
    }

    public void LoginButton()
    {
        Debug.Log("Login Button Clicked");
        if (auth == null) return;
        if (emailLoginField == null || passwordLoginField == null) return;

        StartCoroutine(Login(emailLoginField.text, passwordLoginField.text));
    }

    public void RegisterButton()
    {
        Debug.Log("Register Button Clicked");
        if (auth == null) return;

        if (usernameRegisterField == null ||
            emailRegisterField == null ||
            passwordRegisterField == null ||
            passwordRegisterVerifyField == null ||
            roleDropdown == null) 
        {
            Debug.LogError("Register Fields NOT assigned in Inspector");
            return;
        }

        string selectedRole = roleDropdown.options[roleDropdown.value].text;

        StartCoroutine(Register(
            usernameRegisterField.text,
            emailRegisterField.text,
            passwordRegisterField.text,
            selectedRole 
        ));
    }

    private IEnumerator Login(string _email, string _password)
    {
        Debug.Log("Attempting Login...");

        var loginTask = auth.SignInWithEmailAndPasswordAsync(_email, _password);
        yield return new WaitUntil(() => loginTask.IsCompleted);

        if (loginTask.Exception != null)
        {
            Debug.LogError("Login Failed: " + loginTask.Exception);
            if (warningLoginText != null) warningLoginText.text = "Login Failed";
        }
        else
        {
            User = loginTask.Result.User;
            Debug.Log("Login Success — checking role...");

            if (warningLoginText != null) warningLoginText.text = "";

            // --- FIXED: Look up this user's role in Firestore before routing ---
            Task<DocumentSnapshot> roleTask = null;

            try
            {
                FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
                roleTask = db.Collection("Users").Document(User.UserId).GetSnapshotAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to start role fetch: " + e.Message);
            }

            if (roleTask != null)
            {
                yield return new WaitUntil(() => roleTask.IsCompleted);

                if (roleTask.Exception != null)
                {
                    Debug.LogError("Role fetch failed: " + roleTask.Exception);
                    // Fallback: send to main menu if we can't determine role
                    if (AppManager.instance != null) AppManager.instance.ShowMainMenu();
                }
                else
                {
                    DocumentSnapshot doc = roleTask.Result;
                    string role = "Patient"; // Safe default

                    if (doc.Exists && doc.ContainsField("role"))
                        role = doc.GetValue<string>("role");

                    Debug.Log($"User role is: {role}");

                    if (AppManager.instance != null)
                    {
                        if (role == "Doctor")
                            AppManager.instance.ShowDoctorDashboard();
                        else
                            AppManager.instance.ShowMainMenu();
                    }
                }
            }
            else
            {
                // roleTask never started — just send to main menu
                if (AppManager.instance != null) AppManager.instance.ShowMainMenu();
            }
        }
    }

    private IEnumerator Register(string _username, string _email, string _password, string _role)
    {
        Debug.Log("Attempting Register...");

        if (_password != passwordRegisterVerifyField.text)
        {
            if (warningRegisterText != null) warningRegisterText.text = "Passwords Do Not Match";
            yield break;
        }

        var registerTask = auth.CreateUserWithEmailAndPasswordAsync(_email, _password);
        yield return new WaitUntil(() => registerTask.IsCompleted);

        if (registerTask.Exception != null)
        {
            Debug.LogError("Register Failed: " + registerTask.Exception);
            if (warningRegisterText != null) warningRegisterText.text = "Register Failed";
        }
        else
        {
            User = registerTask.Result.User;

            if (User != null)
            {
                UserProfile profile = new UserProfile { DisplayName = _username };
                var profileTask = User.UpdateUserProfileAsync(profile);
                yield return new WaitUntil(() => profileTask.IsCompleted);

                // Separated the Task creation from the yield return (compiler requirement)
                Task firestoreTask = null;
                
                try
                {
                    FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
                    DocumentReference docRef = db.Collection("Users").Document(User.UserId);
                    
                    Dictionary<string, object> userData = new Dictionary<string, object>
                    {
                        { "username", _username },
                        { "role", _role }
                    };

                    firestoreTask = docRef.SetAsync(userData);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Failed to trigger Firestore save: " + e.Message);
                }

                if (firestoreTask != null)
                {
                    yield return new WaitUntil(() => firestoreTask.IsCompleted);

                    if (firestoreTask.Exception == null)
                        Debug.Log($"Successfully saved {_role} profile to database!");
                    else
                        Debug.LogError("Failed to save role to Firestore: " + firestoreTask.Exception);
                }
            }

            Debug.Log("Register Success");

            if (warningRegisterText != null)
                warningRegisterText.text = "Registration Successful! Please Login.";

            auth.SignOut();

            if (AppManager.instance != null)
                AppManager.instance.ShowLogin();
        }
    }
}