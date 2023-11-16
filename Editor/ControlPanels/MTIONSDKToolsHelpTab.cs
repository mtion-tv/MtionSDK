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
            if (GUILayout.Button("Documentation", MTIONSDKToolsWindow.LargeButtonStyle))
            {
                Application.OpenURL("https://mtion.notion.site/Everything-about-mtion-studio-21a73a6693cb4d429cc2160ffdd8890e");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Tutorials", MTIONSDKToolsWindow.LargeButtonStyle))
            {
                Application.OpenURL("https://mtion.notion.site/Tutorials-Guides-c72ea8be342240ad9ecbe02491f23693");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Discord", MTIONSDKToolsWindow.LargeButtonStyle))
            {
                Application.OpenURL("https://discord.com/invite/CuRyapecDq");
            }

            GUILayout.EndVertical();
        }
    }
}
