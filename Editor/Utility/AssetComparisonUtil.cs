using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace mtion.room.sdk.compiled
{
    public static class AssetComparisonUtil
    {
        public static List<MTIONSDKAssetBase> FilterDuplicateAssets(IEnumerable<MTIONSDKAssetBase> assets,
            ExportLocationOptions locationOption)
        {
            var groupedDuplicates = new List<List<MTIONSDKAssetBase>>();
            foreach (var asset in assets)
            {
                var existingDuplicateFound = false;
                foreach (var duplicateGroup in groupedDuplicates)
                {
                    var duplicates = AssetsAreDuplicates(asset, duplicateGroup[0]);
                    if (duplicates)
                    {
                        duplicateGroup.Add(asset);
                        existingDuplicateFound = true;
                        break;
                    }
                }

                if (!existingDuplicateFound)
                {
                    groupedDuplicates.Add(new List<MTIONSDKAssetBase> { asset });
                }
            }

            var output = new List<MTIONSDKAssetBase>();
            foreach (var duplicateGroup in groupedDuplicates)
            {
                var foundPrevExport = false;
                foreach (var duplicate in duplicateGroup)
                {
                    if (string.IsNullOrEmpty(duplicate.GUID))
                    {
                        continue;
                    }

                    var dirExists = SDKUtil.GetSDKItemDirectoryExists(duplicate, locationOption);
                    if (dirExists)
                    {
                        output.Add(duplicate);
                        foundPrevExport = true;
                        break;
                    }
                }

                if (!foundPrevExport)
                {
                    output.Add(duplicateGroup[0]);
                }
            }

            return output;
        }

        public static bool AssetsAreDuplicates(MTIONSDKAssetBase asset1, MTIONSDKAssetBase asset2)
        {
            return CheckGameObjectsIdentical(asset1.ObjectReferenceProp, asset2.ObjectReferenceProp);
        }

        private static bool CheckGameObjectsIdentical(GameObject obj1, GameObject obj2)
        {
            if (!MatchingChildrenLocalPos(obj1, obj2) ||
                !MatchingMaterials(obj1, obj2) ||
                !MatchingVertices(obj1, obj2) ||
                !MatchingComponents(obj1, obj2))
            {
                return false;
            }

            for (int i = 0; i < obj1.transform.childCount; ++i)
            {
                var success = CheckGameObjectsIdentical(obj1.transform.GetChild(i).gameObject,
                    obj2.transform.GetChild(i).gameObject);
                if (!success)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchingChildrenLocalPos(GameObject obj1, GameObject obj2)
        {
            if (obj1.transform.childCount != obj2.transform.childCount)
            {
                return false;
            }

            for (int i = 0; i < obj1.transform.childCount; ++i)
            {
                var pos1 = obj1.transform.GetChild(i).localPosition;
                var pos2 = obj2.transform.GetChild(i).localPosition;
                var rot1 = obj1.transform.GetChild(i).localRotation;
                var rot2 = obj2.transform.GetChild(i).localRotation;
                if (!pos1.Equals(pos2) || !rot1.Equals(rot2))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchingVertices(GameObject obj1, GameObject obj2)
        {
            var mesh1 = obj1.gameObject.GetComponent<MeshFilter>();
            var mesh2 = obj2.gameObject.GetComponent<MeshFilter>();
            if (mesh1 != null && mesh2 == null ||
                mesh1 == null && mesh2 != null)
            {
                return false;
            }
            else if (mesh1 == null && mesh2 == null)
            {
                return true;
            }

            var vertices1 = mesh1.sharedMesh.vertices;
            var vertices2 = mesh2.sharedMesh.vertices;

            if (vertices1.Length != vertices2.Length)
            {
                return false;
            }

            for (int i = 0; i < vertices1.Length; i += 10)
            {
                if (!vertices1[i].Equals(vertices2[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchingMaterials(GameObject obj1, GameObject obj2)
        {
            var renderer1 = obj1.gameObject.GetComponent<MeshRenderer>();
            var renderer2 = obj2.gameObject.GetComponent<MeshRenderer>();
            if (renderer1 != null && renderer2 == null ||
                renderer1 == null && renderer2 != null)
            {
                return false;
            }
            else if (renderer1 == null && renderer2 == null)
            {
                return true;
            }

            if (renderer1.sharedMaterials.Length != renderer2.sharedMaterials.Length)
            {
                return false;
            }

            for (int i = 0; i < renderer1.sharedMaterials.Length; ++i)
            {
                if (!renderer1.sharedMaterials[i].Equals(renderer2.sharedMaterials[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchingComponents(GameObject obj1, GameObject obj2)
        {
            var components1 = obj1.GetComponents(typeof(Component))
                .Select(comp => comp.GetType()).ToLookup(type => type);
            var components2 = obj2.GetComponents(typeof(Component))
                .Select(comp => comp.GetType()).ToLookup(type => type);

            foreach (var grp in components1)
            {
                if (grp.Count() != components2[grp.Key].Count())
                {
                    return false;
                }
            }

            foreach (var grp in components2)
            {
                if (grp.Count() != components1[grp.Key].Count())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
