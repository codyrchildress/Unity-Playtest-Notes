using UnityEngine;
using UnityEngine.InputSystem;

namespace PlaytestNotes
{
    /// <summary>
    /// Where notes are saved, how long a recording may run, the input bindings and the Editor transcription setup.
    /// A host without a settings asset uses these defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaytestNotesSettings", menuName = "Playtest Notes/Settings")]
    public sealed class PlaytestNotesSettings : ScriptableObject
    {
        [Header("Storage")]
        [Tooltip("Folder that holds notes/. Empty: <project>/PlaytestNotes in the Editor, <persistentDataPath>/PlaytestNotes in builds. "
            + "A relative path is resolved against the project folder in the Editor and persistentDataPath in builds.")]
        [SerializeField]
        private string saveRoot = string.Empty;

        [Header("Recording")]
        [Tooltip("Recording stops by itself after this many seconds.")]
        [SerializeField, Range(5, 600)]
        private int maxRecordingSeconds = 60;

        [Tooltip("Holding this many fingers on the screen for a second starts a capture. 0 turns the gesture off. "
            + "It is a hold, not a tap, so it never triggers the SRP Rendering Debugger's three-finger double tap.")]
        [SerializeField, Range(0, 5)]
        private int touchCaptureFingers = 3;

        // On a gamepad, Capture is LB + RB held for a second. The SRP Rendering Debugger already opens on L3 + R3
        // in the Editor and development builds, and the hold keeps a game's own shoulder presses from capturing.
        [Header("Input")]
        [SerializeField]
        private InputAction capture = AddChord(CreateButton("Capture", "<Keyboard>/f8"), "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder", "hold(duration=1)");

        [SerializeField]
        private InputAction stop = AddChord(CreateButton("Stop", "<Keyboard>/f8", "<Keyboard>/enter", "<Gamepad>/buttonSouth"), "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder");

        [SerializeField]
        private InputAction submit = CreateButton("Submit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");

        [SerializeField]
        private InputAction rerecord = CreateButton("Re-record", "<Keyboard>/r", "<Gamepad>/buttonWest");

        [SerializeField]
        private InputAction delete = CreateButton("Delete", "<Keyboard>/delete", "<Keyboard>/backspace", "<Gamepad>/buttonEast");

        [SerializeField]
        private InputAction nextCategory = CreateButton("Next Category", "<Keyboard>/tab", "<Gamepad>/rightShoulder");

        [Header("Transcription (Editor only)")]
        [Tooltip("ggml Whisper model used by the whisper.unity transcriber. A relative path starts at the project folder. "
            + "Keep it outside Assets so it never ships in a build.")]
        [SerializeField]
        private string whisperModelPath = "PlaytestNotes/Models/ggml-base.en.bin";

        [Tooltip("Spoken language code such as \"en\", or \"auto\" to detect it.")]
        [SerializeField]
        private string whisperLanguage = "en";

        public string SaveRoot => saveRoot;

        public int MaxRecordingSeconds => maxRecordingSeconds;

        public int TouchCaptureFingers => touchCaptureFingers;

        public InputAction CaptureAction => capture;

        public InputAction StopAction => stop;

        public InputAction SubmitAction => submit;

        public InputAction RerecordAction => rerecord;

        public InputAction DeleteAction => delete;

        public InputAction NextCategoryAction => nextCategory;

        public string WhisperModelPath => whisperModelPath;

        public string WhisperLanguage => whisperLanguage;

        internal void SetActionsEnabled(bool enabled)
        {
            SetEnabled(capture, enabled);
            SetEnabled(stop, enabled);
            SetEnabled(submit, enabled);
            SetEnabled(rerecord, enabled);
            SetEnabled(delete, enabled);
            SetEnabled(nextCategory, enabled);
        }

        private static void SetEnabled(InputAction action, bool enabled)
        {
            if (enabled)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }

        private static InputAction CreateButton(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings)
            {
                action.AddBinding(binding);
            }

            return action;
        }

        // Both buttons down, pressed in either order (modifiersOrder=2 is Unordered).
        private static InputAction AddChord(InputAction action, string first, string second, string interactions = null)
        {
            action.AddCompositeBinding("OneModifier(modifiersOrder=2)", interactions)
                .With("Modifier", first)
                .With("Binding", second);
            return action;
        }
    }
}
