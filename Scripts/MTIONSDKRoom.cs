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

        protected override bool ShowReference => false;

        [SerializeField] 
        public GameObject SDKRoot;

        public override bool MigrateFromDescriptorSO()
        {
            if (_defaultEnvironmentOLD != null && 
                _defaultEnvironmentOLD.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
            {
                EnvironmentInternalID = _defaultEnvironmentOLD.InternalID;
                _defaultEnvironmentOLD = null;
            }

            return base.MigrateFromDescriptorSO();
        }
    }
}
