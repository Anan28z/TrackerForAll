# TrackerForAll — Change Log

---

## [2026-05-12 13:23] - Feature
- **Goal:** Flip the doctor-patient assignment so patients choose their doctor first, not the other way around
- **Action:** Added `requestedDoctorId` Firestore field. `ProfileManager.cs` — added Doctor Selection UI (dropdown + status text), `FetchAvailableDoctors()`, and `OnAddDoctorClicked()` to write the field. `DoctorDashboardManagerv.cs` — changed `FetchAllPatients()` to only show patients where `requestedDoctorId == currentDoctorId` in the Search & Add section
- **Result:** Doctors no longer see all unassigned patients; only patients who explicitly requested them appear
- **Next Step:** Wire dropdown and Confirm button in Unity Inspector on Profile_Dashboard

---

## [2026-05-12 13:38] - Feature
- **Goal:** Add a logout button that signs out of Firebase and returns to the login screen
- **Action:** Added `using Firebase.Auth;` import and `Logout()` method to `AppManager.cs`. Method calls `StopAllExercises()`, `FirebaseAuth.DefaultInstance.SignOut()`, then `ShowLogin()`
- **Result:** Any button can call `AppManager.Logout()` via OnClick to cleanly log out
- **Next Step:** Wire logout button OnClick → AppManager → Logout() in Unity Inspector

---

## [2026-05-12 14:21] - Fix
- **Goal:** Fix `Missing or insufficient permissions` error when patient opens Profile panel
- **Action:** Changed `FetchAvailableDoctors()` in `ProfileManager.cs` from `db.Collection("Users").GetSnapshotAsync()` (reads entire collection) to `db.Collection("Users").WhereEqualTo("role", "Doctor").GetSnapshotAsync()` (server-side filter). Also removed redundant role check inside the loop. Advised user to update Firestore Security Rules to allow any logged-in user to read documents where `role == "Doctor"`
- **Result:** Query is now targeted; still requires Firestore rules update to fully resolve
- **Next Step:** User must publish updated Firestore rules in Firebase Console

---

## [2026-05-12 14:46] - Feature
- **Goal:** Let doctors navigate to past months to view historical graph data (previously only showed rolling window from today)
- **Action:** Added month dropdown + Confirm button fields to `DoctorPatientDetailManager.cs`. Added `selectedAnchorDate` to replace `System.DateTime.UtcNow` as the time reference. Added `PopulateMonthDropdown()` (fills all 12 months, pre-selects most recent with data), `OnConfirmMonth()` (validates selection, sets anchor to last day of chosen month, unlocks 7/30/1Y buttons), and `SetTimeButtonsInteractable()`. Time buttons now start disabled and only unlock after a valid month is confirmed
- **Result:** Doctors can select any past month and view 7-day, 30-day, or 1-year data anchored to that month
- **Next Step:** Wire dropdown, Confirm button, and status text in Unity Inspector on each Panel_DoctorCurl/Squat/Balance

---

## [2026-05-18] - Fix
- **Goal:** Fix `No document to update` error when patient tries to re-request a doctor after being removed
- **Action:** Changed `UpdateAsync("requestedDoctorId", ...)` to `SetAsync(new Dictionary { requestedDoctorId }, SetOptions.MergeAll)` in `ProfileManager.cs`. `UpdateAsync` fails if the document doesn't exist; `SetAsync` with `MergeAll` creates missing fields without overwriting other existing data
- **Result:** Patient can now request a doctor even if their Firestore document is missing the field
- **Next Step:** —

---

## [2026-05-18] - Fix
- **Goal:** Fix doctor dashboard showing "No patients found" even after a patient successfully requested them
- **Action:** Added `OnRefreshClicked()` public method to `DoctorDashboardManagerv.cs` that calls `FetchAllPatients()`. The dashboard only loads data once on `OnEnable`, so if a patient requests while the doctor panel is already open, the doctor sees stale data until they manually refresh
- **Result:** Doctor can tap Refresh to re-fetch Firestore and see newly requested patients
- **Next Step:** Add a Refresh button to the Doctor Dashboard panel in Unity and wire OnClick → DoctorDashboardManager → OnRefreshClicked()
