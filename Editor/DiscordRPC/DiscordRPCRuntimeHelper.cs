#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDKTools
{
    [InitializeOnLoadAttribute]
    public static class DiscordRpcRuntimeHelper
    {
        // register an event handler when the class is initialized
        static DiscordRpcRuntimeHelper()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;
        }

        private static void LogPlayModeState(PlayModeStateChange state)
        {
            if(state == PlayModeStateChange.EnteredEditMode)
            {
                DiscordRPCSerializer.updateState(RpcState.EDITMODE);
                DiscordRPCSerializer.ResetTime();
            } else if(state == PlayModeStateChange.EnteredPlayMode)
            {
                if (GameObject.Find("VRCSDK"))
                {
                    if (GameObject.Find("VRCSDK/UI/Canvas/AvatarPanel"))
                    {
                        DiscordRPCSerializer.updateState(RpcState.UPLOADAVATAR);
                        DiscordRPCSerializer.ResetTime();
                    }
                }
                else
                {
                    DiscordRPCSerializer.updateState(RpcState.PLAYMODE);
                    DiscordRPCSerializer.ResetTime();
                }
            }
        }
    }
}
#endif