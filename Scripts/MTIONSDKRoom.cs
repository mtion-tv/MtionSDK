using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace mtion.room.sdk.compiled
{
    public class MTIONSDKRoom : MTIONSDKDescriptorSceneBase
    {
        [Obsolete]
        [FormerlySerializedAs("defaultEnvironment")]
        [SerializeField]
        [HideInInspector]
        private MSDKAddressableAssetDescriptorSO _defaultEnvironmentOLD;

        public string EnvironmentInternalID;

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