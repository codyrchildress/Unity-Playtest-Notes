using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PlaytestNotes
{
    /// <summary>
    /// Builds notes/index.md: one entry per note, in time order, written to be handed to Claude (or a person) as-is.
    /// Paths are relative to the index so the notes folder can be moved or zipped whole.
    /// </summary>
    internal static class NoteIndexWriter
    {
        public static string Build(IReadOnlyList<NoteRecord> notes, DateTime generatedUtc)
        {
            int resolved = 0;
            foreach (NoteRecord note in notes)
            {
                if (note.Data.Resolved)
                {
                    resolved++;
                }
            }

            var builder = new StringBuilder();
            builder.Append("# Playtest notes\n\n");
            builder.Append(notes.Count).Append(notes.Count == 1 ? " note" : " notes")
                .Append(" (").Append(notes.Count - resolved).Append(" open, ").Append(resolved).Append(" resolved), generated ")
                .Append(generatedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" UTC.\n\n");
            builder.Append("Each entry is one piece of feedback captured while playing: a screenshot taken the moment the tester pressed Capture, ")
                .Append("where they were, and what they said. Transcripts are speech-to-text and may mishear words. ")
                .Append("Each note.json has the full capture: camera pose, device, build, FPS and game metadata. ")
                .Append("Paths are relative to this file. Positions are Unity world space (metres, Y up).\n");

            foreach (NoteRecord note in notes)
            {
                AppendNote(builder, note);
            }

            return builder.ToString();
        }

        private static void AppendNote(StringBuilder builder, NoteRecord note)
        {
            NoteData data = note.Data;
            string folder = note.FolderName;
            builder.Append("\n## ").Append(note.Number).Append(". ").Append(data.Category)
                .Append(" · ").Append(string.IsNullOrEmpty(data.SceneName) ? "(no scene)" : data.SceneName)
                .Append(" · ").Append(data.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(" UTC")
                .Append(data.Resolved ? " · resolved" : string.Empty).Append("\n\n");

            builder.Append("- Screenshot: ");
            if (note.HasScreenshot)
            {
                builder.Append('[').Append(folder).Append('/').Append(NoteStore.ScreenshotFile).Append("](")
                    .Append(folder).Append('/').Append(NoteStore.ScreenshotFile).Append(")\n");
            }
            else
            {
                builder.Append("none\n");
            }

            builder.Append("- Category: ").Append(data.Category).Append('\n');
            builder.Append("- Scene: ").Append(string.IsNullOrEmpty(data.SceneName) ? "(none)" : data.SceneName);
            if (!string.IsNullOrEmpty(data.ScenePath))
            {
                builder.Append(" (`").Append(data.ScenePath).Append("`)");
            }

            builder.Append('\n');
            builder.Append("- Position: ");
            if (data.HasTarget)
            {
                builder.Append(Escape(data.TargetName)).Append(" at ").Append(Format(data.TargetPosition))
                    .Append(", facing ").Append(Format(data.TargetRotation.eulerAngles.y)).Append("° yaw");
            }
            else
            {
                builder.Append("unknown");
            }

            builder.Append('\n');
            if (data.HasCamera)
            {
                builder.Append("- Camera: ").Append(Format(data.CameraPosition)).Append(", rotation ")
                    .Append(Format(data.CameraRotation.eulerAngles)).Append(", ")
                    .Append(data.CameraOrthographic ? "orthographic size " + Format(data.CameraOrthographicSize) : "FOV " + Format(data.CameraFieldOfView))
                    .Append('\n');
            }

            builder.Append("- Build: ").Append(string.IsNullOrEmpty(data.BuildVersion) ? "?" : data.BuildVersion)
                .Append(data.DevelopmentBuild ? " (development)" : string.Empty)
                .Append(" · ").Append(data.Platform).Append(", ").Append(Escape(data.DeviceModel))
                .Append(" · ").Append(Format(data.FpsAtCapture)).Append(" fps\n");

            if (data.Metadata.Count > 0)
            {
                builder.Append("- Metadata:");
                bool first = true;
                foreach (KeyValuePair<string, string> pair in data.Metadata)
                {
                    builder.Append(first ? " " : ", ").Append('`').Append(Escape(pair.Key)).Append("` = ").Append(Escape(pair.Value));
                    first = false;
                }

                builder.Append('\n');
            }

            builder.Append("- Data: [note.json](").Append(folder).Append('/').Append(NoteStore.DataFile).Append(")\n");
            builder.Append("- Transcript:");
            if (!note.HasAudio)
            {
                builder.Append(" none (no audio was recorded)\n");
            }
            else if (note.Transcript == null)
            {
                builder.Append(" not transcribed yet; audio in [")
                    .Append(folder).Append('/').Append(NoteStore.AudioFile).Append("](")
                    .Append(folder).Append('/').Append(NoteStore.AudioFile).Append(")\n");
            }
            else if (note.Transcript.Trim().Length == 0)
            {
                builder.Append(" no speech detected\n");
            }
            else
            {
                builder.Append('\n');
                foreach (string line in note.Transcript.Trim().Split('\n'))
                {
                    builder.Append("  > ").Append(line.TrimEnd('\r')).Append('\n');
                }
            }
        }

        private static string Escape(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('\n', ' ').Replace('\r', ' ');
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Format(Vector3 v)
        {
            return "(" + Format(v.x) + ", " + Format(v.y) + ", " + Format(v.z) + ")";
        }
    }
}
