using UnityEngine;

namespace mtion.room.sdk.compiled.MainLoop
{
    public sealed class MainLoopData
    {
        #region public properties
        
        public Vector3 CameraPosition { get; private set; }
        public Quaternion CameraRotaion { get; private set; }
        
        #endregion
        
        #region public constructors
        
        public MainLoopData()
        {

        }

        public MainLoopData(Vector3 cameraPosition, Quaternion cameraRotaion)
        {
            CameraPosition = cameraPosition;
            CameraRotaion = cameraRotaion;
        }

        public MainLoopData(Camera camera)
        {
            CameraPosition = camera.transform.position;
            CameraRotaion = camera.transform.rotation;
        }
        
        #endregion
    }
}
