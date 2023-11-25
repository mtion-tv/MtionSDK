using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.action
{
    public class CustomPropertyAttribute : Attribute
    {

        public CustomPropertyAttribute()
        {

        }


        public bool BoolDefaultValue { get; } = false;

        public CustomPropertyAttribute(bool defaultValue)
        {
            BoolDefaultValue = defaultValue;
        }


        public int IntDefaultValue { get; } = 0;
        public int IntMinValue { get; } = int.MinValue;
        public int IntMaxValue { get; } = int.MaxValue;

        public CustomPropertyAttribute(int defaultValue = 0, int min = int.MinValue, int max = int.MaxValue)
        {
            IntDefaultValue = defaultValue;
            IntMinValue = min;
            IntMaxValue = max;
        }


        public float FloatDefaultValue { get; } = 0f;
        public float FloatMinValue { get; } = float.MinValue;
        public float FloatMaxValue { get; } = float.MaxValue;

        public CustomPropertyAttribute(float defaultValue = 0f, float min = float.MinValue, float max = float.MaxValue)
        {
            FloatDefaultValue = defaultValue;
            FloatMinValue = min;
            FloatMaxValue = max;
        }


        public string StringDefaultValue { get; } = "";

        public CustomPropertyAttribute(string defaultValue = "")
        {
            StringDefaultValue = defaultValue;
        }


        public int ListDefaultLength { get; } = 1;
        public bool ListFixedLength { get; } = false;
        public int ListMinLength { get; } = 0;
        public int ListMaxLength { get; } = int.MaxValue;

        public CustomPropertyAttribute(int defaultValue = 0, int minValue = int.MinValue, int maxValue = int.MaxValue, 
            int defaultLength = 1, bool fixedLength = false, int minLength = 1, int maxLength = int.MaxValue)
        {
            IntDefaultValue = defaultValue;
            IntMinValue = minValue;
            IntMaxValue = maxValue;

            ListDefaultLength = defaultLength;
            ListFixedLength = fixedLength;
            ListMinLength = minLength;
            ListMaxLength = maxLength;
        }

        public CustomPropertyAttribute(string defaultValue = "",
            int defaultLength = 1, bool fixedLength = false, int minLength = 1, int maxLength = int.MaxValue)
        {
            StringDefaultValue = defaultValue;

            ListDefaultLength = defaultLength;
            ListFixedLength = fixedLength;
            ListMinLength = minLength;
            ListMaxLength = maxLength;
        }
    }
}
