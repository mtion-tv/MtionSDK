using mtion.room.sdk.compiled;
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

        public static void TakeSnapshotOfAssetInCurrentScene(Camera camera, string basePersistentDirectory)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;

            TakeSnapshot(camera, basePersistentDirectory);
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
            
            TakeSnapshot(camera, basePersistentDirectory);

            AssetDatabase.DeleteAsset(ThumbnailCreationTempPrefabPath);
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }

        private static void TakeSnapshot(Camera camera, string directory, string filename = "thumbnail.png")
        {
            if (camera == null)
            {
                return;
            }

            RenderTexture rt = new RenderTexture(ThumbnailResWidth, ThumbnailResHeight, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            Texture2D snapshot = new Texture2D(ThumbnailResWidth, ThumbnailResHeight, TextureFormat.RGB24, false);
            snapshot.name = $"Snapshoot-{filename}";
            snapshot.ReadPixels(new Rect(0, 0, ThumbnailResWidth, ThumbnailResHeight), 0, 0);

            camera.targetTexture = null;
            RenderTexture.active = null; // JC: added to avoid errors
            GameObject.DestroyImmediate(rt);

            WriteSnapshotToFile(snapshot, directory, filename);
            GameObject.Destroy(snapshot);
        }

        private static void WriteSnapshotToFile(Texture2D snapshot, string directory, string filename)
        {
            byte[] bytes = snapshot.EncodeToPNG();
            string thumbnailPath = Path.Combine(directory, filename).Replace('\\', '/');
            if (File.Exists(thumbnailPath))
            {
                File.Delete(thumbnailPath);
            }
            File.WriteAllBytes(thumbnailPath, bytes);
        }
    }
}
