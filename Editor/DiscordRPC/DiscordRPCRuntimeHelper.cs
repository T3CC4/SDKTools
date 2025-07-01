#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;


#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif

namespace SDKTools
{
    [InitializeOnLoad]
    public static class DiscordRpcRuntimeHelper
    {
        private static PlayModeStateChange? lastPlayModeState = null;
        private static RpcState lastDetectedState = RpcState.EDITMODE;
        private static bool isProcessingStateChange = false;

        private static string lastActiveSceneName = "";
        private static int lastGameObjectCount = 0;

        private static DateTime lastVrcSdkCheck = DateTime.MinValue;
        private static bool hasVrcSdk = false;
        private static readonly TimeSpan VRC_CHECK_INTERVAL = TimeSpan.FromSeconds(2);

        private static bool isAvatarUploadDetected = false;
        private static DateTime avatarUploadStartTime = DateTime.MinValue;
        private static readonly TimeSpan AVATAR_UPLOAD_TIMEOUT = TimeSpan.FromMinutes(10);

        private static DateTime lastStateCheck = DateTime.MinValue;
        private static readonly TimeSpan STATE_CHECK_INTERVAL = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan HIERARCHY_CHECK_COOLDOWN = TimeSpan.FromSeconds(2);

        private static DateTime lastPlayModeChange = DateTime.MinValue;
        private static readonly TimeSpan PLAY_MODE_LOCK_DURATION = TimeSpan.FromSeconds(3);

        static DiscordRpcRuntimeHelper()
        {
            try
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                EditorApplication.hierarchyChanged += OnHierarchyChanged;
                EditorApplication.update += OnEditorUpdate;

                InitializeHelper();

                LogInfo("DiscordRpcRuntimeHelper initialized successfully");
            }
            catch (Exception e)
            {
                LogError($"Failed to initialize DiscordRpcRuntimeHelper: {e.Message}");
            }
        }

        private static void InitializeHelper()
        {
            try
            {
                DetectAndUpdateCurrentState();

                RefreshSceneCache();

                LogInfo($"Initial state detected: {lastDetectedState.StateName()}");
            }
            catch (Exception e)
            {
                LogError($"Error during helper initialization: {e.Message}");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (isProcessingStateChange)
            {
                LogWarning("State change already in progress, skipping");
                return;
            }

            try
            {
                isProcessingStateChange = true;
                lastPlayModeChange = DateTime.Now;

                LogInfo($"Play mode state changed: {lastPlayModeState} -> {state}");

                RpcState newState = DetermineStateFromPlayMode(state);
                if (newState != lastDetectedState)
                {
                    UpdateRpcState(newState, $"Play mode changed to {state}");
                }

                lastPlayModeState = state;
            }
            catch (Exception e)
            {
                LogError($"Error handling play mode state change: {e.Message}");
            }
            finally
            {
                isProcessingStateChange = false;
            }
        }

        private static void OnHierarchyChanged()
        {
            try
            {
                if (DateTime.Now - lastPlayModeChange < PLAY_MODE_LOCK_DURATION)
                {
                    return;
                }

                if (isProcessingStateChange ||
                    DateTime.Now - lastStateCheck < HIERARCHY_CHECK_COOLDOWN)
                {
                    return;
                }

                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                lastStateCheck = DateTime.Now;

                bool sceneChanged = HasSceneChanged();
                bool significantChange = HasSignificantHierarchyChange();

                if (sceneChanged || significantChange)
                {
                    RefreshSceneCache();

                    if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        DetectAndUpdateCurrentState();
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"Error in hierarchy changed handler: {e.Message}");
            }
        }

        private static void OnEditorUpdate()
        {
            try
            {
                CheckAvatarUploadTimeout();

                if (DateTime.Now - lastStateCheck > TimeSpan.FromSeconds(10) &&
                    DateTime.Now - lastPlayModeChange > PLAY_MODE_LOCK_DURATION &&
                    !isProcessingStateChange)
                {
                    ValidateCurrentState();
                }
            }
            catch (Exception e)
            {
                LogError($"Error in editor update: {e.Message}");
            }
        }

        private static RpcState DetermineStateFromPlayMode(PlayModeStateChange state)
        {
            try
            {
                switch (state)
                {
                    case PlayModeStateChange.EnteredEditMode:
                        isAvatarUploadDetected = false;
                        avatarUploadStartTime = DateTime.MinValue;
                        return RpcState.EDITMODE;

                    case PlayModeStateChange.EnteredPlayMode:
                        return DetectPlayModeState();

                    case PlayModeStateChange.ExitingEditMode:
                    case PlayModeStateChange.ExitingPlayMode:
                        LogInfo($"Play mode transitioning ({state}), maintaining current state: {lastDetectedState.StateName()}");
                        return lastDetectedState;

                    default:
                        LogWarning($"Unknown play mode state: {state}");
                        return lastDetectedState;
                }
            }
            catch (Exception e)
            {
                LogError($"Error determining state from play mode: {e.Message}");
                return lastDetectedState;
            }
        }

        private static RpcState DetectPlayModeState()
        {
            try
            {
                if (HasVrcSdkInScene())
                {
                    if (IsAvatarUploadScenario())
                    {
                        isAvatarUploadDetected = true;
                        avatarUploadStartTime = DateTime.Now;
                        LogInfo("Avatar upload scenario detected");
                        return RpcState.UPLOADAVATAR;
                    }
                }

                return RpcState.PLAYMODE;
            }
            catch (Exception e)
            {
                LogError($"Error detecting play mode state: {e.Message}");
                return RpcState.PLAYMODE;
            }
        }

        private static void DetectAndUpdateCurrentState()
        {
            try
            {
                RpcState newState;

                if (EditorApplication.isPlaying)
                {
                    newState = DetectPlayModeState();
                }
                else if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    LogInfo("Play mode transition in progress, maintaining current state");
                    return;
                }
                else
                {
                    newState = RpcState.EDITMODE;
                    isAvatarUploadDetected = false;
                    avatarUploadStartTime = DateTime.MinValue;
                }

                if (newState != lastDetectedState)
                {
                    UpdateRpcState(newState, "State auto-detected");
                }
            }
            catch (Exception e)
            {
                LogError($"Error detecting current state: {e.Message}");
            }
        }

