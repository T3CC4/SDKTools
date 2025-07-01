using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using UnityEngine;

namespace SDKTools
{
    public static class DiscordRPCAPI
    {
        private static readonly object callbackLock = new object();
        private static EventHandlers? callbacks;
        private static bool isInitialized = false;
        private static bool isShuttingDown = false;

        private static readonly Queue<IntPtr> memoryPool = new Queue<IntPtr>();
        private const int MAX_POOLED_POINTERS = 50;

        public static event Action<string> OnError;
        public static event Action<string> OnLog;

        #region Callback Delegates

        [MonoPInvokeCallback(typeof(OnReadyInfo))]
        public static void ReadyCallback(ref DiscordUser connectedUser)
        {
            if (isShuttingDown) return;

            try
            {
                lock (callbackLock)
                {
                    callbacks?.readyCallback?.Invoke(ref connectedUser);
                }
                LogInfo($"Discord RPC connected for user: {connectedUser.username}");
            }
            catch (Exception e)
            {
                LogError($"Error in ReadyCallback: {e.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(OnDisconnectedInfo))]
        public static void DisconnectedCallback(int errorCode, string message)
        {
            if (isShuttingDown) return;

            try
            {
                lock (callbackLock)
                {
                    callbacks?.disconnectedCallback?.Invoke(errorCode, message);
                }
                LogWarning($"Discord RPC disconnected. Error: {errorCode}, Message: {message}");
            }
            catch (Exception e)
            {
                LogError($"Error in DisconnectedCallback: {e.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(OnErrorInfo))]
        public static void ErrorCallback(int errorCode, string message)
        {
            try
            {
                lock (callbackLock)
                {
                    callbacks?.errorCallback?.Invoke(errorCode, message);
                }
                LogError($"Discord RPC error: {errorCode}, Message: {message}");
            }
            catch (Exception e)
            {
                LogError($"Error in ErrorCallback: {e.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(OnJoinInfo))]
        public static void JoinCallback(string secret)
        {
            if (isShuttingDown) return;

            try
            {
                lock (callbackLock)
                {
                    callbacks?.joinCallback?.Invoke(secret);
                }
                LogInfo($"Discord RPC join request received");
            }
            catch (Exception e)
            {
                LogError($"Error in JoinCallback: {e.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(OnSpectateInfo))]
        public static void SpectateCallback(string secret)
        {
            if (isShuttingDown) return;

            try
            {
                lock (callbackLock)
                {
                    callbacks?.spectateCallback?.Invoke(secret);
                }
                LogInfo($"Discord RPC spectate request received");
            }
            catch (Exception e)
            {
                LogError($"Error in SpectateCallback: {e.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(OnRequestInfo))]
        public static void RequestCallback(ref DiscordUser request)
        {
            if (isShuttingDown) return;

            try
            {
                lock (callbackLock)
                {
                    callbacks?.requestCallback?.Invoke(ref request);
                }
                LogInfo($"Discord RPC request from user: {request.username}");
            }
            catch (Exception e)
            {
                LogError($"Error in RequestCallback: {e.Message}");
            }
        }

        #endregion

        #region Delegate Definitions

        public delegate void OnReadyInfo(ref DiscordUser connectedUser);
        public delegate void OnDisconnectedInfo(int errorCode, string message);
        public delegate void OnErrorInfo(int errorCode, string message);
        public delegate void OnJoinInfo(string secret);
        public delegate void OnSpectateInfo(string secret);
        public delegate void OnRequestInfo(ref DiscordUser request);

        #endregion

        #region Structures

        public struct EventHandlers
        {
            public OnReadyInfo readyCallback;
            public OnDisconnectedInfo disconnectedCallback;
            public OnErrorInfo errorCallback;
            public OnJoinInfo joinCallback;
            public OnSpectateInfo spectateCallback;
            public OnRequestInfo requestCallback;
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct RichPresenceStruct
        {
            public IntPtr state; /* max 128 bytes */
            public IntPtr details; /* max 128 bytes */
            public long startTimestamp;
            public long endTimestamp;
            public IntPtr largeImageKey; /* max 32 bytes */
            public IntPtr largeImageText; /* max 128 bytes */
            public IntPtr smallImageKey; /* max 32 bytes */
            public IntPtr smallImageText; /* max 128 bytes */
            public IntPtr partyId; /* max 128 bytes */
            public int partySize;
            public int partyMax;
            public IntPtr matchSecret; /* max 128 bytes */
            public IntPtr joinSecret; /* max 128 bytes */
            public IntPtr spectateSecret; /* max 128 bytes */
            public bool instance;
        }

        [Serializable]
        public struct DiscordUser
        {
            public string userId;
            public string username;
            public string discriminator;
            public string avatar;
        }

        public enum Reply
        {
            No = 0,
            Yes = 1,
            Ignore = 2
        }

        #endregion

        #region Public API

        public static bool Initialize(string applicationId, ref EventHandlers handlers, bool autoRegister = true, string optionalSteamId = "")
        {
            if (isInitialized)
            {
                LogWarning("Discord RPC already initialized");
                return true;
            }

            if (string.IsNullOrEmpty(applicationId))
            {
                LogError("Application ID cannot be null or empty");
                return false;
            }

            try
            {
                isShuttingDown = false;

                lock (callbackLock)
                {
                    callbacks = handlers;
                }

                EventHandlers staticEventHandlers = new EventHandlers
                {
                    readyCallback = ReadyCallback,
                    disconnectedCallback = DisconnectedCallback,
                    errorCallback = ErrorCallback,
                    joinCallback = JoinCallback,
                    spectateCallback = SpectateCallback,
                    requestCallback = RequestCallback
                };

                InitializeInternal(applicationId, ref staticEventHandlers, autoRegister, optionalSteamId ?? "");
                isInitialized = true;

                LogInfo($"Discord RPC initialized with Application ID: {applicationId}");
                return true;
            }
            catch (Exception e)
            {
                LogError($"Failed to initialize Discord RPC: {e.Message}");
                isInitialized = false;
                return false;
            }
        }

        public static void Shutdown()
        {
            if (!isInitialized)
            {
                LogWarning("Discord RPC not initialized, cannot shutdown");
                return;
            }

            try
            {
                isShuttingDown = true;

                ClearPresence();

                ShutdownInternal();

                lock (callbackLock)
                {
                    callbacks = null;
                }

                CleanupMemoryPool();

                isInitialized = false;
                LogInfo("Discord RPC shut down successfully");
            }
            catch (Exception e)
            {
                LogError($"Error during Discord RPC shutdown: {e.Message}");
            }
            finally
            {
                isShuttingDown = false;
                isInitialized = false;
            }
        }

        public static void RunCallbacks()
        {
            if (!isInitialized || isShuttingDown)
                return;

            try
            {
                RunCallbacksInternal();
            }
            catch (Exception e)
            {
                LogError($"Error running Discord RPC callbacks: {e.Message}");
            }
        }

        public static bool UpdatePresence(RichPresence presence)
        {
            if (!isInitialized)
            {
                LogError("Discord RPC not initialized");
                return false;
            }

            if (isShuttingDown)
            {
                LogWarning("Cannot update presence during shutdown");
                return false;
            }

            if (presence == null)
            {
                LogError("Presence cannot be null");
                return false;
            }

            try
            {
                var presenceStruct = presence.GetStruct();
                UpdatePresenceNative(ref presenceStruct);
                presence.FreeMem();

                LogInfo("Discord RPC presence updated successfully");
                return true;
            }
            catch (Exception e)
            {
                LogError($"Failed to update Discord RPC presence: {e.Message}");
                presence?.FreeMem();
                return false;
            }
        }

        public static void ClearPresence()
        {
            if (!isInitialized)
            {
                LogWarning("Discord RPC not initialized, cannot clear presence");
                return;
            }

            try
            {
                ClearPresenceInternal();
                LogInfo("Discord RPC presence cleared");
            }
            catch (Exception e)
            {
                LogError($"Error clearing Discord RPC presence: {e.Message}");
            }
        }

        public static void Respond(string userId, Reply reply)
        {
            if (!isInitialized)
            {
                LogError("Discord RPC not initialized");
                return;
            }

            if (string.IsNullOrEmpty(userId))
            {
                LogError("User ID cannot be null or empty");
                return;
            }

            try
            {
                RespondInternal(userId, reply);
                LogInfo($"Discord RPC response sent: {reply} to user {userId}");
            }
            catch (Exception e)
            {
                LogError($"Error responding to Discord RPC request: {e.Message}");
            }
        }

        public static bool IsInitialized => isInitialized;
        public static bool IsShuttingDown => isShuttingDown;

        #endregion

        #region Native Imports

        [DllImport("discord-rpc", EntryPoint = "Discord_Initialize", CallingConvention = CallingConvention.Cdecl)]
        private static extern void InitializeInternal(string applicationId, ref EventHandlers handlers, bool autoRegister, string optionalSteamId);

        [DllImport("discord-rpc", EntryPoint = "Discord_Shutdown", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ShutdownInternal();

        [DllImport("discord-rpc", EntryPoint = "Discord_RunCallbacks", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RunCallbacksInternal();

        [DllImport("discord-rpc", EntryPoint = "Discord_UpdatePresence", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UpdatePresenceNative(ref RichPresenceStruct presence);

        [DllImport("discord-rpc", EntryPoint = "Discord_ClearPresence", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ClearPresenceInternal();

        [DllImport("discord-rpc", EntryPoint = "Discord_Respond", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RespondInternal(string userId, Reply reply);

        #endregion

        #region Memory Management

        private static void CleanupMemoryPool()
        {
            lock (memoryPool)
            {
                while (memoryPool.Count > 0)
                {
                    var ptr = memoryPool.Dequeue();
                    if (ptr != IntPtr.Zero)
                    {
                        try
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                        catch (Exception e)
                        {
                            LogError($"Error freeing memory: {e.Message}");
                        }
                    }
                }
            }
        }

        private static IntPtr GetPooledPointer(int size)
        {
            lock (memoryPool)
            {
                if (memoryPool.Count > 0)
                {
                    var ptr = memoryPool.Dequeue();
                    return ptr;
                }
            }

            return Marshal.AllocHGlobal(size);
        }

        private static void ReturnPooledPointer(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return;

            lock (memoryPool)
            {
                if (memoryPool.Count < MAX_POOLED_POINTERS)
                {
                    memoryPool.Enqueue(ptr);
                }
                else
                {
                    try
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                    catch (Exception e)
                    {
                        LogError($"Error freeing excess pooled memory: {e.Message}");
                    }
                }
            }
        }

        #endregion

        #region Logging

        private static void LogInfo(string message)
        {
            try
            {
                OnLog?.Invoke($"[DiscordRPC] {message}");
#if UNITY_EDITOR || UNITY_STANDALONE
                Debug.Log($"[DiscordRPC] {message}");
#endif
            }
            catch { }
        }

        private static void LogWarning(string message)
        {
            try
            {
                OnLog?.Invoke($"[DiscordRPC] WARNING: {message}");
#if UNITY_EDITOR || UNITY_STANDALONE
                Debug.LogWarning($"[DiscordRPC] {message}");
#endif
            }
            catch { }
        }

        private static void LogError(string message)
        {
            try
            {
                OnError?.Invoke(message);
#if UNITY_EDITOR || UNITY_STANDALONE
                Debug.LogError($"[DiscordRPC] {message}");
#endif
            }
            catch { }
        }

        #endregion

        #region Rich Presence Class

        public class RichPresence
        {
            private RichPresenceStruct _presence;
            private readonly List<IntPtr> _buffers = new List<IntPtr>(10);

            private string _state;
            private string _details;
            private string _largeImageKey;
            private string _largeImageText;
            private string _smallImageKey;
            private string _smallImageText;
            private string _partyId;
            private string _matchSecret;
            private string _joinSecret;
            private string _spectateSecret;

            public string state
            {
                get => _state;
                set => _state = ValidateAndTruncateString(value, 128, "state");
            }

            public string details
            {
                get => _details;
                set => _details = ValidateAndTruncateString(value, 128, "details");
            }

            public long startTimestamp { get; set; }
            public long endTimestamp { get; set; }

            public string largeImageKey
            {
                get => _largeImageKey;
                set => _largeImageKey = ValidateAndTruncateString(value, 32, "largeImageKey");
            }

            public string largeImageText
            {
                get => _largeImageText;
                set => _largeImageText = ValidateAndTruncateString(value, 128, "largeImageText");
            }

            public string smallImageKey
            {
                get => _smallImageKey;
                set => _smallImageKey = ValidateAndTruncateString(value, 32, "smallImageKey");
            }

            public string smallImageText
            {
                get => _smallImageText;
                set => _smallImageText = ValidateAndTruncateString(value, 128, "smallImageText");
            }

            public string partyId
            {
                get => _partyId;
                set => _partyId = ValidateAndTruncateString(value, 128, "partyId");
            }

            public int partySize { get; set; }
            public int partyMax { get; set; }

            public string matchSecret
            {
                get => _matchSecret;
                set => _matchSecret = ValidateAndTruncateString(value, 128, "matchSecret");
            }

            public string joinSecret
            {
                get => _joinSecret;
                set => _joinSecret = ValidateAndTruncateString(value, 128, "joinSecret");
            }

            public string spectateSecret
            {
                get => _spectateSecret;
                set => _spectateSecret = ValidateAndTruncateString(value, 128, "spectateSecret");
            }

            public bool instance { get; set; }

            private static string ValidateAndTruncateString(string input, int maxLength, string fieldName)
            {
                if (string.IsNullOrEmpty(input))
                    return input;

                if (input.Length > maxLength)
                {
                    LogWarning($"Field '{fieldName}' truncated from {input.Length} to {maxLength} characters");
                    return input.Substring(0, maxLength);
                }

                return input;
            }

            /// <summary>
            /// Get the RichPresenceStruct representation of this instance
            /// </summary>
            internal RichPresenceStruct GetStruct()
            {
                FreeMem();

                try
                {
                    _presence.state = StrToPtr(_state);
                    _presence.details = StrToPtr(_details);
                    _presence.startTimestamp = startTimestamp;
                    _presence.endTimestamp = endTimestamp;
                    _presence.largeImageKey = StrToPtr(_largeImageKey);
                    _presence.largeImageText = StrToPtr(_largeImageText);
                    _presence.smallImageKey = StrToPtr(_smallImageKey);
                    _presence.smallImageText = StrToPtr(_smallImageText);
                    _presence.partyId = StrToPtr(_partyId);
                    _presence.partySize = Math.Max(0, partySize);
                    _presence.partyMax = Math.Max(0, partyMax);
                    _presence.matchSecret = StrToPtr(_matchSecret);
                    _presence.joinSecret = StrToPtr(_joinSecret);
                    _presence.spectateSecret = StrToPtr(_spectateSecret);
                    _presence.instance = instance;

                    return _presence;
                }
                catch (Exception e)
                {
                    LogError($"Error creating presence struct: {e.Message}");
                    FreeMem();
                    throw;
                }
            }

            /// <summary>
            /// Convert string to UTF-8 pointer with proper memory management
            /// </summary>
            private IntPtr StrToPtr(string input)
            {
                if (string.IsNullOrEmpty(input))
                    return IntPtr.Zero;

                try
                {
                    var bytes = Encoding.UTF8.GetBytes(input);
                    var buffer = Marshal.AllocHGlobal(bytes.Length + 1);

                    for (int i = 0; i < bytes.Length + 1; i++)
                    {
                        Marshal.WriteByte(buffer, i, 0);
                    }

                    Marshal.Copy(bytes, 0, buffer, bytes.Length);

                    _buffers.Add(buffer);
                    return buffer;
                }
                catch (Exception e)
                {
                    LogError($"Error converting string to pointer: {e.Message}");
                    return IntPtr.Zero;
                }
            }

            /// <summary>
            /// Free allocated memory for conversion to RichPresenceStruct
            /// </summary>
            internal void FreeMem()
            {
                var errors = new List<string>();

                for (var i = _buffers.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (_buffers[i] != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(_buffers[i]);
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add($"Buffer {i}: {e.Message}");
                    }
                }

                _buffers.Clear();

                if (errors.Count > 0)
                {
                    LogError($"Errors freeing presence memory: {string.Join(", ", errors)}");
                }
            }

            ~RichPresence()
            {
                FreeMem();
            }
        }

        #endregion
    }
}