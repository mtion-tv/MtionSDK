using UnityEngine;
using Unity.Collections;


namespace mtion.room.sdk.compiled
{
    public class MSDKAddressableAssetDescriptorSO : MSDKSerializableBaseSO
    {

        [SerializeField, ReadOnly] public string InternalID;

        [SerializeField] public string Name;
        [SerializeField, Multiline(5)] public string Description;

        [SerializeField, HideInInspector] public string AddressableID;

        [SerializeField] public float Version;
        [SerializeField] public long CreateTimeMS;
        [SerializeField] public long UpdateTimeMS;
    }
}