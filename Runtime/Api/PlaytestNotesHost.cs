using System;
using UnityEngine;

namespace PlaytestNotes
{
    /// <summary>
    /// Put one in your first scene (or use the sample prefab). In the Editor and in development builds it adds the
    /// capture controller from the PlaytestNotes.Capture assembly. In release builds that assembly is compiled out
    /// and this component does nothing, so scenes and game code that reference it still build.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Playtest Notes/Playtest Notes Host")]
    public sealed class PlaytestNotesHost : MonoBehaviour
    {
        [Tooltip("Settings asset. Empty uses the defaults.")]
        [SerializeField]
        private PlaytestNotesSettings settings;

        [Tooltip("Transform whose pose is stored with each note. Empty: the first enabled IPlaytestTarget in the loaded scenes, then the main camera.")]
        [SerializeField]
        private Transform trackedTarget;

        [Tooltip("Keep this object across scene loads. The host must be a root object.")]
        [SerializeField]
        private bool persistAcrossScenes = true;

        private static PlaytestNotesHost active;

        private ICaptureController controller;

        /// <summary>True when the capture code is compiled into this build.</summary>
        public static bool IsAvailable => ControllerType != null;

        public PlaytestNotesSettings Settings => settings;

        public Transform TrackedTarget
        {
            get => trackedTarget;
            set => trackedTarget = value;
        }

        /// <summary>True from the moment a capture starts until the note is submitted or deleted.</summary>
        public bool IsCapturing => controller != null && controller.IsCapturing;

        // Set by PlaytestNotes.Capture before the first scene loads; stays null when that assembly is compiled out.
        internal static Type ControllerType { get; set; }

        /// <summary>Starts a capture, as the Capture binding does. Call it from a debug menu button, for example.</summary>
        public void Capture()
        {
            controller?.BeginCapture();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
        }

        private void Awake()
        {
            if (ControllerType == null)
            {
                return;
            }

            if (active != null && active != this)
            {
                Destroy(this);
                return;
            }

            active = this;
            if (persistAcrossScenes)
            {
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                else
                {
                    Debug.LogWarning("[Playtest Notes] Persist Across Scenes needs the host to be a root object; it will unload with its scene.", this);
                }
            }

            controller = (ICaptureController)gameObject.AddComponent(ControllerType);
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }
    }
}
