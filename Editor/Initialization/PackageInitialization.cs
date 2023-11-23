using System.Linq;
using UnityEditor;
using UnityEngine;
using mtion.room.sdk.compiled;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.RegularExpressions;

namespace mtion.room.sdk
{
    public class MonoBehaviourYAMLParser
    {
        public class RootObjectAssetBase
        {
            [YamlMember(ApplyNamingConventions = false)]
            public MonoBehaviourAssetBase MonoBehaviour { get; set; }
        }

        public class MonoBehaviourAssetBase
        {
            [YamlMember(ApplyNamingConventions = false)]
            public int m_ObjectHideFlags { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public FileId m_CorrespondingSourceObject { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public FileId m_PrefabInstance { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public FileId m_PrefabAsset { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public FileId m_GameObject { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public int m_Enabled { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public int m_EditorHideFlags { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public Script m_Script { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string m_Name { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string m_EditorClassIdentifier { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string _guid { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public int ObjectType { get; set; }
        }

        public class RootObjectddressableAssetBase
        {
            [YamlMember(ApplyNamingConventions = false)]
            public MonoBehaviourAddressableAssetBase MonoBehaviour { get; set; }
        }

        public class MonoBehaviourAddressableAssetBase
        {
            [YamlMember(ApplyNamingConventions = false)]
            public int m_ObjectHideFlags { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public FileId m_CorrespondingSourceObject { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public FileId m_PrefabInstance { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public FileId m_PrefabAsset { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public FileId m_GameObject { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public int m_Enabled { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public int m_EditorHideFlags { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public Script m_Script { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string m_Name { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string m_EditorClassIdentifier { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string _guid { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public int ObjectType { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string InternalID { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string Name { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string Description { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public string AddressableID { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public float Version { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public long CreateTimeMS { get; set; }
            [YamlMember(ApplyNamingConventions = false)]
            public long UpdateTimeMS { get; set; }
        }

        public class FileId
        {
            public int fileID { get; set; }
        }

        public class Script
        {
            public int fileID { get; set; }
            public string guid { get; set; }
            public int type { get; set; }
        }

        public enum ObjectType
        {
            SCENE_ASSET,
            SDK_ASSET
        }

        // Deserializer setup
        private static IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

        // Serializer setup
        private static ISerializer serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        private string fileLocation = null;
        private string fileContents = null;

        public int FileID;
        public string FileGuid;
        public int FileType;

        //private dynamic parsedObject = null;

        public MonoBehaviourYAMLParser() { }

        public MonoBehaviourYAMLParser(string file, ObjectType sdkType)
        {
            ProcessFile(file, sdkType);
        }

        public void ProcessFile(string file, ObjectType sdkType)
        {
            fileLocation = file;

            fileContents = File.ReadAllText(fileLocation);

            // Remove Unity specific tags
            var input = Regex.Replace(fileContents, @"^%TAG !u!.*$", string.Empty, RegexOptions.Multiline);
            input = Regex.Replace(input, @"!u!\S+", string.Empty);

            // Parse data
            if (sdkType == ObjectType.SCENE_ASSET)
            {
                var parsedMonoBehaviour = deserializer.Deserialize<RootObjectAssetBase>(input);

                FileID = parsedMonoBehaviour.MonoBehaviour.m_Script.fileID;
                FileGuid = parsedMonoBehaviour.MonoBehaviour.m_Script.guid;
                FileType = parsedMonoBehaviour.MonoBehaviour.m_Script.type;
            }
            else
            {
                var parsedMonoBehaviour = deserializer.Deserialize<RootObjectddressableAssetBase>(input);

                FileID = parsedMonoBehaviour.MonoBehaviour.m_Script.fileID;
                FileGuid = parsedMonoBehaviour.MonoBehaviour.m_Script.guid;
                FileType = parsedMonoBehaviour.MonoBehaviour.m_Script.type;
            }
        }

        public void ReplaceInternalReferenceFiles(int fileId, string fileGuid, int fileType)
        {
            FileID = fileId;
            FileGuid = fileGuid;

            // Modify the values using regex
            // Can't properly serialize using YAML as there are custom TAGS in the asset files
            string replacement = $"m_Script: {{fileID: {fileId}, guid: {fileGuid}, type: {fileType}}}";
            string revisedAssets = Regex.Replace(fileContents, @"m_Script:\s+\{fileID:.*[0-9]+,\sguid:\s[A-Za-z0-9]+,\stype:\s[0-9]+\}", replacement);

            File.WriteAllText(fileLocation, revisedAssets);
        }
    }

    public class PackageInitialization : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var inPackages = importedAssets.Any(path => path.Contains("Packages/com.mtion.sdk/")) ||
                deletedAssets.Any(path => path.Contains("Packages/com.mtion.sdk/")) ||
                movedAssets.Any(path => path.Contains("Packages/com.mtion.sdk/")) ||
                movedFromAssetPaths.Any(path => path.Contains("Packages/com.mtion.sdk/"));

            if (inPackages)
            {
                InitializeOnLoad();
            }
        }

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            bool migrated = false;

            // Migrate Cameras
            VirtualComponentTracker[] cameras = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>();
            migrated |= MigrateAssets(cameras, MTIONObjectType.MTIONSDK_CAMERA);

            // Migrate Displays
            VirtualComponentTracker[] displays = GameObject.FindObjectsOfType<MVirtualDisplayTracker>();
            migrated |= MigrateAssets(displays, MTIONObjectType.MTIONSDK_DISPLAY);

            // Migrate Lights
            VirtualComponentTracker[] lights = GameObject.FindObjectsOfType<MVirtualLightingTracker>();
            migrated |= MigrateAssets(lights, MTIONObjectType.MTIONSDK_LIGHT);

            // Virtual Assets
            MTIONSDKAssetBase[] virtualAssets = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            migrated |= MigrateBaseAssets(virtualAssets, MTIONObjectType.MTIONSDK_CAMERA);

            // Room
            MTIONSDKAssetBase[] rooms = new MTIONSDKAssetBase[] { GameObject.FindObjectOfType<MTIONSDKRoom>() };
            migrated |= MigrateBaseAssets(rooms, MTIONObjectType.MTIONSDK_ROOM);

            // Env
            MTIONSDKAssetBase[] env = new MTIONSDKAssetBase[] { GameObject.FindObjectOfType<MTIONSDKEnvironment>() };
            migrated |= MigrateBaseAssets(env, MTIONObjectType.MTIONSDK_ENVIRONMENT);

            if (migrated)
            {
                Debug.Log($"Processing Scene Elements and Migrating\n\nCameras: {cameras.Length}\nDisplays: {displays.Length}\nLights: {lights.Length}\nAssets: {virtualAssets.Length}");
                Debug.Log($"\n\nRESTART UNITY PLEASE\n\n\n");
            }
        }


        private static bool MigrateAssets(VirtualComponentTracker[] assetList, MTIONObjectType sdkType)
        {
            // Nothing to migrate for now
            return true;
        }

        private static bool MigrateBaseAssets(MTIONSDKAssetBase[] assets, MTIONObjectType sdkType)
        {
            foreach (var asset in assets)
            {
                if (asset == null) continue;

                asset.MigrateFromDescriptorSO();
            }

            return true;
        }
    }
}
