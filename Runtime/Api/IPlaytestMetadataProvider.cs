using System.Collections.Generic;

namespace PlaytestNotes
{
    /// <summary>
    /// Implement on any MonoBehaviour to add game state to every note (quest step, health, checkpoint...).
    /// All enabled implementations in the loaded scenes are asked once per capture, on the main thread,
    /// while the captured frame is still current.
    /// </summary>
    public interface IPlaytestMetadataProvider
    {
        /// <summary>
        /// Adds entries to the note's metadata. Prefix keys with your system's name ("quest.step")
        /// so providers don't overwrite each other. An exception is logged and the note is still saved.
        /// </summary>
        void CollectPlaytestMetadata(IDictionary<string, string> metadata);
    }
}
