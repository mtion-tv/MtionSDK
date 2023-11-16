using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    [Serializable]
    public class ListIntCustomProperty : ListCustomProperty<int>
    {
        [SerializeField] private int _minValue;
        [SerializeField] private int _maxValue;

        public int MinValue => _minValue;
        public int MaxValue => _maxValue;

        public ListIntCustomProperty(int defaultElementValue, int minValue, int maxValue)
        {
            _defaultElementValue = defaultElementValue;
            _minValue = minValue;
            _maxValue = maxValue;
        }

        public override int CleanElementValue(int value)
        {
            return Math.Clamp(value, _minValue, _maxValue);
        }
    }
}
