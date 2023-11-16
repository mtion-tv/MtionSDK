using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    [Serializable]
    public class StringCustomProperty : CustomProperty<string>
    {
        public StringCustomProperty(string defaultValue)
        {
            _defaultValue = defaultValue;
        }

        public override string CleanValue(string value)
        {
            return value;
        }
    }
}
