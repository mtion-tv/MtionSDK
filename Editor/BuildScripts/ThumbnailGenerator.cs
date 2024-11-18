using mtion.room.sdk.compiled;
using mtion.utility;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace mtion.room.sdk
{
    public static class ThumbnailGenerator
    {
        private const int ThumbnailResWidth = 1024;
        private const int ThumbnailResHeight = 1024;

        private static string ThumbnailCreationTempPrefabPath = SDKUtil.GetBaseAssetDirectory() + "ThumbnailAssetTEMP.prefab";
        private static string ThumbnailCreationScenePath
        {
            get
            {
                string sceneGuid = AssetDatabase.FindAssets("MTIONSDKThumbnailScene")[0];
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                return scenePath;
            }
        }

        public static void TakeSnapshotOfAssetInCurrentScene(Camera camera, string directory, string filename)
        {
            if (camera == null)
            {
                Debug.LogWarning("Camera could not be found for snapshot image");
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;

            TakeSnapshot(camera, directory, filename);
        }

        public static void TakeSnapShotOfAssetInIsolatedScene(MTIONSDKAssetBase asset, string basePersistentDirectory)
        {
            PrefabUtility.SaveAsPrefabAsset(asset.ObjectReferenceProp, ThumbnailCreationTempPrefabPath);
            GameObject assetObject = PrefabUtility.LoadPrefabContents(ThumbnailCreationTempPrefabPath);

            string currentScenePath = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(ThumbnailCreationScenePath, OpenSceneMode.Single);

            Quaternion assetRotation = Quaternion.Euler(0f, -120f, 0f);
            GameObject goToSnapshot = GameObject.Instantiate(assetObject, Vector3.zero, assetRotation);

            Camera camera = Camera.main;
            CenterTargetInFrame centerTargetInFrame = camera.GetComponent<CenterTargetInFrame>();
            centerTargetInFrame.CenterOnTarget(goToSnapshot);
            
            TakeSnapshot(camera, basePersistentDirectory, "thumbnail.png");

            AssetDatabase.DeleteAsset(ThumbnailCreationTempPrefabPath);
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }

        private static void TakeSnapshot(Camera camera, string directory, string filename)
        {
            if (camera == null)
            {
                return;
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

            WriteSnapshotToFile(snapshot, directory, filename);
#if UNITY_EDITOR
            GameObject.DestroyImmediate(snapshot);
#else
            GameObject.Destroy(snapshot);
#endif
        }

        private static void WriteSnapshotToFile(Texture2D snapshot, string directory, string filename)
        {
            byte[] bytes = snapshot.EncodeToPNG();
            string thumbnailPath = Path.Combine(directory, filename).Replace('\\', '/');
            if (SafeFileIO.Exists(thumbnailPath))
            {
                SafeFileIO.Delete(thumbnailPath);
            }
            SafeFileIO.WriteAllBytes(thumbnailPath, bytes);
        }
    }
}
