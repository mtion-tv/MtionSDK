using mtion.room.sdk.action;
using UnityEngine;

namespace mtion.room.sdk
{
    public sealed class ResizableCube : MonoBehaviour
    {
        #region public properties

        [CustomProperty]
        public float Width
        {
            get
            {
                return transform.localScale.x;
            }
            set
            {
                Vector3 scale = transform.localScale;
                scale.x = value;
                transform.localScale = scale;
            }
        }

        [CustomProperty]
        public float Depth
        {
            get
            {
                return transform.localScale.z;
            }
            set
            {
                Vector3 scale = transform.localScale;
                scale.z = value;
                transform.localScale = scale;
            }
        }

        [CustomProperty]
        public float Height
        {
            get
            {
                return transform.localScale.y;
            }
            set
            {
                Vector3 scale = transform.localScale;
                scale.y = value;
                transform.localScale = scale;
            }
        }
        
        #endregion
    }
}
