using System;
using System.Collections.Generic;
using UnityEngine;

namespace SDKTools
{
    /// <summary>
    /// Enhanced RPC State management with detailed information and extensibility
    /// </summary>
    public static class RpcStateInfo
    {
        private static readonly Dictionary<RpcState, StateInfo> stateInfoCache = new Dictionary<RpcState, StateInfo>();

        public static event Action<RpcState, RpcState> OnStateChanged;
        public static event Action<RpcState> OnStateEntered;
        public static event Action<RpcState> OnStateExited;

        private static RpcState? lastNotifiedState = null;

        static RpcStateInfo()
        {
            InitializeStateInfo();
        }

        /// <summary>
        /// Get the display name for a given RPC state
        /// </summary>
        public static string StateName(this RpcState state)
        {
            return GetStateInfo(state).displayName;
        }

        /// <summary>
        /// Get the detailed description for a given RPC state
        /// </summary>
        public static string StateDescription(this RpcState state)
        {
            return GetStateInfo(state).description;
        }

        /// <summary>
        /// Get the icon/emoji for a given RPC state
        /// </summary>
        public static string StateIcon(this RpcState state)
        {
            return GetStateInfo(state).icon;
        }

        /// <summary>
        /// Get the priority level for a given RPC state (higher = more important)
        /// </summary>
        public static int StatePriority(this RpcState state)
        {
            return GetStateInfo(state).priority;
        }

        /// <summary>
        /// Get the associated color for a given RPC state
        /// </summary>
        public static Color StateColor(this RpcState state)
        {
            return GetStateInfo(state).color;
        }

        /// <summary>
        /// Check if a state represents an active/working state
        /// </summary>
        public static bool IsActiveState(this RpcState state)
        {
            return GetStateInfo(state).isActive;
        }

        /// <summary>
        /// Check if a state allows user interaction
        /// </summary>
        public static bool AllowsInteraction(this RpcState state)
        {
            return GetStateInfo(state).allowsInteraction;
        }

        /// <summary>
        /// Get formatted string with icon and name
        /// </summary>
        public static string StateDisplayText(this RpcState state)
        {
            var info = GetStateInfo(state);
            return $"{info.icon} {info.displayName}";
        }

        /// <summary>
        /// Get detailed status string for UI display
        /// </summary>
        public static string StateStatusText(this RpcState state)
        {
            var info = GetStateInfo(state);
            return $"{info.icon} {info.displayName} - {info.description}";
        }

        /// <summary>
        /// Get all available states with their information
        /// </summary>
        public static IEnumerable<(RpcState state, StateInfo info)> GetAllStates()
        {
            foreach (var kvp in stateInfoCache)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Get states sorted by priority (highest first)
        /// </summary>
        public static IEnumerable<RpcState> GetStatesByPriority()
        {
            var sortedStates = new List<(RpcState state, int priority)>();

            foreach (var kvp in stateInfoCache)
            {
                sortedStates.Add((kvp.Key, kvp.Value.priority));
            }

            sortedStates.Sort((a, b) => b.priority.CompareTo(a.priority));

            foreach (var item in sortedStates)
            {
                yield return item.state;
            }
        }

        /// <summary>
        /// Check if state transition is valid
        /// </summary>
        public static bool IsValidTransition(RpcState from, RpcState to)
        {
            var fromInfo = GetStateInfo(from);
            var toInfo = GetStateInfo(to);

            if (to == RpcState.ERROR || from == RpcState.ERROR)
                return true;

            return fromInfo.allowedTransitions.Contains(to);
        }

        /// <summary>
        /// Get suggested next states from current state
        /// </summary>
        public static IEnumerable<RpcState> GetSuggestedTransitions(RpcState currentState)
        {
            return GetStateInfo(currentState).allowedTransitions;
        }

        /// <summary>
        /// Notify about state change (for event system integration)
        /// </summary>
        public static void NotifyStateChange(RpcState newState)
        {
            try
            {
                RpcState? previousState = lastNotifiedState;

                if (previousState != newState)
                {
                    if (previousState.HasValue)
                    {
                        OnStateExited?.Invoke(previousState.Value);
                    }

                    if (previousState.HasValue)
                    {
                        OnStateChanged?.Invoke(previousState.Value, newState);
                    }

                    OnStateEntered?.Invoke(newState);

                    lastNotifiedState = newState;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SDKTools] Error in state change notification: {e.Message}");
            }
        }

        /// <summary>
        /// Get time-based state text (e.g., "Working for 5 minutes")
        /// </summary>
        public static string GetTimedStateText(RpcState state, TimeSpan duration)
        {
            var info = GetStateInfo(state);
            string timeText = FormatDuration(duration);

            return state switch
            {
                RpcState.EDITMODE => $"{info.icon} Editing for {timeText}",
                RpcState.PLAYMODE => $"{info.icon} Testing for {timeText}",
                RpcState.UPLOADAVATAR => $"{info.icon} Uploading for {timeText}",
                RpcState.ERROR => $"{info.icon} Error state for {timeText}",
                _ => $"{info.icon} {info.displayName} for {timeText}"
            };
        }

        /// <summary>
        /// Format duration for display
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 1)
                return $"{duration.Seconds}s";
            else if (duration.TotalHours < 1)
                return $"{duration.Minutes}m {duration.Seconds}s";
            else
                return $"{duration.Hours}h {duration.Minutes}m";
        }

