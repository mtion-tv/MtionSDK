using System;
using System.Collections;
using mtion.room.sdk.compiled;
using UnityEngine;

namespace mtion.room.sdk
{
    public sealed class ForceLayer : MonoBehaviour
    {
        #region private attributes

        [SerializeField]
        [Layer]
        private int _layer;

        #endregion
        
        #region MonoBehaviour functions

        private void Start()
        {
            StartCoroutine(SetLayerCR());
        }

        #endregion

        #region private functions

        IEnumerator SetLayerCR()
        {
            yield return new WaitForSeconds(2.0f);

            SetLayerRecursively(gameObject, _layer);
        }
        
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;

            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        #endregion
    }
}
