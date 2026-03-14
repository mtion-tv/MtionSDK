using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    public static class MTIONSDKToolsHelpTab
    {
        public static void Draw()
        {
            GUILayout.BeginVertical();

            GUILayout.Space(10);
            if (GUILayout.Button(new GUIContent("Documentation", "Opens link to SDK documentation"), MTIONSDKToolsWindow.LargeButtonStyle))
            {
            }

            GUILayout.Space(10);
            if (GUILayout.Button(new GUIContent("Tutorials", "Opens link to mxm studio tutorials"), MTIONSDKToolsWindow.LargeButtonStyle))
            {
            }

            GUILayout.Space(10);
            if (GUILayout.Button(new GUIContent("Discord", "Opens link to join our discord server"), MTIONSDKToolsWindow.LargeButtonStyle))
            {
                Application.OpenURL("https://discord.com/invite/CuRyapecDq");
            }

            GUILayout.EndVertical();
        }
    }
}
