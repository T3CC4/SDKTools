#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SDKTools
{
    public class RPCSettings : EditorWindow
    {
        public static readonly string[] rpcImgOptions = new string[] {
            "default", "drawn", "cursed", "energy", "water", "winter", "grass", "gold"
        };

        private static readonly Dictionary<string, RPCThemeInfo> themeInfo = new Dictionary<string, RPCThemeInfo>
        {
            { "default", new RPCThemeInfo("Default", "Standard SDKTools branding", new Color(0.2f, 0.5f, 0.9f)) },
            { "drawn", new RPCThemeInfo("Drawn Style", "Artistic hand-drawn appearance", new Color(0.7f, 0.6f, 0.4f)) },
            { "cursed", new RPCThemeInfo("Cursed", "Dark and mysterious theme", new Color(0.4f, 0.1f, 0.4f)) },
            { "energy", new RPCThemeInfo("Energy", "Electric and dynamic theme", new Color(0.9f, 0.9f, 0.2f)) },
            { "water", new RPCThemeInfo("Water", "Cool blue aquatic theme", new Color(0.1f, 0.6f, 0.9f)) },
            { "winter", new RPCThemeInfo("Winter", "Cold and snowy theme", new Color(0.8f, 0.9f, 1f)) },
            { "grass", new RPCThemeInfo("Grass", "Natural green theme", new Color(0.2f, 0.7f, 0.2f)) },
            { "gold", new RPCThemeInfo("Gold", "Luxurious golden theme", new Color(0.9f, 0.7f, 0.1f)) }
        };

        [System.Serializable]
        public class RPCThemeInfo
        {
            public string displayName;
            public string description;
            public Color themeColor;

            public RPCThemeInfo(string displayName, string description, Color themeColor)
            {
                this.displayName = displayName;
                this.description = description;
                this.themeColor = themeColor;
            }
        }

        private int selectedOption = 0;
        private Vector2 scrollPosition;
        private bool showAdvancedSettings = false;
        private bool showDiagnostics = false;
        private bool rpcEnabled = true;
        private string customAvatarName = "";

        private double lastUpdateTime = 0;
        private const double UPDATE_INTERVAL = 1.0;

        private GUIStyle headerStyle;
        private GUIStyle cardStyle;
        private GUIStyle selectedCardStyle;
        private GUIStyle statusStyle;

        [MenuItem("VRChat SDK/SDKTools/RPC Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<RPCSettings>("RPC Settings");
            window.minSize = new Vector2(450, 550);
            window.maxSize = new Vector2(700, 800);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
            InitializeStyles();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;

            if (GUI.changed)
            {
                ApplySettings();
            }
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - lastUpdateTime >= UPDATE_INTERVAL)
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.largeLabel)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (cardStyle == null)
            {
                cardStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(8, 8, 8, 8),
                    margin = new RectOffset(4, 4, 2, 2)
                };
            }

            if (selectedCardStyle == null)
            {
                selectedCardStyle = new GUIStyle(cardStyle);
                selectedCardStyle.normal.background = CreateColorTexture(new Color(0.3f, 0.6f, 1f, 0.3f));
            }

            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.hideFlags = HideFlags.DontSave;
            return texture;
        }

        private void OnGUI()
        {
            if (headerStyle == null) InitializeStyles();

            DrawHeader();
            DrawMainSettings();
            DrawThemeSelection();
            DrawAdvancedSettings();
            DrawDiagnostics();
            DrawActionButtons();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Discord Rich Presence Settings", headerStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Color statusColor = GetStatusColor();
            var originalColor = GUI.contentColor;
            GUI.contentColor = statusColor;

            string statusText = GetStatusText();
            EditorGUILayout.LabelField(statusText, statusStyle);

            GUI.contentColor = originalColor;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (DiscordRPCSerializer.IsEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                string currentStateText = $"Current State: {DiscordRpcRuntimeHelper.GetCurrentState().StateName()}";
                EditorGUILayout.LabelField(currentStateText, EditorStyles.miniLabel);

                if (DiscordRpcRuntimeHelper.IsAvatarUploadActive())
                {
                    GUI.contentColor = Color.yellow;
                    EditorGUILayout.LabelField("(Avatar Upload Detected)", EditorStyles.miniLabel);
                    GUI.contentColor = originalColor;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (DiscordRPCSerializer.IsInitialized)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    TimeSpan uptime = DateTime.Now - DiscordRPCSerializer.StartTime;
                    string uptimeText = $"Uptime: {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
                    EditorGUILayout.LabelField(uptimeText, EditorStyles.miniLabel);

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private Color GetStatusColor()
        {
            if (!rpcEnabled) return Color.gray;
            if (DiscordRPCSerializer.HasError) return Color.red;
            if (!DiscordRPCSerializer.IsInitialized) return Color.yellow;
            return Color.green;
        }

        private string GetStatusText()
        {
            if (!rpcEnabled) return "● RPC Disabled";
            if (DiscordRPCSerializer.HasError) return "● RPC Error";
            if (!DiscordRPCSerializer.IsInitialized) return "● RPC Initializing...";
            return "● RPC Connected";
        }

        private void DrawMainSettings()
        {
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            bool newRpcEnabled = EditorGUILayout.Toggle(
                new GUIContent("Enable Discord RPC", "Enable or disable Discord Rich Presence integration"),
                rpcEnabled);

            if (rpcEnabled && DiscordRPCSerializer.IsInitialized)
            {
                GUI.contentColor = Color.green;
                EditorGUILayout.LabelField("✓ Active", GUILayout.Width(50));
            }
            else if (rpcEnabled && !DiscordRPCSerializer.IsInitialized)
            {
                GUI.contentColor = Color.yellow;
                EditorGUILayout.LabelField("⚠ Starting", GUILayout.Width(60));
            }
            else
            {
                GUI.contentColor = Color.gray;
                EditorGUILayout.LabelField("○ Inactive", GUILayout.Width(60));
            }
            GUI.contentColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (newRpcEnabled != rpcEnabled)
            {
                rpcEnabled = newRpcEnabled;
                DiscordRPCSerializer.SetEnabled(rpcEnabled);

                if (rpcEnabled)
                {
                    ShowNotification(new GUIContent("Discord RPC Enabled"));
                }
                else
                {
                    ShowNotification(new GUIContent("Discord RPC Disabled"));
                }
            }

            EditorGUI.BeginDisabledGroup(!rpcEnabled);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Avatar Information", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            string newAvatarName = EditorGUILayout.TextField(
                new GUIContent("Avatar Name", "Custom name to display for the current avatar"),
                customAvatarName);

            int charCount = newAvatarName?.Length ?? 0;
            Color charColor = charCount > 128 ? Color.red : (charCount > 100 ? Color.yellow : Color.gray);
            GUI.contentColor = charColor;
            EditorGUILayout.LabelField($"{charCount}/128", GUILayout.Width(50));
            GUI.contentColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (newAvatarName != customAvatarName)
            {
                customAvatarName = newAvatarName;
                string prefKey = $"SDKTools.{GetProjectKeyLength()}.AvatarName";
                EditorPrefs.SetString(prefKey, customAvatarName);

                if (rpcEnabled && DiscordRPCSerializer.IsInitialized)
                {
                    DiscordRPCSerializer.UpdateDRPC();
                }
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawThemeSelection()
        {
            EditorGUILayout.LabelField("Theme Selection", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Theme:", EditorStyles.miniLabel, GUILayout.Width(85));

            string currentKey = rpcImgOptions[selectedOption];
            if (themeInfo.ContainsKey(currentKey))
            {
                var theme = themeInfo[currentKey];

                var colorRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
                EditorGUI.DrawRect(colorRect, theme.themeColor);
                EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, colorRect.width, colorRect.height), Color.black);

                GUILayout.Space(5);
                EditorGUILayout.LabelField(theme.displayName, EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                GUI.contentColor = Color.gray;
                EditorGUILayout.LabelField($"({currentKey})", EditorStyles.miniLabel, GUILayout.Width(60));
                GUI.contentColor = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField(currentKey, EditorStyles.boldLabel);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            for (int i = 0; i < rpcImgOptions.Length; i++)
            {
                string key = rpcImgOptions[i];
                bool isSelected = i == selectedOption;

                var style = isSelected ? selectedCardStyle : cardStyle;

                EditorGUILayout.BeginHorizontal(style);

                if (themeInfo.ContainsKey(key))
                {
                    var theme = themeInfo[key];
                    var colorRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));
                    EditorGUI.DrawRect(new Rect(colorRect.x - 1, colorRect.y - 1, colorRect.width + 2, colorRect.height + 2), Color.black);
                    EditorGUI.DrawRect(colorRect, theme.themeColor);

                    GUILayout.Space(8);

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(theme.displayName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(theme.description, EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.LabelField(key, EditorStyles.boldLabel);
                }

                GUILayout.FlexibleSpace();

                if (isSelected)
                {
                    var originalColor = GUI.contentColor;
                    GUI.contentColor = Color.green;
                    EditorGUILayout.LabelField("✓ Selected", EditorStyles.miniLabel, GUILayout.Width(60));
                    GUI.contentColor = originalColor;
                }
                else
                {
                    if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        SelectOption(i);
                    }
                }

                EditorGUILayout.EndHorizontal();

                var lastRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition) && !isSelected)
                {
                    SelectOption(i);
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);

            if (showAdvancedSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.indentLevel++;

                EditorGUI.BeginDisabledGroup(!rpcEnabled);

                EditorGUILayout.LabelField("Current Configuration", EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Selected Theme:", GUILayout.Width(100));
                string currentKey = rpcImgOptions[selectedOption];
                if (themeInfo.ContainsKey(currentKey))
                {
                    EditorGUILayout.LabelField(themeInfo[currentKey].displayName);
                }
                else
                {
                    EditorGUILayout.LabelField(currentKey);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Theme Key:", GUILayout.Width(100));
                EditorGUILayout.SelectableLabel(currentKey, EditorStyles.textField, GUILayout.Height(16));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Project Key:", GUILayout.Width(100));
                EditorGUILayout.LabelField(GetProjectKeyLength().ToString());
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Force Update RPC", EditorStyles.miniButton))
                {
                    if (rpcEnabled && DiscordRPCSerializer.IsInitialized)
                    {
                        DiscordRPCSerializer.ForceRefresh();
                        ShowNotification(new GUIContent("RPC Updated!"));
                        Debug.Log("[SDKTools] Discord RPC manually updated from settings");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("RPC Not Available",
                            "Discord RPC is either disabled or not initialized. Enable it first to update.", "OK");
                    }
                }

                if (GUILayout.Button("Reset Timer", EditorStyles.miniButton))
                {
                    if (rpcEnabled && DiscordRPCSerializer.IsInitialized)
                    {
                        DiscordRPCSerializer.ResetTime();
                        ShowNotification(new GUIContent("Timer Reset!"));
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Clear Errors", EditorStyles.miniButton))
                {
                    DiscordRPCSerializer.ClearErrorState();
                    ShowNotification(new GUIContent("Errors Cleared!"));
                }

                if (GUILayout.Button("Reset to Default", EditorStyles.miniButton))
                {
                    if (EditorUtility.DisplayDialog("Reset Settings", "Reset all RPC settings to default values?", "Yes", "Cancel"))
                    {
                        ResetToDefaults();
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawDiagnostics()
        {
            showDiagnostics = EditorGUILayout.Foldout(showDiagnostics, "Diagnostics & Status", true);

            if (showDiagnostics)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("RPC System Status", EditorStyles.miniLabel);
                DrawStatusLine("RPC Enabled:", rpcEnabled.ToString(), rpcEnabled ? Color.green : Color.gray);
                DrawStatusLine("RPC Initialized:", DiscordRPCSerializer.IsInitialized.ToString(), DiscordRPCSerializer.IsInitialized ? Color.green : Color.red);
                DrawStatusLine("Has Errors:", DiscordRPCSerializer.HasError.ToString(), DiscordRPCSerializer.HasError ? Color.red : Color.green);

                EditorGUILayout.Space(3);

                EditorGUILayout.LabelField("Runtime Helper Status", EditorStyles.miniLabel);
                DrawStatusLine("Current State:", DiscordRpcRuntimeHelper.GetCurrentState().StateName(), Color.cyan);
                DrawStatusLine("Avatar Upload:", DiscordRpcRuntimeHelper.IsAvatarUploadActive().ToString(), DiscordRpcRuntimeHelper.IsAvatarUploadActive() ? Color.yellow : Color.gray);
                DrawStatusLine("Play Mode:", EditorApplication.isPlaying.ToString(), EditorApplication.isPlaying ? Color.green : Color.gray);

                EditorGUILayout.Space(3);

                EditorGUILayout.LabelField("Configuration", EditorStyles.miniLabel);
                DrawStatusLine("Avatar Name:", string.IsNullOrEmpty(customAvatarName) ? "(default)" : customAvatarName, Color.white);
                DrawStatusLine("Project Name:", PlayerSettings.productName, Color.white);
                DrawStatusLine("Theme:", themeInfo.ContainsKey(rpcImgOptions[selectedOption]) ? themeInfo[rpcImgOptions[selectedOption]].displayName : rpcImgOptions[selectedOption], Color.white);

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Refresh State", EditorStyles.miniButton))
                {
                    DiscordRpcRuntimeHelper.ForceStateRefresh();
                    ShowNotification(new GUIContent("State Refreshed!"));
                }

                if (GUILayout.Button("Reset Avatar Detection", EditorStyles.miniButton))
                {
                    DiscordRpcRuntimeHelper.ResetAvatarUploadDetection();
                    ShowNotification(new GUIContent("Detection Reset!"));
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawStatusLine(string label, string value, Color valueColor)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));

            var originalColor = GUI.contentColor;
            GUI.contentColor = valueColor;
            EditorGUILayout.LabelField(value);
            GUI.contentColor = originalColor;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Apply Settings", GUILayout.Width(100), GUILayout.Height(30)))
            {
                ApplySettings();
            }

            if (GUILayout.Button("Revert Changes", GUILayout.Width(100), GUILayout.Height(30)))
            {
                LoadSettings();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
        }

        private void SelectOption(int index)
        {
            if (index >= 0 && index < rpcImgOptions.Length)
            {
                selectedOption = index;

                string themeName = themeInfo.ContainsKey(rpcImgOptions[index]) ?
                    themeInfo[rpcImgOptions[index]].displayName : rpcImgOptions[index];
                ShowNotification(new GUIContent($"Selected: {themeName}"));

                Repaint();
            }
        }

        private void LoadSettings()
        {
            try
            {
                rpcEnabled = DiscordRPCSerializer.IsEnabled;

                string currentImage = EditorPrefs.GetString($"SDKTools.{GetProjectKeyLength()}.CurrentRPCImage", "default");
                selectedOption = FindImageOptionIndex(currentImage);

                customAvatarName = EditorPrefs.GetString($"SDKTools.{GetProjectKeyLength()}.AvatarName", "");

                Debug.Log($"[SDKTools] RPC Settings loaded: Theme='{rpcImgOptions[selectedOption]}', Enabled={rpcEnabled}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SDKTools] Error loading RPC settings: {e.Message}");
                ResetToDefaults();
            }
        }

        private void ApplySettings()
        {
            try
            {
                string selectedKey = rpcImgOptions[selectedOption];
                EditorPrefs.SetString($"SDKTools.{GetProjectKeyLength()}.CurrentRPCImage", selectedKey);

                EditorPrefs.SetString($"SDKTools.{GetProjectKeyLength()}.AvatarName", customAvatarName);

                if (rpcEnabled && DiscordRPCSerializer.IsInitialized)
                {
                    DiscordRPCSerializer.UpdateDRPCImg();
                    DiscordRPCSerializer.UpdateDRPC();
                }

                Debug.Log($"[SDKTools] RPC Settings applied: Theme='{selectedKey}', Enabled={rpcEnabled}");

                ShowNotification(new GUIContent("Settings Applied!"));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SDKTools] Error applying RPC settings: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to apply settings: {e.Message}", "OK");
            }
        }

        private void ResetToDefaults()
        {
            selectedOption = 0;
            rpcEnabled = true;
            customAvatarName = "";

            DiscordRPCSerializer.SetEnabled(true);
            ApplySettings();

            Debug.Log("[SDKTools] RPC Settings reset to defaults");
            ShowNotification(new GUIContent("Settings Reset!"));
        }

        private int FindImageOptionIndex(string key)
        {
            for (int i = 0; i < rpcImgOptions.Length; i++)
            {
                if (rpcImgOptions[i] == key)
                {
                    return i;
                }
            }
            return 0;
        }

        private int GetProjectKeyLength()
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
    }
}
#endif