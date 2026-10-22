using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace PlaytestNotes
{
    /// <summary>
    /// Asks for the microphone on Android and iOS the first time a note is captured. Elsewhere it is always granted.
    /// </summary>
    internal static class MicrophonePermission
    {
        public static bool IsGranted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#elif UNITY_IOS && !UNITY_EDITOR
                return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
                return true;
#endif
            }
        }

        /// <summary>Shows the system prompt if needed and waits for an answer. Check <see cref="IsGranted"/> afterwards.</summary>
        public static IEnumerator Request()
        {
            if (IsGranted)
            {
                yield break;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            bool answered = false;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered = true;
            callbacks.PermissionDenied += _ => answered = true;
            callbacks.PermissionRequestDismissed += _ => answered = true;
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
            while (!answered)
            {
                yield return null;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#endif
        }
    }
}
