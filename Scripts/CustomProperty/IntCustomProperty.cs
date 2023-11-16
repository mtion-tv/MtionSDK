using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    [Serializable]
    public class IntCustomProperty : CustomProperty<int>
    {
        [SerializeField] private int _minValue;
        [SerializeField] private int _maxValue;

        public int MinValue => _minValue;
        public int MaxValue => _maxValue;

        public IntCustomProperty(int defaultValue, int minValue, int maxValue)
        {
            _defaultValue = defaultValue;
            _minValue = minValue;
            _maxValue = maxValue;
        }

        public override int CleanValue(int value)
        {
            return Math.Clamp(value, _minValue, _maxValue);
        }
    }
}
