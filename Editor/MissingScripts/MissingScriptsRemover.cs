#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SDKTools
{
    public class MissingScriptsRemover : Editor
    {
        [MenuItem("VRChat SDK/SDKTools/QoL/Remove Missing Scripts")]
        public static void RemoveMissingScripts()
        {
            GameObject[] gameObjects = GameObject.FindObjectsOfType<GameObject>();

            int totalComponentCount = 0;

            foreach (GameObject go in gameObjects)
            {
                int componentCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);

                if (componentCount > 0)
                {
                    Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");

                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

                    totalComponentCount += componentCount;
                }
            }

            string message = string.Format("Removed {0} missing scripts from {1} game object(s) in the hierarchy.", totalComponentCount, gameObjects.Length);
            EditorUtility.DisplayDialog("Remove Missing Scripts", message, "OK");
        }

        [MenuItem("VRChat SDK/SDKTools/Remove Missing Scripts", true)]
        private static bool ValidateRemoveMissingScripts()
        {
            return true;
        }
    }

}
#endif