#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using VRC.SDKBase;
using VRC.Core;

namespace SDKTools
{
    [InitializeOnLoad]
    public static class DiscordRPCSerializer
    {
        private const string APPLICATION_ID = "956193080111923300";
        private const float UPDATE_INTERVAL = 5f;
        private const float CALLBACK_INTERVAL = 1f;

        private static DiscordRPCAPI.RichPresence presence;
        private static DateTime startTime;
        private static long timestamp;
        private static RpcState currentState = RpcState.EDITMODE;
        private static bool isInitialized = false;
        private static bool isEnabled = false;
        private static bool hasError = false;

        private static string lastContentLabel = "";
        private static string lastContentName = "";
        private static string lastProjectName = "";
        private static string lastImageKey = "";
        private static RpcState lastState = RpcState.EDITMODE;

        private static double lastUpdateTime = 0;
        private static double lastCallbackTime = 0;

        private static int consecutiveErrors = 0;
        private const int MAX_CONSECUTIVE_ERRORS = 3;
        private static DateTime lastErrorTime = DateTime.MinValue;
        private const double ERROR_COOLDOWN_MINUTES = 5;

        static DiscordRPCSerializer()
        {
            try
            {
                InitializeRPC();
                EditorApplication.update += OnEditorUpdate;
                EditorApplication.quitting += OnEditorQuitting;

                DiscordRPCAPI.OnError += OnRPCError;
                DiscordRPCAPI.OnLog += OnRPCLog;
            }
            catch (Exception e)
            {
                LogError($"Failed to initialize DiscordRPCSerializer: {e.Message}");
            }
        }

        private static void InitializeRPC()
        {
            try
            {
                if (!EditorPrefs.HasKey("discordRPC"))
                {
                    EditorPrefs.SetBool("discordRPC", true);
                }

                isEnabled = EditorPrefs.GetBool("discordRPC", true);

                if (!isEnabled)
                {
                    LogInfo("Discord RPC is disabled in preferences");
                    return;
                }

                if (hasError && (DateTime.Now - lastErrorTime).TotalMinutes < ERROR_COOLDOWN_MINUTES)
                {
                    LogInfo($"Discord RPC in error cooldown, will retry after {ERROR_COOLDOWN_MINUTES} minutes");
                    return;
                }

                var eventHandlers = new DiscordRPCAPI.EventHandlers
                {
                    readyCallback = OnReady,
                    disconnectedCallback = OnDisconnected,
                    errorCallback = OnError,
                    joinCallback = OnJoin,
                    spectateCallback = OnSpectate,
                    requestCallback = OnRequest
                };

                bool success = DiscordRPCAPI.Initialize(APPLICATION_ID, ref eventHandlers, false, string.Empty);

                if (success)
                {
                    isInitialized = true;
                    hasError = false;
                    consecutiveErrors = 0;

                    presence = new DiscordRPCAPI.RichPresence();

                    ResetTime();

                    UpdatePresenceInternal();

                    LogInfo("Discord RPC initialized successfully");
                }
                else
                {
                    LogError("Failed to initialize Discord RPC");
                    HandleInitializationError();
                }
            }
            catch (Exception e)
            {
                LogError($"Exception during RPC initialization: {e.Message}");
                HandleInitializationError();
            }
        }

        private static void OnEditorUpdate()
        {
            if (!isEnabled || !isInitialized || hasError)
                return;

            double currentTime = EditorApplication.timeSinceStartup;

            try
            {
                if (currentTime - lastCallbackTime >= CALLBACK_INTERVAL)
                {
                    DiscordRPCAPI.RunCallbacks();
                    lastCallbackTime = currentTime;
                }

                if (currentTime - lastUpdateTime >= UPDATE_INTERVAL)
                {
                    UpdatePresenceInternal();
                    lastUpdateTime = currentTime;
                }
            }
            catch (Exception e)
            {
                LogError($"Error in editor update: {e.Message}");
                HandleRuntimeError();
            }
        }

        private static void OnEditorQuitting()
        {
            try
            {
                ShutdownRPC();
            }
            catch (Exception e)
            {
                LogError($"Error during editor shutdown: {e.Message}");
            }
        }

