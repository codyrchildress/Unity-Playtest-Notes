using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlaytestNotes
{
    /// <summary>
    /// Collects what a note records about the moment of capture: scene, target and camera pose, build, device,
    /// FPS and the metadata from every <see cref="IPlaytestMetadataProvider"/> in the loaded scenes.
    /// </summary>
    internal static class NoteContext
    {
        private static readonly List<GameObject> Roots = new List<GameObject>();
        private static readonly List<MonoBehaviour> Behaviours = new List<MonoBehaviour>();

        public static NoteData Collect(PlaytestNotesHost host, float fps)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            var data = new NoteData
            {
                Id = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow,
                SceneName = activeScene.name,
                ScenePath = activeScene.path,
                SessionId = PlaytestSession.Id,
                BuildVersion = Application.version,
                UnityVersion = Application.unityVersion,
                DevelopmentBuild = Debug.isDebugBuild,
                Platform = Application.platform.ToString(),
                DeviceModel = SystemInfo.deviceModel,
                OperatingSystem = SystemInfo.operatingSystem,
                GraphicsDevice = SystemInfo.graphicsDeviceName,
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
                FpsAtCapture = fps,
            };

            List<MonoBehaviour> behaviours = FindBehaviours(host.gameObject.scene);
            Camera camera = FindCamera();
            RecordTarget(data, host, behaviours, camera);
            if (camera != null)
            {
                Transform cameraTransform = camera.transform;
                data.HasCamera = true;
                data.CameraName = camera.name;
                data.CameraPosition = cameraTransform.position;
                data.CameraRotation = cameraTransform.rotation;
                data.CameraFieldOfView = camera.fieldOfView;
                data.CameraOrthographic = camera.orthographic;
                data.CameraOrthographicSize = camera.orthographicSize;
            }

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPlaytestMetadataProvider provider)
                {
                    try
                    {
                        provider.CollectPlaytestMetadata(data.Metadata);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, behaviour);
                    }
                }
            }

            Behaviours.Clear();
            return data;
        }

        private static void RecordTarget(NoteData data, PlaytestNotesHost host, List<MonoBehaviour> behaviours, Camera camera)
        {
            if (host.TrackedTarget != null)
            {
                Record(data, host.TrackedTarget.name, "TrackedTarget", host.TrackedTarget.position, host.TrackedTarget.rotation);
                return;
            }

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPlaytestTarget target)
                {
                    Pose pose = target.PlaytestPose;
                    Record(data, behaviour.name, "IPlaytestTarget", pose.position, pose.rotation);
                    return;
                }
            }

            if (camera != null)
            {
                Record(data, camera.name, "MainCamera", camera.transform.position, camera.transform.rotation);
            }
        }

        private static void Record(NoteData data, string name, string source, Vector3 position, Quaternion rotation)
        {
            data.HasTarget = true;
            data.TargetName = name;
            data.TargetSource = source;
            data.TargetPosition = position;
            data.TargetRotation = rotation;
        }

        private static Camera FindCamera()
        {
            if (Camera.main != null)
            {
                return Camera.main;
            }

            return Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
        }

        // Enabled behaviours in every loaded scene, plus the host's own scene (DontDestroyOnLoad isn't in the scene list).
        private static List<MonoBehaviour> FindBehaviours(Scene hostScene)
        {
            Behaviours.Clear();
            bool hostSceneListed = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                hostSceneListed |= scene == hostScene;
                AddBehaviours(scene);
            }

            if (!hostSceneListed)
            {
                AddBehaviours(hostScene);
            }

            return Behaviours;
        }

        private static void AddBehaviours(Scene scene)
        {
            if (!scene.isLoaded)
            {
                return;
            }

            scene.GetRootGameObjects(Roots);
            var found = new List<MonoBehaviour>();
            foreach (GameObject root in Roots)
            {
                root.GetComponentsInChildren(false, found);
                foreach (MonoBehaviour behaviour in found)
                {
                    if (behaviour != null && behaviour.isActiveAndEnabled)
                    {
                        Behaviours.Add(behaviour);
                    }
                }
            }

            Roots.Clear();
        }
    }
}
