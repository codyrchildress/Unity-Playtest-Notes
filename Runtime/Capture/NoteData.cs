using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PlaytestNotes
{
    /// <summary>
    /// Everything stored in note.json. Read back by the Editor tools; unknown fields are ignored and missing
    /// fields keep their defaults, so older notes stay readable when the format grows.
    /// </summary>
    internal sealed class NoteData
    {
        public const int CurrentFormat = 1;

        private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public string Id { get; set; } = string.Empty;

        public DateTime TimestampUtc { get; set; }

        public NoteCategory Category { get; set; }

        public bool Resolved { get; set; }

        public string SceneName { get; set; } = string.Empty;

        public string ScenePath { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public bool HasTarget { get; set; }

        public string TargetName { get; set; } = string.Empty;

        /// <summary>"TrackedTarget", "IPlaytestTarget" or "MainCamera".</summary>
        public string TargetSource { get; set; } = string.Empty;

        public Vector3 TargetPosition { get; set; }

        public Quaternion TargetRotation { get; set; } = Quaternion.identity;

        public bool HasCamera { get; set; }

        public string CameraName { get; set; } = string.Empty;

        public Vector3 CameraPosition { get; set; }

        public Quaternion CameraRotation { get; set; } = Quaternion.identity;

        public float CameraFieldOfView { get; set; } = 60f;

        public bool CameraOrthographic { get; set; }

        public float CameraOrthographicSize { get; set; } = 5f;

        public string BuildVersion { get; set; } = string.Empty;

        public string UnityVersion { get; set; } = string.Empty;

        public bool DevelopmentBuild { get; set; }

        public string Platform { get; set; } = string.Empty;

        public string DeviceModel { get; set; } = string.Empty;

        public string OperatingSystem { get; set; } = string.Empty;

        public string GraphicsDevice { get; set; } = string.Empty;

        public int ScreenWidth { get; set; }

        public int ScreenHeight { get; set; }

        public float FpsAtCapture { get; set; }

        /// <summary>Length of audio.wav in seconds; 0 when the note has no audio.</summary>
        public float AudioSeconds { get; set; }

        public SortedDictionary<string, string> Metadata { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public string ShortId => Id.Length >= 6 ? Id.Substring(0, 6) : Id;

        public static NoteData FromJson(string json)
        {
            if (!(MiniJson.Read(json) is Dictionary<string, object> root))
            {
                throw new FormatException("note.json is not a JSON object.");
            }

            var data = new NoteData
            {
                Id = GetString(root, "id"),
                Category = Enum.TryParse(GetString(root, "category"), out NoteCategory category) ? category : NoteCategory.Uncategorized,
                Resolved = GetBool(root, "resolved"),
                SessionId = GetString(root, "sessionId"),
                FpsAtCapture = GetFloat(root, "fpsAtCapture"),
                AudioSeconds = GetFloat(root, "audioSeconds"),
            };

            if (DateTime.TryParseExact(GetString(root, "timestampUtc"), TimestampFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime timestamp))
            {
                data.TimestampUtc = timestamp;
            }

            if (root.TryGetValue("scene", out object sceneValue) && sceneValue is Dictionary<string, object> scene)
            {
                data.SceneName = GetString(scene, "name");
                data.ScenePath = GetString(scene, "path");
            }

            if (root.TryGetValue("target", out object targetValue) && targetValue is Dictionary<string, object> target)
            {
                data.HasTarget = true;
                data.TargetName = GetString(target, "name");
                data.TargetSource = GetString(target, "source");
                data.TargetPosition = GetVector3(target, "position");
                data.TargetRotation = GetQuaternion(target, "rotation");
            }

            if (root.TryGetValue("camera", out object cameraValue) && cameraValue is Dictionary<string, object> camera)
            {
                data.HasCamera = true;
                data.CameraName = GetString(camera, "name");
                data.CameraPosition = GetVector3(camera, "position");
                data.CameraRotation = GetQuaternion(camera, "rotation");
                data.CameraFieldOfView = GetFloat(camera, "fieldOfView", 60f);
                data.CameraOrthographic = GetBool(camera, "orthographic");
                data.CameraOrthographicSize = GetFloat(camera, "orthographicSize", 5f);
            }

            if (root.TryGetValue("build", out object buildValue) && buildValue is Dictionary<string, object> build)
            {
                data.BuildVersion = GetString(build, "version");
                data.UnityVersion = GetString(build, "unityVersion");
                data.DevelopmentBuild = GetBool(build, "development");
            }

            if (root.TryGetValue("device", out object deviceValue) && deviceValue is Dictionary<string, object> device)
            {
                data.Platform = GetString(device, "platform");
                data.DeviceModel = GetString(device, "model");
                data.OperatingSystem = GetString(device, "operatingSystem");
                data.GraphicsDevice = GetString(device, "graphicsDevice");
                if (device.TryGetValue("screen", out object screenValue) && screenValue is List<object> screen && screen.Count == 2)
                {
                    data.ScreenWidth = (int)ToDouble(screen[0]);
                    data.ScreenHeight = (int)ToDouble(screen[1]);
                }
            }

            if (root.TryGetValue("metadata", out object metadataValue) && metadataValue is Dictionary<string, object> metadata)
            {
                foreach (KeyValuePair<string, object> pair in metadata)
                {
                    data.Metadata[pair.Key] = pair.Value == null ? string.Empty : Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
                }
            }

            return data;
        }

        public string ToJson()
        {
            var root = new Dictionary<string, object>
            {
                ["format"] = CurrentFormat,
                ["id"] = Id,
                ["timestampUtc"] = TimestampUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                ["category"] = Category.ToString(),
                ["resolved"] = Resolved,
                ["scene"] = new Dictionary<string, object>
                {
                    ["name"] = SceneName,
                    ["path"] = ScenePath,
                },
                ["sessionId"] = SessionId,
                ["target"] = HasTarget
                    ? new Dictionary<string, object>
                    {
                        ["name"] = TargetName,
                        ["source"] = TargetSource,
                        ["position"] = ToList(TargetPosition),
                        ["rotation"] = ToList(TargetRotation),
                        ["euler"] = ToList(TargetRotation.eulerAngles),
                    }
                    : null,
                ["camera"] = HasCamera
                    ? new Dictionary<string, object>
                    {
                        ["name"] = CameraName,
                        ["position"] = ToList(CameraPosition),
                        ["rotation"] = ToList(CameraRotation),
                        ["euler"] = ToList(CameraRotation.eulerAngles),
                        ["fieldOfView"] = Round(CameraFieldOfView),
                        ["orthographic"] = CameraOrthographic,
                        ["orthographicSize"] = Round(CameraOrthographicSize),
                    }
                    : null,
                ["build"] = new Dictionary<string, object>
                {
                    ["version"] = BuildVersion,
                    ["unityVersion"] = UnityVersion,
                    ["development"] = DevelopmentBuild,
                },
                ["device"] = new Dictionary<string, object>
                {
                    ["platform"] = Platform,
                    ["model"] = DeviceModel,
                    ["operatingSystem"] = OperatingSystem,
                    ["graphicsDevice"] = GraphicsDevice,
                    ["screen"] = new List<object> { ScreenWidth, ScreenHeight },
                },
                ["fpsAtCapture"] = Math.Round(FpsAtCapture, 1),
                ["audioSeconds"] = Math.Round(AudioSeconds, 2),
                ["metadata"] = Metadata,
            };

            return MiniJson.Write(root);
        }

        private static double Round(float value)
        {
            return Math.Round(value, 4);
        }

        private static List<object> ToList(Vector3 v)
        {
            return new List<object> { Round(v.x), Round(v.y), Round(v.z) };
        }

        private static List<object> ToList(Quaternion q)
        {
            // Six decimals keep the pose exact enough to frame the Scene view on it.
            return new List<object> { Math.Round(q.x, 6), Math.Round(q.y, 6), Math.Round(q.z, 6), Math.Round(q.w, 6) };
        }

        private static string GetString(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out object value) && value is string text ? text : string.Empty;
        }

        private static bool GetBool(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out object value) && value is bool flag && flag;
        }

        private static float GetFloat(Dictionary<string, object> map, string key, float fallback = 0f)
        {
            return map.TryGetValue(key, out object value) && value is double number ? (float)number : fallback;
        }

        private static Vector3 GetVector3(Dictionary<string, object> map, string key)
        {
            if (map.TryGetValue(key, out object value) && value is List<object> list && list.Count == 3)
            {
                return new Vector3((float)ToDouble(list[0]), (float)ToDouble(list[1]), (float)ToDouble(list[2]));
            }

            return Vector3.zero;
        }

        private static Quaternion GetQuaternion(Dictionary<string, object> map, string key)
        {
            if (map.TryGetValue(key, out object value) && value is List<object> list && list.Count == 4)
            {
                var q = new Quaternion((float)ToDouble(list[0]), (float)ToDouble(list[1]), (float)ToDouble(list[2]), (float)ToDouble(list[3]));
                float length = Mathf.Sqrt((q.x * q.x) + (q.y * q.y) + (q.z * q.z) + (q.w * q.w));
                return length > 0.0001f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : Quaternion.identity;
            }

            return Quaternion.identity;
        }

        private static double ToDouble(object value)
        {
            return value is double number ? number : 0d;
        }
    }
}
