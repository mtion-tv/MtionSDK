using mtion.room.sdk.utility;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk.action
{
    [CustomEditor(typeof(MTIONComponent), true)]
    public class MTIONComponentEditor : Editor
    {
        private const string BANNER_TEXTURE_NAME = "component-banner.png";
        private const float BANNER_WIDTH = 120f;
        private const float BANNER_HEIGHT = 30f;

        private Texture2D _banner;

        private void OnEnable()
        {
            _banner = TextureLoader.LoadSDKTexture(BANNER_TEXTURE_NAME);
        }

        public override void OnInspectorGUI()
        {
            GUILayout.Label(_banner, GUILayout.Width(BANNER_WIDTH), GUILayout.Height(BANNER_HEIGHT));

            base.OnInspectorGUI();
        }
    }
}
