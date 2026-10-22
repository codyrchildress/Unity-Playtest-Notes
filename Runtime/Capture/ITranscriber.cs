using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlaytestNotes
{
    /// <summary>
    /// Turns a note's audio.wav into text. "Playtest Notes > Transcribe Pending Notes" creates the first
    /// implementation it finds (by full type name; it needs a public parameterless constructor), runs it over
    /// every note that has audio but no transcript.txt, then disposes it.
    /// </summary>
    public interface ITranscriber : IDisposable
    {
        /// <summary>Shown in the progress bar and log.</summary>
        string DisplayName { get; }

        /// <summary>Returns the transcript; an empty string means no speech. Throw on failure; the run stops at the first error.</summary>
        Task<string> TranscribeAsync(string wavPath, CancellationToken cancellationToken);
    }
}
