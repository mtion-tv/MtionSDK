using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    public class CustomPropertiesContainer : MonoBehaviour
    {
        [SerializeField] public List<BoolCustomProperty> BoolCustomProperties;
        [SerializeField] public List<IntCustomProperty> IntCustomProperties;
        [SerializeField] public List<FloatCustomProperty> FloatCustomProperties;
        [SerializeField] public List<StringCustomProperty> StringCustomProperties;

        [SerializeField] public List<ListIntCustomProperty> ListIntCustomProperties;
        [SerializeField] public List<ListStringCustomProperty> ListStringCustomProperties;

        public void ClearProperties()
        {
            BoolCustomProperties = new List<BoolCustomProperty>();
            IntCustomProperties = new List<IntCustomProperty>();
            FloatCustomProperties = new List<FloatCustomProperty>();
            StringCustomProperties = new List<StringCustomProperty>();

            ListIntCustomProperties = new List<ListIntCustomProperty>();
            ListStringCustomProperties = new List<ListStringCustomProperty>();
        }

        public List<ICustomProperty> GetAllProperties()
        {
            var output = new List<ICustomProperty>();
            output.AddRange(BoolCustomProperties);
            output.AddRange(IntCustomProperties);
            output.AddRange(FloatCustomProperties);
            output.AddRange(StringCustomProperties);
            output.AddRange(ListIntCustomProperties); 
            output.AddRange(ListStringCustomProperties);
            return output;
        }
    }
}
