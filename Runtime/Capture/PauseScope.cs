using UnityEngine;

namespace PlaytestNotes
{
    /// <summary>Pauses time, audio and frees the cursor for the overlay, then puts all three back as they were.</summary>
    internal sealed class PauseScope
    {
        private float timeScale;
        private bool audioPaused;
        private CursorLockMode cursorLockMode;
        private bool cursorVisible;

        public bool IsActive { get; private set; }

        public void Begin()
        {
            if (IsActive)
            {
                return;
            }

            timeScale = Time.timeScale;
            audioPaused = AudioListener.pause;
            cursorLockMode = Cursor.lockState;
            cursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsActive = true;
        }

        public void End()
        {
            if (!IsActive)
            {
                return;
            }

            Time.timeScale = timeScale;
            AudioListener.pause = audioPaused;
            Cursor.lockState = cursorLockMode;
            Cursor.visible = cursorVisible;
            IsActive = false;
        }
    }
}
