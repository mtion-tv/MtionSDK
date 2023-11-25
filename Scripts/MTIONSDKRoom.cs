using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace mtion.room.sdk.compiled
{
    [ExecuteInEditMode]
    public sealed class MTIONSDKRoom : MTIONSDKDescriptorSceneBase
    {
        [Obsolete]
        [FormerlySerializedAs("defaultEnvironment")]
        [SerializeField]
        [HideInInspector]
        private MSDKAddressableAssetDescriptorSO _defaultEnvironmentOLD;

        public string EnvironmentInternalID;
        
        [SerializeField, HideInInspector] 
        public GameObject SDKRoot;

        public override void MigrateFromDescriptorSO()
        {
            if (_defaultEnvironmentOLD != null && 
                _defaultEnvironmentOLD.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
            {
                EnvironmentInternalID = _defaultEnvironmentOLD.InternalID;
                _defaultEnvironmentOLD = null;
            }

            base.MigrateFromDescriptorSO();
        }
    }
}