        private static void InitializeStateInfo()
        {
            stateInfoCache[RpcState.EDITMODE] = new StateInfo
            {
                displayName = "in Edit Mode",
                description = "Actively editing the project in Unity Editor",
                icon = "✏️",
                priority = 1,
                color = new Color(0.2f, 0.8f, 0.2f, 1f),
                isActive = true,
                allowsInteraction = true,
                allowedTransitions = new HashSet<RpcState> { RpcState.PLAYMODE, RpcState.ERROR }
            };

            stateInfoCache[RpcState.PLAYMODE] = new StateInfo
            {
                displayName = "in Play Mode",
                description = "Testing the project in Unity's Play Mode",
                icon = "▶️",
                priority = 2,
                color = new Color(0.2f, 0.6f, 1f, 1f),
                isActive = true,
                allowsInteraction = true,
                allowedTransitions = new HashSet<RpcState> { RpcState.EDITMODE, RpcState.UPLOADAVATAR, RpcState.ERROR }
            };

            stateInfoCache[RpcState.UPLOADAVATAR] = new StateInfo
            {
                displayName = "Uploading Avatar",
                description = "Uploading VRChat avatar to VRC servers",
                icon = "📤",
                priority = 3,
                color = new Color(1f, 0.6f, 0.2f, 1f),
                isActive = true,
                allowsInteraction = false,
                allowedTransitions = new HashSet<RpcState> { RpcState.EDITMODE, RpcState.PLAYMODE, RpcState.ERROR }
            };

            stateInfoCache[RpcState.ERROR] = new StateInfo
            {
                displayName = "Error State",
                description = "An error occurred in the RPC system",
                icon = "❌",
                priority = 10,
                color = new Color(1f, 0.2f, 0.2f, 1f),
                isActive = false,
                allowsInteraction = true,
                allowedTransitions = new HashSet<RpcState> { RpcState.EDITMODE, RpcState.PLAYMODE, RpcState.UPLOADAVATAR }
            };
        }

        private static StateInfo GetStateInfo(RpcState state)
        {
            if (stateInfoCache.TryGetValue(state, out StateInfo info))
            {
                return info;
            }

            return new StateInfo
            {
                displayName = state.ToString(),
                description = $"Unknown state: {state}",
                icon = "❓",
                priority = 0,
                color = Color.gray,
                isActive = false,
                allowsInteraction = true,
                allowedTransitions = new HashSet<RpcState>()
            };
        }

        /// <summary>
        /// Comprehensive state information
        /// </summary>
        public class StateInfo
        {
            public string displayName;
            public string description;
            public string icon;
            public int priority;
            public Color color;
            public bool isActive;
            public bool allowsInteraction;
            public HashSet<RpcState> allowedTransitions;
        }

        #region Validation and Diagnostics

        /// <summary>
        /// Validate state enum completeness
        /// </summary>
        public static bool ValidateStateCompleteness()
        {
            try
            {
                var enumValues = Enum.GetValues(typeof(RpcState));
                foreach (RpcState state in enumValues)
                {
                    if (!stateInfoCache.ContainsKey(state))
                    {
                        Debug.LogWarning($"[SDKTools] RpcState {state} is missing from StateInfo cache");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SDKTools] Error validating state completeness: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get diagnostic information about all states
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            try
            {
                var info = "RPC State Diagnostic Information:\n";
                info += $"Total States: {stateInfoCache.Count}\n";
                info += $"Validation: {(ValidateStateCompleteness() ? "PASS" : "FAIL")}\n\n";

                foreach (var kvp in stateInfoCache)
                {
                    var state = kvp.Key;
                    var stateInfo = kvp.Value;
                    info += $"{state}: {stateInfo.displayName}\n";
                    info += $"  Priority: {stateInfo.priority}\n";
                    info += $"  Active: {stateInfo.isActive}\n";
                    info += $"  Transitions: {string.Join(", ", stateInfo.allowedTransitions)}\n\n";
                }

                return info;
            }
            catch (Exception e)
            {
                return $"Error generating diagnostic info: {e.Message}";
            }
        }

        #endregion
    }

    /// <summary>
    /// Enhanced RPC State enumeration with additional states for future expansion
    /// </summary>
    public enum RpcState
    {
        EDITMODE = 0,
        PLAYMODE = 1,
        UPLOADAVATAR = 2,
        ERROR = 99
    }
}