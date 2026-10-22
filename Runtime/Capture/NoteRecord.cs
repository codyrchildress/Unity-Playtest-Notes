namespace PlaytestNotes
{
    /// <summary>A saved note: its data plus what its folder holds.</summary>
    internal sealed class NoteRecord
    {
        public NoteRecord(string folder, NoteData data, bool hasScreenshot, bool hasAudio, string transcript)
        {
            Folder = folder;
            Data = data;
            HasScreenshot = hasScreenshot;
            HasAudio = hasAudio;
            Transcript = transcript;
        }

        /// <summary>Absolute path of the note's folder.</summary>
        public string Folder { get; }

        public string FolderName => System.IO.Path.GetFileName(Folder);

        public NoteData Data { get; }

        public bool HasScreenshot { get; }

        public bool HasAudio { get; }

        /// <summary>Contents of transcript.txt, or null when it doesn't exist yet.</summary>
        public string Transcript { get; set; }

        /// <summary>1-based position in time order, the number used in index.md.</summary>
        public int Number { get; set; }

        public bool IsTranscriptionPending => HasAudio && Transcript == null;

        public string ScreenshotPath => System.IO.Path.Combine(Folder, NoteStore.ScreenshotFile);

        public string AudioPath => System.IO.Path.Combine(Folder, NoteStore.AudioFile);
    }
}
