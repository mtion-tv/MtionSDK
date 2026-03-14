using mtion.room.sdk.compiled;
using mtion.utility;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace mtion.room.sdk
{
    public static class ThumbnailGenerator
    {
        private const int ThumbnailResWidth = 1024;
        private const int ThumbnailResHeight = 1024;
        private static readonly Vector3 TemporaryCameraDirection = new Vector3(-0.9f, 0.6f, -0.9f).normalized;

        private static string ThumbnailCreationTempPrefabPath = SDKUtil.GetBaseAssetDirectory() + "ThumbnailAssetTEMP.prefab";
        private static string ThumbnailCreationScenePath
        {
            get
            {
                TryGetThumbnailCreationScenePath(out string scenePath);
                return scenePath;
            }
        }

        public static bool HasThumbnailScene()
        {
            return TryGetThumbnailCreationScenePath(out _);
        }

        public static bool TryGetThumbnailCreationScenePath(out string scenePath)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene MTIONSDKThumbnailScene");
            foreach (string sceneGuid in sceneGuids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                if (Path.GetFileNameWithoutExtension(candidatePath) == "MTIONSDKThumbnailScene")
                {
                    scenePath = candidatePath;
                    return true;
                }
            }

            scenePath = null;
            return false;
        }

        public static bool CanGenerateThumbnail(MTIONSDKDescriptorSceneBase sceneBase, out string diagnostic)
        {
            if (TryResolveThumbnailCamera(sceneBase, false, out _, out _, out diagnostic))
            {
                return true;
            }

            if (CanCreateTemporaryThumbnailCamera(sceneBase, out diagnostic))
            {
                return true;
            }

            return false;
        }

        public static bool TryGenerateSceneThumbnail(MTIONSDKDescriptorSceneBase sceneBase, string directory, string filename, out string diagnostic)
        {
            GameObject temporaryCameraObject = null;
            if (!TryResolveThumbnailCamera(sceneBase, true, out Camera camera, out temporaryCameraObject, out diagnostic))
            {
                return false;
            }

            try
            {
                bool success = TakeSnapshotOfAssetInCurrentScene(camera, directory, filename);
                if (!success)
                {
                    diagnostic = $"Thumbnail generation failed while rendering camera '{camera.name}'.";
                }

                return success;
            }
            finally
            {
                if (temporaryCameraObject != null)
                {
                    Object.DestroyImmediate(temporaryCameraObject);
                }
            }
        }

        public static bool TryResolveThumbnailCamera(MTIONSDKDescriptorSceneBase sceneBase, out Camera camera, out string diagnostic)
        {
            return TryResolveThumbnailCamera(sceneBase, false, out camera, out _, out diagnostic);
        }

        private static bool TryResolveThumbnailCamera(MTIONSDKDescriptorSceneBase sceneBase, bool allowTemporaryCamera, out Camera camera, out GameObject temporaryCameraObject, out string diagnostic)
        {
            if (sceneBase == null)
            {
                camera = null;
                temporaryCameraObject = null;
                diagnostic = "Scene descriptor is missing.";
                return false;
            }

            Scene scene = sceneBase.gameObject.scene;
            List<Camera> descriptorCameras = sceneBase.GetComponentsInChildren<Camera>(true)
                .Where(candidate => candidate != null && candidate.gameObject.scene == scene)
                .ToList();
            camera = SelectPreferredCamera(descriptorCameras);
            if (camera != null)
            {
                temporaryCameraObject = null;
                diagnostic = $"Using descriptor camera '{camera.name}'.";
                return true;
            }

            List<Camera> sceneCameras = GetSceneCameras(scene);
            camera = SelectPreferredCamera(sceneCameras);
            if (camera != null)
            {
                temporaryCameraObject = null;
                diagnostic = $"Using scene camera '{camera.name}'.";
                return true;
            }

            List<Camera> loadedSceneCameras = GetLoadedSceneCameras();
            camera = SelectPreferredCamera(loadedSceneCameras);
            if (camera != null)
            {
                temporaryCameraObject = null;
                diagnostic = $"Using fallback loaded-scene camera '{camera.name}'.";
                return true;
            }

            if (allowTemporaryCamera && TryCreateTemporaryThumbnailCamera(sceneBase, out camera, out temporaryCameraObject, out diagnostic))
            {
                return true;
            }

            temporaryCameraObject = null;
            diagnostic = $"No enabled active camera found in scene '{scene.path}'. Descriptor cameras: {descriptorCameras.Count}, scene cameras: {sceneCameras.Count}, loaded scene cameras: {loadedSceneCameras.Count}.";
            return false;
        }

        public static bool TakeSnapshotOfAssetInCurrentScene(Camera camera, string directory, string filename)
        {
            if (camera == null)
            {
                Debug.LogWarning("Camera could not be found for snapshot image");
                return false;
            }

            CameraClearFlags originalClearFlags = camera.clearFlags;
            Color originalBackgroundColor = camera.backgroundColor;

            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                return TakeSnapshot(camera, directory, filename);
            }
            finally
            {
                camera.clearFlags = originalClearFlags;
                camera.backgroundColor = originalBackgroundColor;
            }
        }

        public static void TakeSnapShotOfAssetInIsolatedScene(MTIONSDKAssetBase asset, string basePersistentDirectory)
        {
            if (!TryGetThumbnailCreationScenePath(out string thumbnailScenePath))
            {
                Debug.LogWarning("MTIONSDKThumbnailScene could not be found. Skipping isolated thumbnail generation.");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(asset.ObjectReferenceProp, ThumbnailCreationTempPrefabPath);
            GameObject assetObject = PrefabUtility.LoadPrefabContents(ThumbnailCreationTempPrefabPath);

            string currentScenePath = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(thumbnailScenePath, OpenSceneMode.Single);

            Quaternion assetRotation = Quaternion.Euler(0f, -120f, 0f);
            GameObject goToSnapshot = GameObject.Instantiate(assetObject, Vector3.zero, assetRotation);

            Camera camera = Camera.main;
            CenterTargetInFrame centerTargetInFrame = camera.GetComponent<CenterTargetInFrame>();
            centerTargetInFrame.CenterOnTarget(goToSnapshot);
            
            TakeSnapshot(camera, basePersistentDirectory, "thumbnail.png");

            AssetDatabase.DeleteAsset(ThumbnailCreationTempPrefabPath);
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }

        private static bool TakeSnapshot(Camera camera, string directory, string filename)
        {
            if (camera == null)
            {
                return false;
            }
            
            RenderTexture rt = new RenderTexture(ThumbnailResWidth, ThumbnailResHeight, 24);
            rt.name = "ThumbnailGenerator.TakeSnapshot.rt";
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            Texture2D snapshot = new Texture2D(ThumbnailResWidth, ThumbnailResHeight, TextureFormat.RGB24, false);
            snapshot.name = $"ThumbnailGenerator.TakeSnapshot.Snapshot-{filename}";
            snapshot.ReadPixels(new Rect(0, 0, ThumbnailResWidth, ThumbnailResHeight), 0, 0);

            camera.targetTexture = null;
            RenderTexture.active = null; // JC: added to avoid errors
#if UNITY_EDITOR
            GameObject.DestroyImmediate(rt);
#else
            GameObject.Destroy(rt);
#endif

            bool writeSucceeded = WriteSnapshotToFile(snapshot, directory, filename);
#if UNITY_EDITOR
            GameObject.DestroyImmediate(snapshot);
#else
            GameObject.Destroy(snapshot);
#endif
            return writeSucceeded;
        }

        private static bool WriteSnapshotToFile(Texture2D snapshot, string directory, string filename)
        {
            byte[] bytes = snapshot.EncodeToPNG();
            string thumbnailPath = Path.Combine(directory, filename).Replace('\\', '/');
            if (SafeFileIO.Exists(thumbnailPath))
            {
                SafeFileIO.Delete(thumbnailPath);
            }
            SafeFileIO.WriteAllBytes(thumbnailPath, bytes);
            return SafeFileIO.Exists(thumbnailPath);
        }

        private static List<Camera> GetSceneCameras(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return new List<Camera>();
            }

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Where(camera => camera != null && camera.gameObject.scene == scene)
                .ToList();
        }

        private static List<Camera> GetLoadedSceneCameras()
        {
            List<Camera> cameras = new List<Camera>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                cameras.AddRange(GetSceneCameras(SceneManager.GetSceneAt(i)));
            }

            return cameras;
        }

        private static Camera SelectPreferredCamera(IEnumerable<Camera> cameras)
        {
            List<Camera> cameraList = cameras.Where(camera => camera != null).Distinct().ToList();
            return cameraList
                .Where(camera => camera != null)
                .OrderBy(camera => !camera.enabled)
                .ThenBy(camera => !camera.gameObject.activeInHierarchy)
                .ThenBy(camera => !camera.CompareTag("MainCamera"))
                .ThenBy(camera => camera.depth)
                .FirstOrDefault(camera => camera.enabled && camera.gameObject.activeInHierarchy)
                ?? cameraList.FirstOrDefault(camera => camera.enabled)
                ?? cameraList.FirstOrDefault();
        }

        private static bool CanCreateTemporaryThumbnailCamera(MTIONSDKDescriptorSceneBase sceneBase, out string diagnostic)
        {
            if (TryGetThumbnailTargetBounds(sceneBase, out _, out diagnostic))
            {
                return true;
            }

            return false;
        }

        private static bool TryCreateTemporaryThumbnailCamera(MTIONSDKDescriptorSceneBase sceneBase, out Camera camera, out GameObject temporaryCameraObject, out string diagnostic)
        {
            camera = null;
            temporaryCameraObject = null;

            if (!TryGetThumbnailTargetBounds(sceneBase, out Bounds targetBounds, out diagnostic))
            {
                return false;
            }

            temporaryCameraObject = new GameObject("MTIONSDK_TemporaryThumbnailCamera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            camera = temporaryCameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 5000f;
            camera.fieldOfView = 45f;

            Vector3 targetCenter = targetBounds.center;
            float radius = Mathf.Max(targetBounds.extents.magnitude, 1f);
            float distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) + radius;
            temporaryCameraObject.transform.position = targetCenter - TemporaryCameraDirection * distance;
            temporaryCameraObject.transform.rotation = Quaternion.LookRotation(targetCenter - temporaryCameraObject.transform.position, Vector3.up);

            diagnostic = $"Created temporary thumbnail camera for '{sceneBase.InternalID}'.";
            return true;
        }

        private static bool TryGetThumbnailTargetBounds(MTIONSDKDescriptorSceneBase sceneBase, out Bounds bounds, out string diagnostic)
        {
            Transform targetTransform = sceneBase.ObjectReference != null
                ? sceneBase.ObjectReference.transform
                : sceneBase.transform;

            Renderer[] renderers = targetTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                diagnostic = $"Using renderer bounds for '{sceneBase.InternalID}'.";
                return true;
            }

            Collider[] colliders = targetTransform.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }

                diagnostic = $"Using collider bounds for '{sceneBase.InternalID}'.";
                return true;
            }

            bounds = new Bounds(targetTransform.position, Vector3.one * 2f);
            diagnostic = $"No renderers or colliders found for '{sceneBase.InternalID}'. Using default thumbnail bounds around transform position.";
            return true;
        }
    }
}
