using UnityEngine;

namespace PlaytestNotes
{
    /// <summary>
    /// Implement on a MonoBehaviour (usually the player) to say where a note was taken.
    /// Used when the <see cref="PlaytestNotesHost"/> has no Tracked Target assigned; the first enabled
    /// implementation in the loaded scenes wins. Without either, the main camera is recorded.
    /// </summary>
    public interface IPlaytestTarget
    {
        /// <summary>World-space pose stored with each note.</summary>
        Pose PlaytestPose { get; }
    }
}
