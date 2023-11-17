#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SDKTools
{
    public class RPCImgSelector : EditorWindow
    {
        public string[] rpcimgoptions = new string[] { "default", "drawn", "cursed", "energy", "water", "winter", "grass", "gold" };
        private int selectedOption = 0;

        [MenuItem("VRChat SDK/SDKTools/RPCSettings")]
        public static void ShowWindow()
        {
            var window = GetWindow<RPCImgSelector>("RPCSettings");
            window.maxSize = new Vector2(650, 900);
        }


        void OnEnable() 
        {
            selectedOption = FindStringInArray(EditorPrefs.GetString("SDKTools." + (PlayerSettings.productName.Length + 1) + ".CurrentRPCImage"), rpcimgoptions);
        }

        int FindStringInArray(string searchString, string[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == searchString)
                {
                    return i;
                }
            }

            return 0;
        }


        void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            selectedOption = EditorGUILayout.Popup("RPC Image:", selectedOption, rpcimgoptions);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString("SDKTools." + (PlayerSettings.productName.Length + 1) + ".CurrentRPCImage", rpcimgoptions[selectedOption]);
                DiscordRPCSerializer.updateDRPCImg();
            }
            EditorGUILayout.EndVertical();
        }
    }
}
#endif