using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace PlaytestNotes.Tests
{
    public sealed class NoteStoreTests
    {
        private string root;
        private NoteStore store;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PlaytestNotesTests_" + Guid.NewGuid().ToString("N"));
            store = new NoteStore(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void SaveWritesOneFolderWithEveryFileAndAnIndex()
        {
            var shot = new Texture2D(4, 4);
            NoteRecord saved;
            try
            {
                saved = store.Save(NoteFormatTests.SampleNote(), shot, new RecordedAudio(new float[3200], 16000, 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shot);
            }

            Assert.That(saved.FolderName, Is.EqualTo("20261021T153012Z_012345"));
            Assert.That(File.Exists(Path.Combine(saved.Folder, NoteStore.ScreenshotFile)), Is.True);
            Assert.That(File.Exists(Path.Combine(saved.Folder, NoteStore.AudioFile)), Is.True);
            Assert.That(File.Exists(Path.Combine(saved.Folder, NoteStore.DataFile)), Is.True);
            Assert.That(File.Exists(Path.Combine(saved.Folder, NoteStore.TranscriptFile)), Is.False);
            Assert.That(saved.Data.AudioSeconds, Is.EqualTo(0.2f).Within(0.0001f));

            string index = File.ReadAllText(store.IndexPath);
            Assert.That(index, Does.Contain("## 1. Polish · Level 1"));
            Assert.That(index, Does.Contain("[20261021T153012Z_012345/shot.png](20261021T153012Z_012345/shot.png)"));
            Assert.That(index, Does.Contain("Player at (1.25, 0, -3.5)"));
            Assert.That(index, Does.Contain("not transcribed yet"));
        }

        [Test]
        public void TranscriptClearsPendingAndAppearsInIndex()
        {
            store.Save(NoteFormatTests.SampleNote(), null, new RecordedAudio(new float[1600], 16000, 1));
            NoteRecord note = store.LoadAll()[0];
            Assert.That(note.IsTranscriptionPending, Is.True);

            store.WriteTranscript(note, "The ledge here is too far.\nMaybe add a rock.");
            store.RebuildIndex();

            NoteRecord reloaded = store.LoadAll()[0];
            Assert.That(reloaded.IsTranscriptionPending, Is.False);
            string index = File.ReadAllText(store.IndexPath);
            Assert.That(index, Does.Contain("  > The ledge here is too far."));
            Assert.That(index, Does.Contain("  > Maybe add a rock."));
        }

        [Test]
        public void NotesLoadOldestFirstAndSkipBrokenFolders()
        {
            NoteData later = NoteFormatTests.SampleNote();
            NoteData earlier = NoteFormatTests.SampleNote();
            earlier.Id = "ffffff" + earlier.Id.Substring(6);
            earlier.TimestampUtc = later.TimestampUtc.AddMinutes(-5);
            store.Save(later, null, null);
            store.Save(earlier, null, null);
            string broken = Path.Combine(store.NotesDirectory, "broken");
            Directory.CreateDirectory(broken);
            File.WriteAllText(Path.Combine(broken, NoteStore.DataFile), "{ not json");

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Skipping .*broken"));
            var notes = store.LoadAll();

            Assert.That(notes.Count, Is.EqualTo(2));
            Assert.That(notes[0].Data.Id, Is.EqualTo(earlier.Id));
            Assert.That(notes[0].Number, Is.EqualTo(1));
            Assert.That(notes[1].Number, Is.EqualTo(2));
            Assert.That(notes[0].HasAudio, Is.False);
            Assert.That(notes[0].IsTranscriptionPending, Is.False);
        }

        [Test]
        public void RelativeRootResolvesUnderTheBaseFolder()
        {
            Assert.That(NoteStore.ResolveRoot(string.Empty), Is.EqualTo(Path.Combine(NoteStore.BaseDirectory, "PlaytestNotes")));
            Assert.That(NoteStore.ResolveRoot("QA/Notes"), Is.EqualTo(Path.GetFullPath(Path.Combine(NoteStore.BaseDirectory, "QA/Notes"))));
            Assert.That(NoteStore.ResolveRoot(root), Is.EqualTo(Path.GetFullPath(root)));
        }
    }
}
