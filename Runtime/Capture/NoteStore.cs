using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PlaytestNotes
{
    /// <summary>
    /// Reads and writes the notes/ folder: one folder per note (shot.png, audio.wav, note.json, transcript.txt)
    /// and notes/index.md, which is rebuilt from the folders after every change.
    /// </summary>
    internal sealed class NoteStore
    {
        public const string NotesFolder = "notes";
        public const string ScreenshotFile = "shot.png";
        public const string AudioFile = "audio.wav";
        public const string DataFile = "note.json";
        public const string TranscriptFile = "transcript.txt";
        public const string IndexFile = "index.md";

        public NoteStore(string root)
        {
            Root = root;
            NotesDirectory = Path.Combine(root, NotesFolder);
        }

        public string Root { get; }

        public string NotesDirectory { get; }

        public string IndexPath => Path.Combine(NotesDirectory, IndexFile);

        /// <summary>The project folder in the Editor, persistentDataPath in builds.</summary>
        public static string BaseDirectory => Application.isEditor
            ? Path.GetDirectoryName(Application.dataPath)
            : Application.persistentDataPath;

        public static string ResolveRoot(string configuredRoot)
        {
            return string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(BaseDirectory, "PlaytestNotes")
                : Path.GetFullPath(Path.Combine(BaseDirectory, configuredRoot));
        }

        public static string FolderNameFor(NoteData data)
        {
            return data.TimestampUtc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + "_" + data.ShortId;
        }

        public NoteRecord Save(NoteData data, Texture2D screenshot, RecordedAudio audio)
        {
            string folder = Path.Combine(NotesDirectory, FolderNameFor(data));
            Directory.CreateDirectory(folder);
            if (screenshot != null)
            {
                File.WriteAllBytes(Path.Combine(folder, ScreenshotFile), screenshot.EncodeToPNG());
            }

            data.AudioSeconds = audio != null ? audio.Seconds : 0f;
            if (audio != null)
            {
                File.WriteAllBytes(Path.Combine(folder, AudioFile), WavFile.Encode(audio));
            }

            File.WriteAllText(Path.Combine(folder, DataFile), data.ToJson());
            RebuildIndex();
            return new NoteRecord(folder, data, screenshot != null, audio != null, null);
        }

        /// <summary>All readable notes, oldest first, numbered. Folders with a broken note.json are skipped with a warning.</summary>
        public List<NoteRecord> LoadAll()
        {
            var notes = new List<NoteRecord>();
            if (!Directory.Exists(NotesDirectory))
            {
                return notes;
            }

            foreach (string folder in Directory.GetDirectories(NotesDirectory))
            {
                string dataPath = Path.Combine(folder, DataFile);
                if (!File.Exists(dataPath))
                {
                    continue;
                }

                NoteData data;
                try
                {
                    data = NoteData.FromJson(File.ReadAllText(dataPath));
                }
                catch (FormatException e)
                {
                    Debug.LogWarning("[Playtest Notes] Skipping " + folder + ": " + e.Message);
                    continue;
                }

                string transcriptPath = Path.Combine(folder, TranscriptFile);
                string transcript = File.Exists(transcriptPath) ? File.ReadAllText(transcriptPath) : null;
                notes.Add(new NoteRecord(
                    folder,
                    data,
                    File.Exists(Path.Combine(folder, ScreenshotFile)),
                    File.Exists(Path.Combine(folder, AudioFile)),
                    transcript));
            }

            notes.Sort((a, b) =>
            {
                int byTime = a.Data.TimestampUtc.CompareTo(b.Data.TimestampUtc);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.FolderName, b.FolderName);
            });

            for (int i = 0; i < notes.Count; i++)
            {
                notes[i].Number = i + 1;
            }

            return notes;
        }

        public void WriteData(NoteRecord note)
        {
            File.WriteAllText(Path.Combine(note.Folder, DataFile), note.Data.ToJson());
        }

        public void WriteTranscript(NoteRecord note, string transcript)
        {
            note.Transcript = transcript ?? string.Empty;
            File.WriteAllText(Path.Combine(note.Folder, TranscriptFile), note.Transcript);
        }

        public void Delete(NoteRecord note)
        {
            Directory.Delete(note.Folder, true);
        }

        public void RebuildIndex()
        {
            Directory.CreateDirectory(NotesDirectory);
            File.WriteAllText(IndexPath, NoteIndexWriter.Build(LoadAll(), DateTime.UtcNow));
        }
    }
}
