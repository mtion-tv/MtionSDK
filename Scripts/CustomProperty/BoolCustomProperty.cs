using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    [Serializable]
    public class BoolCustomProperty : CustomProperty<bool>
    {
        public BoolCustomProperty(bool defaultValue)
        {
            _defaultValue = defaultValue; 
        }

        public override bool CleanValue(bool value)
        {
            return value;
        }
    }
}