        public static void UpdateDRPC()
        {
            if (!isEnabled)
            {
                LogInfo("Discord RPC is disabled, skipping update");
                return;
            }

            if (!isInitialized)
            {
                LogWarning("Discord RPC not initialized, attempting to initialize");
                InitializeRPC();
                return;
            }

            try
            {
                UpdatePresenceInternal();
                LogInfo("Discord RPC updated manually");
            }
            catch (Exception e)
            {
                LogError($"Error updating Discord RPC: {e.Message}");
                HandleRuntimeError();
            }
        }

        public static void UpdateDRPCImg()
        {
            if (!isEnabled || !isInitialized)
                return;

            try
            {
                UpdatePresenceInternal();
                LogInfo("Discord RPC image updated");
            }
            catch (Exception e)
            {
                LogError($"Error updating Discord RPC image: {e.Message}");
                HandleRuntimeError();
            }
        }

        public static void UpdateState(RpcState state)
        {
            if (!isEnabled || !isInitialized)
                return;

            try
            {
                LogInfo($"State update requested: {currentState.StateName()} -> {state.StateName()}");
                currentState = state;

                UpdatePresenceInternal();

                LogInfo($"Discord RPC state updated to: {state.StateName()}");
            }
            catch (Exception e)
            {
                LogError($"Error updating Discord RPC state: {e.Message}");
                HandleRuntimeError();
            }
        }

        public static void ResetTime()
        {
            try
            {
                startTime = DateTime.UtcNow;
                timestamp = ((DateTimeOffset)startTime).ToUnixTimeSeconds();

                if (presence != null && isInitialized && isEnabled)
                {
                    presence.startTimestamp = timestamp;
                    UpdatePresenceInternal();
                }

                LogInfo("Discord RPC timestamp reset");
            }
            catch (Exception e)
            {
                LogError($"Error resetting Discord RPC time: {e.Message}");
            }
        }

        private static void UpdatePresenceInternal()
        {
            if (presence == null || !isInitialized || !isEnabled)
                return;

            try
            {
                string contentLabel = GetContentLabel();
                string contentName = GetCurrentAvatarName();
                string projectName = GetCurrentProjectName();
                string imageKey = GetCurrentImageKey();

                presence.details = $"{contentLabel}: {contentName}";
                presence.state = $"Currently {currentState.StateName()}";
                presence.startTimestamp = timestamp;
                presence.largeImageKey = imageKey;
                presence.largeImageText = projectName;
                presence.smallImageKey = "";
                presence.smallImageText = "";
                presence.instance = false;

                bool success = DiscordRPCAPI.UpdatePresence(presence);

                if (success)
                {
                    LogInfo($"RPC Updated: {presence.details} | {presence.state}");
                }
                else
                {
                    LogWarning("Failed to update Discord RPC presence");
                    HandleRuntimeError();
                }
            }
            catch (Exception e)
            {
                LogError($"Error in UpdatePresenceInternal: {e.Message}");
                HandleRuntimeError();
            }
        }

        private static string GetCurrentAvatarName()
        {
            try
            {
                return DiscordRpcRuntimeHelper.GetCurrentAvatarName();
            }
            catch (Exception e)
            {
                LogError($"Error getting avatar name: {e.Message}");
                return "Unknown";
            }
        }

        private static string GetContentLabel()
        {
            try
            {
                return DiscordRpcRuntimeHelper.GetContentLabel();
            }
            catch (Exception e)
            {
                LogError($"Error getting content label: {e.Message}");
                return "Project";
            }
        }

        private static string GetCurrentProjectName()
        {
            try
            {
                string projectName = PlayerSettings.productName;
                return string.IsNullOrWhiteSpace(projectName) ? "Unity Project" : projectName.Trim();
            }
            catch (Exception e)
            {
                LogError($"Error getting project name: {e.Message}");
                return "Unity Project";
            }
        }

        private static string GetCurrentImageKey()
        {
            try
            {
                string prefKey = $"SDKTools.{GetProjectKeyLength()}.CurrentRPCImage";
                string imageKey = EditorPrefs.GetString(prefKey, "default");

                return string.IsNullOrWhiteSpace(imageKey) ? "default" : imageKey.Trim().ToLowerInvariant();
            }
            catch (Exception e)
            {
                LogError($"Error getting image key: {e.Message}");
                return "default";
            }
        }

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

