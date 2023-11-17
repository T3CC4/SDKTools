using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace SDKTools
{
    [InitializeOnLoad]
    public class AnimatorHandler : Editor
    {
        static AnimatorHandler()
        {
            EditorApplication.hierarchyChanged += CheckForScript;
        }
        static void CheckForScript()
        {
            GameObject[] allGameObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (GameObject go in allGameObjects)
            {
                if (go.GetComponent<VRCAvatarDescriptor>() != null)
                {
                    if (!go.GetComponent<Animator>())
                    {
                        go.AddComponent<Animator>();
                        Debug.Log("Added Animator on " + go.name + " to prevent weird error from SDK.");
                    }
                }
            }
        }
    }
}