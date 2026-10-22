namespace PlaytestNotes
{
    /// <summary>
    /// Implemented by the controller in PlaytestNotes.Capture, so the host can reach it without referencing that assembly.
    /// </summary>
    internal interface ICaptureController
    {
        bool IsCapturing { get; }

        void BeginCapture();
    }
}
