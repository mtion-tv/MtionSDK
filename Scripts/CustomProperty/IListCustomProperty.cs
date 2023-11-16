using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    public interface IListCustomProperty
    {
        public int DefaultLength { get; }
        public bool FixedLength { get; }
        public int MinLength { get; }
        public int MaxLength { get; }

        public void DefineList(int defaultLength, bool fixedLength, int minLength, int maxLength);
    }
}
