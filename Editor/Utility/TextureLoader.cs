using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk.utility
{
    public static class TextureLoader
    {
        public static Texture2D LoadSDKTexture(string fileName)
        {
            var varPackageAsset = EditorGUIUtility.Load($"Packages/com.mtion.sdk/Editor Default Resources/_mtion/Icons/{fileName}") as Texture2D;
            var packageTexturePath = AssetDatabase.GetAssetPath(varPackageAsset);
            var destinationFullPath = $"{Application.dataPath}/Editor Default Resources/_mtion/Icons/{fileName}";

            var editorDefaultIconsPath = "Assets/Editor Default Resources/_mtion/Icons";
            if (!Directory.Exists(editorDefaultIconsPath))
            {
                Directory.CreateDirectory(editorDefaultIconsPath);
            }

            if (File.Exists(packageTexturePath))
            {
                File.Copy(packageTexturePath, destinationFullPath, true);
                AssetDatabase.Refresh();
            }

            return EditorGUIUtility.Load($"_mtion/Icons/{fileName}") as Texture2D;
        }
    }
}