        private static void ShutdownRPC()
        {
            if (!isInitialized)
                return;

            try
            {
                LogInfo("Shutting down Discord RPC");

                if (DiscordRPCAPI.IsInitialized)
                {
                    DiscordRPCAPI.ClearPresence();

                    for (int i = 0; i < 10; i++)
                    {
                        DiscordRPCAPI.RunCallbacks();
                        System.Threading.Thread.Sleep(10);
                    }
                }

                DiscordRPCAPI.Shutdown();

                presence?.FreeMem();
                presence = null;
                isInitialized = false;

                LogInfo("Discord RPC shutdown complete");
            }
            catch (Exception e)
            {
                LogError($"Error during RPC shutdown: {e.Message}");
            }
        }

        private static void HandleInitializationError()
        {
            consecutiveErrors++;
            hasError = true;
            lastErrorTime = DateTime.Now;
            isInitialized = false;

            if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
            {
                LogError($"Discord RPC failed to initialize {MAX_CONSECUTIVE_ERRORS} times, entering cooldown period");
            }
        }

        private static void HandleRuntimeError()
        {
            consecutiveErrors++;

            if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
            {
                LogError("Too many consecutive errors, shutting down Discord RPC");
                hasError = true;
                lastErrorTime = DateTime.Now;
                ShutdownRPC();
            }
        }

        #region RPC Event Handlers

        private static void OnReady(ref DiscordRPCAPI.DiscordUser user)
        {
            LogInfo($"Discord RPC ready for user: {user.username}#{user.discriminator}");
            consecutiveErrors = 0;
            hasError = false;

            UpdatePresenceInternal();
        }

        private static void OnDisconnected(int errorCode, string message)
        {
            LogWarning($"Discord RPC disconnected - Code: {errorCode}, Message: {message}");

            if (errorCode != 0)
            {
                HandleRuntimeError();
            }
        }

        private static void OnError(int errorCode, string message)
        {
            LogError($"Discord RPC error - Code: {errorCode}, Message: {message}");
            HandleRuntimeError();
        }

        private static void OnJoin(string secret)
        {
            LogInfo($"Discord RPC join request received");
        }

        private static void OnSpectate(string secret)
        {
            LogInfo($"Discord RPC spectate request received");
        }

        private static void OnRequest(ref DiscordRPCAPI.DiscordUser user)
        {
            LogInfo($"Discord RPC friend request from: {user.username}#{user.discriminator}");
        }

        #endregion

        #region External Event Handlers

        private static void OnRPCError(string error)
        {
            LogError($"RPC API Error: {error}");
        }

        private static void OnRPCLog(string message)
        {
            LogInfo($"RPC API: {message}");
        }

        #endregion

        #region Logging

        private static void LogInfo(string message)
        {
            Debug.Log($"[SDKTools] DiscordRPC: {message}");
        }

        private static void LogWarning(string message)
        {
            Debug.LogWarning($"[SDKTools] DiscordRPC: {message}");
        }

        private static void LogError(string message)
        {
            Debug.LogError($"[SDKTools] DiscordRPC: {message}");
        }

        #endregion

        #region Public Properties

        public static bool IsEnabled => isEnabled;
        public static bool IsInitialized => isInitialized;
        public static bool HasError => hasError;
        public static RpcState CurrentState => currentState;
        public static DateTime StartTime => startTime;

        #endregion

        #region Public Methods

        public static void SetEnabled(bool enabled)
        {
            if (isEnabled == enabled)
                return;

            isEnabled = enabled;
            EditorPrefs.SetBool("discordRPC", enabled);

            if (enabled && !isInitialized)
            {
                InitializeRPC();
            }
            else if (!enabled && isInitialized)
            {
                ShutdownRPC();
            }

            LogInfo($"Discord RPC {(enabled ? "enabled" : "disabled")}");
        }

        public static void ForceRefresh()
        {
            try
            {
                if (!isEnabled)
                {
                    LogInfo("Cannot refresh - Discord RPC is disabled");
                    return;
                }

                if (!isInitialized)
                {
                    LogInfo("Reinitializing Discord RPC");
                    InitializeRPC();
                    return;
                }

                UpdatePresenceInternal();
                LogInfo("Discord RPC force refreshed");
            }
            catch (Exception e)
            {
                LogError($"Error during force refresh: {e.Message}");
                HandleRuntimeError();
            }
        }

        public static void ClearErrorState()
        {
            hasError = false;
            consecutiveErrors = 0;
            lastErrorTime = DateTime.MinValue;
            LogInfo("Discord RPC error state cleared");
        }

        #endregion
    }
}
#endif