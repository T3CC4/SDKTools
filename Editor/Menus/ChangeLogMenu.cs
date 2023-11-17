#if UNITY_EDITOR
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using static UnityEngine.EventSystems.EventTrigger;

namespace SDKTools
{
    [System.Serializable]
    public class ChangelogEntry
    {
        public string version;
        public List<string> changes;
    }

    [System.Serializable]
    public class ChangelogData
    {
        public List<ChangelogEntry> changelog;
    }


    public class ChangeLogMenu : EditorWindow
    {

        [MenuItem("VRChat SDK/SDKTools/ChangeLog")]
        public static void ShowWindow()
        {
            var window = GetWindow<ChangeLogMenu>("ChangeLog");
            window.maxSize = new Vector2(650, 900);
        }

        string text = "";
        string latestversion = "";
        string version = VersionHandler.Version();

        Vector2 scrollpos;

        private GUIStyle separatorStyle;

        private void OnEnable()
        {
            changelogData = JsonConvert.DeserializeObject<ChangelogData>(VersionHandler.GetStringByURL("https://api.SDKTools.repl.co/changelog.html"));
            latestversion = VersionHandler.GetStringByURL("https://api.SDKTools.repl.co");

            separatorStyle = new GUIStyle();
            separatorStyle.normal.background = EditorGUIUtility.whiteTexture;
            separatorStyle.stretchWidth = true; // Make it full width
            separatorStyle.fixedHeight = 1; // Set the separator height
            separatorStyle.margin = new RectOffset(0, 0, 0, 0); // Add margin for spacing
            separatorStyle.normal.textColor = Color.gray; // Set color
        }

        ChangelogData changelogData = null;

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<size=20><color=white>Version: {0}</color></size>", "v" + version), new GUIStyle() { richText = true, padding = new RectOffset(10, 0, 10, 10) });

            if ("v" + version != latestversion)
            {
                GUILayout.Label(string.Format("<size=20><color=white>(Latest: {0})</color></size>", latestversion), new GUIStyle() { richText = true, padding = new RectOffset(10, 0, 10, 10) });
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            scrollpos = GUILayout.BeginScrollView(scrollpos);

            if (changelogData != null && changelogData.changelog != null)
            {
                foreach (ChangelogEntry entry in changelogData.changelog)
                {
                    GUILayout.Box("", separatorStyle);

                    EditorGUILayout.Space();

                    GUILayout.BeginVertical();

                    GUILayout.Label(string.Format("<size=15><color=white><b>Version: {0}</b></color></size>", entry.version), new GUIStyle() { richText = true, padding = new RectOffset(5, 5, 10, 10) });

                    GUILayout.BeginVertical("textfield");

                    foreach (string change in entry.changes)
                    {
                        GUILayout.Label(change);
                    }

                    GUILayout.EndVertical();
                    GUILayout.EndVertical();

                    EditorGUILayout.Space();
                    EditorGUILayout.Space();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

        }
    }
}
#endif