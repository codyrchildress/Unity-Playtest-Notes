using System;
using UnityEngine;

namespace PlaytestNotes
{
    /// <summary>One id per app launch (per Play Mode session in the Editor), so notes from one sitting group together.</summary>
    internal static class PlaytestSession
    {
        public static string Id { get; private set; } = NewId();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Id = NewId();
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
