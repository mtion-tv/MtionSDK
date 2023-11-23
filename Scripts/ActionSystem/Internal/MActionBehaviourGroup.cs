using mtion.room.sdk.compiled;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.action
{
    [ExecuteInEditMode]
    public class MActionBehaviourGroup : MonoBehaviour
    {
        public List<MActionBehaviour> MActionMap = new List<MActionBehaviour>();

        public int Version = 1;

        private void Awake()
        {
#if MTION_ADVANCED_ACTION_UI
            gameObject.hideFlags = HideFlags.None;
#else
            gameObject.hideFlags = HideFlags.HideInHierarchy;
#endif
        }

        public MActionBehaviour CreateAction()
        {
            var go = new GameObject($"Action_{MActionMap.Count}");
            var ab = go.AddComponent<MActionBehaviour>();
            go.transform.parent = transform;

            ab.Guid = SDKUtil.GenerateNewGUID();
            MActionMap.Add(ab);

            return ab;
        }

        public void DeleteLast()
        {
            if (MActionMap.Count <= 0)
            {
                return;
            }

            var ab = MActionMap[MActionMap.Count - 1];
#if UNITY_EDITOR
            DestroyImmediate(ab.gameObject);
#else
            Destroy(ab.gameObject);
#endif

            ab = null;

            MActionMap.RemoveAt(MActionMap.Count - 1);
        }
        public List<MActionBehaviour> GetActions()
        {
            return MActionMap;
        }

        public void DestroyAll()
        {
            foreach (var ab in MActionMap)
            {
                Destroy(ab.gameObject);
            }
            MActionMap.Clear();
        }
    }
}