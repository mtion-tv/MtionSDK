using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    public abstract class ListCustomProperty<T> : CustomProperty<List<T>>, IListCustomProperty
    {
        [SerializeField] private bool _fixedLength;
        [SerializeField] private int _defaultLength;
        [SerializeField] private int _minLength;
        [SerializeField] private int _maxLength;

        [SerializeField] protected T _defaultElementValue;

        public bool FixedLength => _fixedLength;
        public int DefaultLength => _defaultLength;
        public int MinLength => _minLength;
        public int MaxLength => _maxLength;

        public void DefineList(int defaultLength, bool fixedLength, int minLength, int maxLength)
        {
            _defaultLength = defaultLength;
            _fixedLength = fixedLength;
            _minLength = minLength;
            _maxLength = maxLength;
        }

        public T GetValueAtIndex(int index)
        {
            var list = GetValue();
            if (list == null ||
                index < 0 || 
                index >= list.Count)
            {
                return default;
            }

            return list[index];
        }

        public void SetValueAtIndex(int index, T value)
        {
            var list = GetValue();
            if (list == null ||
                index < 0 ||
                index >= list.Count)
            {
                return;
            }

            var cleanedVal = CleanElementValue(value);
            list[index] = cleanedVal;
            SetValue(list);
        }

        public void RemoveElement(int index)
        {
            var list = GetValue();
            if (list == null ||
                index < 0 ||
                index >= list.Count)
            {
                return;
            }

            list.RemoveAt(index);
            SetValue(list);
        }

        public void AddElement()
        {
            var list = GetValue();
            if (list == null)
            {
                return;
            }

            list.Add(_defaultElementValue);
            SetValue(list);
        }

        public override List<T> CleanValue(List<T> value)
        {
            if (value == null) return null;

            for (int i = 0; i < value.Count; ++i)
            {
                value[i] = CleanElementValue(value[i]);
            }

            return value;
        }

        public abstract T CleanElementValue(T value);
    }
}
