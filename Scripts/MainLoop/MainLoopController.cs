using System;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.compiled.MainLoop
{
    public sealed class MainLoopController : MonoBehaviour
    {
        #region private static attributes
        
        private static List<MainLoopController> _controllers = new List<MainLoopController>();
        
        #endregion
        
        #region private attributes

        private IMainLoopListener[] _localListeners;

        #endregion
        
        #region MonoBehaviour implementation

        private void Awake()
        {
            _localListeners = GetComponents<IMainLoopListener>();
        }

        private void OnEnable()
        {
            _controllers.Add(this);
        }

        private void OnDisable()
        {
            _controllers.Remove(this);
        }

        #endregion
        
        #region public static functions

        public static void MainLoop(MainLoopData data)
        {
            foreach (MainLoopController controller in _controllers)
            {
                controller.MainLoopUpdate(data);
            }
        }
        
        public static int GetListenerCount()
        {
            int count = 0;
            foreach (MainLoopController controller in _controllers)
            {
                count += controller._localListeners.Length;
            }

            return count;
        }


        #endregion

        #region private functions

        private void MainLoopUpdate(MainLoopData data)
        {
            foreach (IMainLoopListener listener in _localListeners)
            {
                listener.MainLoop(data);
            }
        }
        
        #endregion
    }
}
