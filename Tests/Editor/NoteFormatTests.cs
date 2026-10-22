using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PlaytestNotes.Tests
{
    public sealed class NoteFormatTests
    {
        [Test]
        public void MiniJsonRoundTripsNestedValuesAndEscapes()
        {
            var value = new Dictionary<string, object>
            {
                ["text"] = "quote \" slash \\ line\nbreak tab\t",
                ["number"] = -12.5,
                ["flag"] = true,
                ["nothing"] = null,
                ["list"] = new List<object> { 1, 2.25, "three" },
                ["nested"] = new Dictionary<string, object> { ["empty"] = new Dictionary<string, object>() },
            };

            var read = (Dictionary<string, object>)MiniJson.Read(MiniJson.Write(value));

            Assert.That(read["text"], Is.EqualTo(value["text"]));
            Assert.That(read["number"], Is.EqualTo(-12.5));
            Assert.That(read["flag"], Is.True);
            Assert.That(read["nothing"], Is.Null);
            Assert.That((List<object>)read["list"], Is.EqualTo(new List<object> { 1d, 2.25d, "three" }));
            Assert.That(((Dictionary<string, object>)read["nested"])["empty"], Is.Empty);
        }

        [Test]
        public void MiniJsonRejectsBrokenJson()
        {
            Assert.Throws<FormatException>(() => MiniJson.Read("{\"a\": 1"));
            Assert.Throws<FormatException>(() => MiniJson.Read("{\"a\": 1} trailing"));
        }

        [Test]
        public void NoteDataRoundTripsThroughJson()
        {
            NoteData original = SampleNote();

            NoteData read = NoteData.FromJson(original.ToJson());

            Assert.That(read.Id, Is.EqualTo(original.Id));
            Assert.That(read.TimestampUtc, Is.EqualTo(original.TimestampUtc));
            Assert.That(read.Category, Is.EqualTo(NoteCategory.Polish));
            Assert.That(read.Resolved, Is.True);
            Assert.That(read.SceneName, Is.EqualTo("Level 1"));
            Assert.That(read.ScenePath, Is.EqualTo("Assets/Scenes/Level 1.unity"));
            Assert.That(read.HasTarget, Is.True);
            Assert.That(Vector3.Distance(read.TargetPosition, original.TargetPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(read.TargetRotation, original.TargetRotation), Is.LessThan(0.01f));
            Assert.That(read.HasCamera, Is.True);
            Assert.That(Quaternion.Angle(read.CameraRotation, original.CameraRotation), Is.LessThan(0.01f));
            Assert.That(read.CameraFieldOfView, Is.EqualTo(42f).Within(0.001f));
            Assert.That(read.ScreenWidth, Is.EqualTo(2400));
            Assert.That(read.Metadata["quest.step"], Is.EqualTo("find the key"));
            Assert.That(read.Metadata["player.health"], Is.EqualTo("3"));
        }

        [Test]
        public void NoteDataWithoutTargetOrCameraReadsAsMissing()
        {
            NoteData original = SampleNote();
            original.HasTarget = false;
            original.HasCamera = false;

            NoteData read = NoteData.FromJson(original.ToJson());

            Assert.That(read.HasTarget, Is.False);
            Assert.That(read.HasCamera, Is.False);
        }

        [Test]
        public void WavEncodeDecodeKeepsFormatAndSamples()
        {
            var samples = new float[1600];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Sin(i * 0.05f) * 0.8f;
            }

            byte[] bytes = WavFile.Encode(new RecordedAudio(samples, 16000, 1));
            RecordedAudio read = WavFile.Decode(bytes);

            Assert.That(bytes.Length, Is.EqualTo(44 + (samples.Length * 2)));
            Assert.That(read.Frequency, Is.EqualTo(16000));
            Assert.That(read.Channels, Is.EqualTo(1));
            Assert.That(read.Samples.Length, Is.EqualTo(samples.Length));
            for (int i = 0; i < samples.Length; i++)
            {
                Assert.That(read.Samples[i], Is.EqualTo(samples[i]).Within(1f / 16000f));
            }

            Assert.That(read.Seconds, Is.EqualTo(0.1f).Within(0.0001f));
        }

        internal static NoteData SampleNote()
        {
            var note = new NoteData
            {
                Id = "0123456789abcdef0123456789abcdef",
                TimestampUtc = new DateTime(2026, 10, 21, 15, 30, 12, 345, DateTimeKind.Utc),
                Category = NoteCategory.Polish,
                Resolved = true,
                SceneName = "Level 1",
                ScenePath = "Assets/Scenes/Level 1.unity",
                SessionId = "abcd1234",
                HasTarget = true,
                TargetName = "Player",
                TargetSource = "IPlaytestTarget",
                TargetPosition = new Vector3(1.25f, 0f, -3.5f),
                TargetRotation = Quaternion.Euler(0f, 90f, 0f),
                HasCamera = true,
                CameraName = "Main Camera",
                CameraPosition = new Vector3(1f, 4f, -10f),
                CameraRotation = Quaternion.Euler(20f, 10f, 0f),
                CameraFieldOfView = 42f,
                BuildVersion = "0.3.1",
                Platform = "Android",
                DeviceModel = "Phone",
                ScreenWidth = 2400,
                ScreenHeight = 1080,
                FpsAtCapture = 58.6f,
            };
            note.Metadata["quest.step"] = "find the key";
            note.Metadata["player.health"] = "3";
            return note;
        }
    }
}
