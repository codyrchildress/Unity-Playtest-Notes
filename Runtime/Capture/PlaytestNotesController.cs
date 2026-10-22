using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PlaytestNotes
{
    /// <summary>
    /// The capture flow: Capture pauses the game and grabs a clean frame, then the microphone records until Stop;
    /// the tester picks a category and submits, re-records or deletes; the game resumes either way.
    /// Added to the host at runtime by <see cref="PlaytestNotesHost"/>.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class PlaytestNotesController : MonoBehaviour, ICaptureController
    {
        // A hold rather than a tap, so the SRP Rendering Debugger's three-finger double tap never starts a capture.
        private const float TouchHoldSeconds = 1f;

        private static readonly int CategoryCount = Enum.GetValues(typeof(NoteCategory)).Length;

        private readonly MicrophoneRecorder recorder = new MicrophoneRecorder();
        private readonly PauseScope pause = new PauseScope();
        private readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

        private PlaytestNotesHost host;
        private PlaytestNotesSettings settings;
        private bool ownsSettings;
        private NoteStore store;
        private PlaytestOverlay overlay;
        private State state;
        private NoteData note;
        private Texture2D screenshot;
        private RecordedAudio audio;
        private bool microphoneUsable;
        private string status;
        private float smoothedDeltaTime = 1f / 60f;
        private float touchHoldStart = -1f;
        private bool touchHoldFired;

        private enum State
        {
            Idle,
            Capturing,
            Recording,
            Review,
        }

        public bool IsCapturing => state != State.Idle;

        public void BeginCapture()
        {
            if (state != State.Idle || !isActiveAndEnabled)
            {
                return;
            }

            state = State.Capturing;
            note = NoteContext.Collect(host, 1f / smoothedDeltaTime);
            pause.Begin();
            StartCoroutine(Capture());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlaytestNotesHost.ControllerType = typeof(PlaytestNotesController);
        }

        private void Awake()
        {
            host = GetComponent<PlaytestNotesHost>();
            settings = host.Settings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PlaytestNotesSettings>();
                ownsSettings = true;
            }

            store = new NoteStore(NoteStore.ResolveRoot(settings.SaveRoot));
            overlay = PlaytestOverlay.Create(transform, settings);
            overlay.StopPressed += StopRecording;
            overlay.SubmitPressed += Submit;
            overlay.RerecordPressed += Rerecord;
            overlay.DeletePressed += Delete;
            overlay.CategoryPressed += SetCategory;
        }

        private void OnEnable()
        {
            settings.SetActionsEnabled(true);
        }

        private void OnDisable()
        {
            if (state != State.Idle)
            {
                Finish(null);
            }

            settings.SetActionsEnabled(false);
        }

        private void OnDestroy()
        {
            if (ownsSettings)
            {
                Destroy(settings);
            }
        }

        private void Update()
        {
            smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.1f;
            bool touchGesture = TouchGestureStarted();
            switch (state)
            {
                case State.Idle:
                    // Performed, not pressed, so interactions on a binding (the gamepad's one-second hold) count.
                    if (settings.CaptureAction.WasPerformedThisFrame() || touchGesture)
                    {
                        BeginCapture();
                    }

                    break;
                case State.Recording:
                    overlay.SetElapsed(recorder.Elapsed);
                    if (settings.StopAction.WasPressedThisFrame() || recorder.ReachedLimit)
                    {
                        StopRecording();
                    }

                    break;
                case State.Review:
                    if (settings.SubmitAction.WasPressedThisFrame())
                    {
                        Submit();
                    }
                    else if (settings.RerecordAction.WasPressedThisFrame())
                    {
                        Rerecord();
                    }
                    else if (settings.DeleteAction.WasPressedThisFrame())
                    {
                        Delete();
                    }
                    else if (settings.NextCategoryAction.WasPressedThisFrame())
                    {
                        SetCategory((NoteCategory)(((int)note.Category + 1) % CategoryCount));
                    }

                    break;
            }
        }

        // True once per hold, when enough fingers have stayed down for TouchHoldSeconds.
        private bool TouchGestureStarted()
        {
            Touchscreen touchscreen = Touchscreen.current;
            int fingers = settings.TouchCaptureFingers;
            if (touchscreen == null || fingers <= 0)
            {
                return false;
            }

            int count = 0;
            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                TouchControl touch = touches[i];
                if (touch.press.isPressed)
                {
                    count++;
                }
            }

            if (count < fingers)
            {
                touchHoldStart = -1f;
                touchHoldFired = false;
                return false;
            }

            if (touchHoldStart < 0f)
            {
                touchHoldStart = Time.unscaledTime;
            }

            if (touchHoldFired || Time.unscaledTime - touchHoldStart < TouchHoldSeconds)
            {
                return false;
            }

            touchHoldFired = true;
            return true;
        }

        // The frame is grabbed at the end of the frame Capture was pressed in, before the overlay is ever drawn.
        private IEnumerator Capture()
        {
            yield return endOfFrame;
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (!MicrophonePermission.IsGranted)
            {
                overlay.ShowWaiting("Waiting for microphone permission...");
                yield return MicrophonePermission.Request();
            }

            if (!MicrophonePermission.IsGranted)
            {
                microphoneUsable = false;
                status = "Microphone permission denied: this note has the screenshot only.";
                EnterReview();
                yield break;
            }

            microphoneUsable = true;
            StartRecording();
        }

        private void StartRecording()
        {
            audio = null;
            if (!recorder.Start(settings.MaxRecordingSeconds))
            {
                microphoneUsable = false;
                status = "No microphone found: this note has the screenshot only.";
                EnterReview();
                return;
            }

            status = null;
            state = State.Recording;
            overlay.ShowRecording(settings.MaxRecordingSeconds);
        }

        private void StopRecording()
        {
            if (state != State.Recording)
            {
                return;
            }

            audio = recorder.Stop();
            if (audio == null)
            {
                status = "Nothing was recorded.";
            }

            EnterReview();
        }

        private void EnterReview()
        {
            state = State.Review;
            overlay.ShowReview(note.Category, audio, microphoneUsable, status);
        }

        private void Rerecord()
        {
            if (state == State.Review && microphoneUsable)
            {
                StartRecording();
            }
        }

        private void SetCategory(NoteCategory category)
        {
            if (state != State.Review)
            {
                return;
            }

            note.Category = category;
            overlay.SetCategory(category);
        }

        private void Submit()
        {
            if (state != State.Review)
            {
                return;
            }

            string message;
            try
            {
                NoteRecord saved = store.Save(note, screenshot, audio);
                message = "Note saved: " + saved.FolderName;
                Debug.Log("[Playtest Notes] Saved " + saved.Folder);
            }
            catch (IOException e)
            {
                Debug.LogException(e, this);
                message = "Could not save the note: " + e.Message;
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogException(e, this);
                message = "Could not save the note: " + e.Message;
            }

            Finish(message);
        }

        private void Delete()
        {
            if (state == State.Review)
            {
                Finish("Note deleted.");
            }
        }

        private void Finish(string message)
        {
            StopAllCoroutines();
            recorder.Cancel();
            if (screenshot != null)
            {
                Destroy(screenshot);
                screenshot = null;
            }

            audio = null;
            note = null;
            status = null;
            pause.End();
            state = State.Idle;

            // When the host is torn down (scene unload, leaving Play Mode) the overlay may already be gone.
            if (overlay == null)
            {
                return;
            }

            overlay.Hide();
            if (message != null)
            {
                overlay.Toast(message);
            }
        }
    }
}