        private static void ValidateCurrentState()
        {
            try
            {
                lastStateCheck = DateTime.Now;

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                if (lastDetectedState == RpcState.UPLOADAVATAR && isAvatarUploadDetected)
                {
                    if (!EditorApplication.isPlaying || !IsAvatarUploadScenario())
                    {
                        LogInfo("Avatar upload scenario no longer detected, updating state");
                        DetectAndUpdateCurrentState();
                    }
                }
                else if (lastDetectedState == RpcState.PLAYMODE && EditorApplication.isPlaying)
                {
                    if (IsAvatarUploadScenario())
                    {
                        LogInfo("Avatar upload scenario detected during play mode validation");
                        UpdateRpcState(RpcState.UPLOADAVATAR, "Avatar upload detected during validation");
                    }
                }
                else if (lastDetectedState != RpcState.EDITMODE && !EditorApplication.isPlaying)
                {
                    LogInfo("Should be in edit mode but state is different, correcting");
                    UpdateRpcState(RpcState.EDITMODE, "Corrected to edit mode");
                }
            }
            catch (Exception e)
            {
                LogError($"Error validating current state: {e.Message}");
            }
        }

        private static bool HasVrcSdkInScene()
        {
            try
            {
                if (DateTime.Now - lastVrcSdkCheck < VRC_CHECK_INTERVAL)
                {
                    return hasVrcSdk;
                }

                lastVrcSdkCheck = DateTime.Now;

                GameObject vrcSdk = GameObject.Find("VRCSDK");
                hasVrcSdk = vrcSdk != null;

                if (!hasVrcSdk)
                {
#if VRC_SDK_VRCSDK3
                    var avatarDescriptors = Object.FindObjectsOfType<VRCAvatarDescriptor>();
                    var sceneDescriptors = Object.FindObjectsOfType<VRC_SceneDescriptor>();
                    hasVrcSdk = avatarDescriptors.Length > 0 || sceneDescriptors.Length > 0;
#endif
                }

                return hasVrcSdk;
            }
            catch (Exception e)
            {
                LogError($"Error checking for VRC SDK: {e.Message}");
                return false;
            }
        }

