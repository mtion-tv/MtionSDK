using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    [Serializable]
    public class ListStringCustomProperty : ListCustomProperty<string>
    {
        public ListStringCustomProperty(string defaultElementValue)
        {
            _defaultElementValue = defaultElementValue;
        }

        public override string CleanElementValue(string value)
        {
            return value;
        }
    }
}
