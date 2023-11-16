using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    [Serializable]
    public class FloatCustomProperty : CustomProperty<float>
    {
        [SerializeField] private float _minValue;
        [SerializeField] private float _maxValue;

        public float MinValue => _minValue;
        public float MaxValue => _maxValue;

        public FloatCustomProperty(float defaultValue, float minValue, float maxValue)
        {
            _defaultValue = defaultValue;
            _minValue = minValue;
            _maxValue = maxValue;
        }

        public override float CleanValue(float value)
        {
            return Mathf.Clamp(value, _minValue, _maxValue);
        }
    }
}