        private static bool IsAvatarUploadScenario()
        {
            try
            {
                if (!HasVrcSdkInScene())
                    return false;

                if (IsActuallyUploading())
                {
                    LogInfo("Avatar upload detected: VRC SDK is actively uploading");
                    return true;
                }

                if (IsVRCBuildOrUploadActive())
                {
                    LogInfo("Avatar upload detected: VRC SDK build/upload process active");
                    return true;
                }

                if (IsVRCSDKInUploadMode())
                {
                    LogInfo("Avatar upload detected: VRC SDK Control Panel in upload mode");
                    return true;
                }

                if (EditorApplication.isPlaying && HasValidUploadAvatar())
                {
                    LogInfo("Avatar upload detected: Play mode with valid upload avatar");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                LogError($"Error checking avatar upload scenario: {e.Message}");
                return false;
            }
        }

        private static bool IsActuallyUploading()
        {
            try
            {
                var vrcfuryHookType = System.Type.GetType("VF.Hooks.IsActuallyUploadingHook, VF.Hooks");
                if (vrcfuryHookType != null)
                {
                    var getMethod = vrcfuryHookType.GetMethod("Get", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (getMethod != null)
                    {
                        bool isUploading = (bool)getMethod.Invoke(null, null);
                        if (isUploading)
                        {
                            LogInfo("VRCFury reports upload in progress");
                            return true;
                        }
                    }
                }

                var vrcSdkControlPanelType = System.Type.GetType("VRC.SDK3.Editor.VRCSdkControlPanel, VRC.SDK3.Editor");
                if (vrcSdkControlPanelType != null)
                {
                    var uploadInProgressField = vrcSdkControlPanelType.GetField("_isUploading",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                    if (uploadInProgressField != null)
                    {
                        bool isUploading = (bool)uploadInProgressField.GetValue(null);
                        if (isUploading)
                        {
                            return true;
                        }
                    }

                    string[] possibleFields = { "_uploading", "isUploading", "uploadInProgress", "_uploadInProgress" };
                    foreach (string fieldName in possibleFields)
                    {
                        var field = vrcSdkControlPanelType.GetField(fieldName,
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                        if (field != null && field.FieldType == typeof(bool))
                        {
                            bool isUploading = (bool)field.GetValue(null);
                            if (isUploading)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                LogError($"Error checking if actually uploading: {e.Message}");
                return false;
            }
        }

        private static bool IsVRCBuildOrUploadActive()
        {
            try
            {
                var builderTypes = new string[]
                {
                    "VRC.SDK3.Editor.VRCSdkControlPanel+AvatarBuilderTab, VRC.SDK3.Editor",
                    "VRC.SDK3.Editor.VRCBuilderApi, VRC.SDK3.Editor",
                    "VRC.SDK3.Editor.VRCAvatarBuilder, VRC.SDK3.Editor"
                };

                foreach (string typeName in builderTypes)
                {
                    var builderType = System.Type.GetType(typeName);
                    if (builderType != null)
                    {
                        string[] stateFields = {
                            "_buildInProgress", "_uploadInProgress", "buildInProgress", "uploadInProgress",
                            "_isBuilding", "_isUploading", "isBuilding", "isUploading",
                            "_busy", "busy", "_working", "working"
                        };

                        foreach (string fieldName in stateFields)
                        {
                            var field = builderType.GetField(fieldName,
                                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                            if (field != null && field.FieldType == typeof(bool))
                            {
                                bool isActive = false;
                                if (field.IsStatic)
                                {
                                    isActive = (bool)field.GetValue(null);
                                }
                                else
                                {
                                    var instances = Resources.FindObjectsOfTypeAll(builderType);
                                    if (instances.Length > 0)
                                    {
                                        isActive = (bool)field.GetValue(instances[0]);
                                    }
                                }

                                if (isActive)
                                {
                                    LogInfo($"VRC build/upload detected via {builderType.Name}.{fieldName}");
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                LogError($"Error checking VRC build/upload state: {e.Message}");
                return false;
            }
        }

        private static bool IsVRCSDKInUploadMode()
        {
            try
            {
                var controlPanelType = System.Type.GetType("VRC.SDK3.Editor.VRCSdkControlPanel, VRC.SDK3.Editor");
                if (controlPanelType != null)
                {
                    var hasOpenInstancesMethod = typeof(EditorWindow).GetMethod("HasOpenInstances");
                    if (hasOpenInstancesMethod != null)
                    {
                        var genericMethod = hasOpenInstancesMethod.MakeGenericMethod(controlPanelType);
                        bool isOpen = (bool)genericMethod.Invoke(null, null);

                        if (isOpen)
                        {
                            var getWindowMethod = typeof(EditorWindow).GetMethod("GetWindow", new System.Type[] { typeof(bool) });
                            if (getWindowMethod != null)
                            {
                                var genericGetWindow = getWindowMethod.MakeGenericMethod(controlPanelType);
                                var window = genericGetWindow.Invoke(null, new object[] { false });

                                if (window != null)
                                {
                                    var fields = controlPanelType.GetFields(System.Reflection.BindingFlags.Instance |
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                                    foreach (var field in fields)
                                    {
                                        string fieldName = field.Name.ToLower();
                                        if (fieldName.Contains("tab") || fieldName.Contains("mode") || fieldName.Contains("state"))
                                        {
                                            var value = field.GetValue(window);
                                            if (value != null)
                                            {
                                                string valueStr = value.ToString().ToLower();
                                                if (valueStr.Contains("avatar") || valueStr.Contains("upload") || valueStr.Contains("build"))
                                                {
                                                    LogInfo($"VRC SDK Control Panel in upload mode: {field.Name} = {value}");
                                                    return true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                LogError($"Error checking VRC SDK upload mode: {e.Message}");
                return false;
            }
        }

        private static bool HasValidUploadAvatar()
        {
            try
            {
#if VRC_SDK_VRCSDK3
                var avatarDescriptors = Object.FindObjectsOfType<VRCAvatarDescriptor>();

                if (avatarDescriptors.Length == 1)
                {
                    var descriptor = avatarDescriptors[0];

                    if (descriptor.transform.parent == null && 
                        descriptor.ViewPosition != Vector3.zero &&
                        !string.IsNullOrEmpty(descriptor.name))
                    {
                        if (Selection.activeGameObject == descriptor.gameObject)
                        {
                            return true;
                        }

                        var allObjects = Object.FindObjectsOfType<GameObject>()
                            .Where(go => go.scene.IsValid() && go.transform.parent == null)
                            .ToArray();

                        if (allObjects.Length <= 3)
                        {
                            return true;
                        }
                    }
                }
#endif

                return false;
            }
            catch (Exception e)
            {
                LogError($"Error checking valid upload avatar: {e.Message}");
                return false;
            }
        }

        private static bool HasSceneChanged()
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();
                string currentSceneName = activeScene.name ?? "";

                bool changed = currentSceneName != lastActiveSceneName;
                if (changed)
                {
                    LogInfo($"Scene changed: '{lastActiveSceneName}' -> '{currentSceneName}'");
                }

                return changed;
            }
            catch (Exception e)
            {
                LogError($"Error checking scene change: {e.Message}");
                return false;
            }
        }

        private static bool HasSignificantHierarchyChange()
        {
            try
            {
                var allObjects = Object.FindObjectsOfType<GameObject>()
                    .Where(go => go.scene.IsValid() && go.scene == SceneManager.GetActiveScene())
                    .ToArray();

                int currentCount = allObjects.Length;

                bool significantChange = Math.Abs(currentCount - lastGameObjectCount) > 10;

                if (significantChange)
                {
                    LogInfo($"Significant hierarchy change detected: {lastGameObjectCount} -> {currentCount} objects");
                }

                return significantChange;
            }
            catch (Exception e)
            {
                LogError($"Error checking hierarchy change: {e.Message}");
                return false;
            }
        }

        private static void RefreshSceneCache()
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();
                lastActiveSceneName = activeScene.name ?? "";

                var allObjects = Object.FindObjectsOfType<GameObject>()
                    .Where(go => go.scene.IsValid() && go.scene == SceneManager.GetActiveScene())
                    .ToArray();
                lastGameObjectCount = allObjects.Length;
            }
            catch (Exception e)
            {
                LogError($"Error refreshing scene cache: {e.Message}");
            }
        }

        private static void CheckAvatarUploadTimeout()
        {
            try
            {
                if (isAvatarUploadDetected &&
                    avatarUploadStartTime != DateTime.MinValue &&
                    DateTime.Now - avatarUploadStartTime > AVATAR_UPLOAD_TIMEOUT)
                {
                    LogWarning($"Avatar upload timeout reached ({AVATAR_UPLOAD_TIMEOUT.TotalMinutes} minutes), resetting state");
                    isAvatarUploadDetected = false;
                    avatarUploadStartTime = DateTime.MinValue;
                    DetectAndUpdateCurrentState();
                }
            }
            catch (Exception e)
            {
                LogError($"Error checking avatar upload timeout: {e.Message}");
            }
        }

        private static void UpdateRpcState(RpcState newState, string reason)
        {
            try
            {
                if (newState == lastDetectedState)
                    return;

                LogInfo($"Updating RPC state: {lastDetectedState.StateName()} -> {newState.StateName()} ({reason})");

                lastDetectedState = newState;

                if (DiscordRPCSerializer.IsEnabled && DiscordRPCSerializer.IsInitialized)
                {
                    DiscordRPCSerializer.UpdateState(newState);
                    DiscordRPCSerializer.ResetTime();
                }
                else
                {
                    LogWarning("DiscordRPCSerializer is not available for state update");
                }
            }
            catch (Exception e)
            {
                LogError($"Error updating RPC state: {e.Message}");
            }
        }

        /// <summary>
        /// Get the current avatar name for RPC display
        /// </summary>
        public static string GetCurrentAvatarName()
        {
            try
            {
#if VRC_SDK_VRCSDK3
                if (HasVrcSdkInScene())
                {
                    if (Selection.activeGameObject != null)
                    {
                        VRCAvatarDescriptor selectedDescriptor = Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>();
                        if (selectedDescriptor != null)
                        {
                            string avatarName = Selection.activeGameObject.name;
                            LogInfo($"Using selected avatar: {avatarName}");
                            return avatarName;
                        }
                    }

                    var avatarDescriptors = Object.FindObjectsOfType<VRCAvatarDescriptor>();
                    if (avatarDescriptors.Length > 0)
                    {
                        foreach (var descriptor in avatarDescriptors)
                        {
                            if (descriptor.transform.parent == null ||
                                descriptor.transform.root == descriptor.transform)
                            {
                                string avatarName = descriptor.gameObject.name;
                                LogInfo($"Using root avatar: {avatarName}");
                                return avatarName;
                            }
                        }

                        string fallbackName = avatarDescriptors[0].gameObject.name;
                        LogInfo($"Using first available avatar: {fallbackName}");
                        return fallbackName;
                    }

                    string customAvatarName = GetCustomAvatarName();
                    if (!string.IsNullOrEmpty(customAvatarName))
                    {
                        LogInfo($"Using custom avatar name: {customAvatarName}");
                        return customAvatarName;
                    }

                    return "Unknown Avatar";
                }
                else
#endif
                {
                    string projectName = GetProjectName();
                    LogInfo($"No VRC SDK detected, using project name: {projectName}");
                    return projectName;
                }
            }
            catch (Exception e)
            {
                LogError($"Error getting avatar name: {e.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Get the appropriate content label (Avatar: or Project:)
        /// </summary>
        public static string GetContentLabel()
        {
            try
            {
#if VRC_SDK_VRCSDK3
                return HasVrcSdkInScene() ? "Avatar" : "Project";
#else
                return "Project";
#endif
            }
            catch
            {
                return "Project";
            }
        }

        /// <summary>
        /// Get custom avatar name from preferences
        /// </summary>
        private static string GetCustomAvatarName()
        {
            try
            {
                string prefKey = $"SDKTools.{GetProjectKeyLength()}.AvatarName";
                return EditorPrefs.GetString(prefKey, "");
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get project name
        /// </summary>
        private static string GetProjectName()
        {
            try
            {
                return PlayerSettings.productName ?? "Unity Project";
            }
            catch
            {
                return "Unity Project";
            }
        }

        /// <summary>
        /// Get project key length for preferences
        /// </summary>
        private static int GetProjectKeyLength()
        {
            try
            {
                return PlayerSettings.productName?.Length + 1 ?? 1;
            }
            catch
            {
                return 1;
            }
        }

        #region Public API

        public static RpcState GetCurrentState() => lastDetectedState;

        public static bool IsAvatarUploadActive() => isAvatarUploadDetected;

        public static void ForceStateRefresh()
        {
            try
            {
                LogInfo("Force refreshing RPC state");
                RefreshSceneCache();
                DetectAndUpdateCurrentState();
            }
            catch (Exception e)
            {
                LogError($"Error during force state refresh: {e.Message}");
            }
        }

        public static void ResetAvatarUploadDetection()
        {
            try
            {
                LogInfo("Resetting avatar upload detection");
                isAvatarUploadDetected = false;
                avatarUploadStartTime = DateTime.MinValue;
                DetectAndUpdateCurrentState();
            }
            catch (Exception e)
            {
                LogError($"Error resetting avatar upload detection: {e.Message}");
            }
        }

        #endregion

        #region Logging

        private static void LogInfo(string message)
        {
            Debug.Log($"[SDKTools] DiscordRpcRuntimeHelper: {message}");
        }

        private static void LogWarning(string message)
        {
            Debug.LogWarning($"[SDKTools] DiscordRpcRuntimeHelper: {message}");
        }

        private static void LogError(string message)
        {
            Debug.LogError($"[SDKTools] DiscordRpcRuntimeHelper: {message}");
        }

        #endregion
    }
}
#endif