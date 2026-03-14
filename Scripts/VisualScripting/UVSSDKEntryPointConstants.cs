using System;
using System.Security.Cryptography;
using System.Text;

namespace mtion.room.sdk.visualscripting
{
    public static class UVSSDKEntryPointConstants
    {
        public const string EventHookName = "MTION.SDK.EntryPoint";
        public const string EntryPointUnitTypeName = "mtion.room.sdk.visualscripting.UVSSDKEntryPointUnit";
        public const string EntryPointUnitCategory = "Events/MTION";
        public const string DefaultDisplayName = "Entry Point";
        public const string BridgeActionId = "TriggerUVSSDKEntryPoint";
        public const string BridgeActionName = "Trigger UVS Entry Point";
        public const string BridgeActionDescription = "Trigger one of the Unity Visual Scripting SDK entry points on this object.";
        public const string StableLookupNamePrefix = "__uvs_";

        public static string FormatDropdownOption(string displayName, string entryPointId)
        {
            return $"{displayName}|{entryPointId}";
        }

        public static string ParseEntryPointId(string optionValue)
        {
            if (string.IsNullOrWhiteSpace(optionValue))
            {
                return string.Empty;
            }

            int separatorIndex = optionValue.LastIndexOf('|');
            if (separatorIndex < 0)
            {
                return optionValue.Trim();
            }

            if (separatorIndex >= optionValue.Length - 1)
            {
                return string.Empty;
            }

            return optionValue.Substring(separatorIndex + 1).Trim();
        }

        public static string ParseDisplayName(string optionValue)
        {
            if (string.IsNullOrWhiteSpace(optionValue))
            {
                return string.Empty;
            }

            int separatorIndex = optionValue.LastIndexOf('|');
            if (separatorIndex <= 0)
            {
                return optionValue.Trim();
            }

            return optionValue.Substring(0, separatorIndex).Trim();
        }

        public static string GenerateEntryPointId()
        {
            return Guid.NewGuid().ToString("D");
        }

        public static string GenerateStableLookupId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string GetStableLookupObjectName(string stableLookupId)
        {
            if (string.IsNullOrWhiteSpace(stableLookupId))
            {
                stableLookupId = GenerateStableLookupId();
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(stableLookupId.Trim()));
                StringBuilder builder = new StringBuilder(StableLookupNamePrefix, StableLookupNamePrefix.Length + 24);
                for (int i = 0; i < 10; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
