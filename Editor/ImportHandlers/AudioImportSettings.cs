#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SDKTools
{
    [InitializeOnLoad]
    public class AudioImportSettings : AssetPostprocessor
    {
        private static readonly HashSet<string> processedAssets = new HashSet<string>();
        private static bool isInitialScanComplete = false;

        static AudioImportSettings()
        {
            EditorApplication.delayCall += () => {
                if (!isInitialScanComplete)
                {
                    SetAudioImportSettings();
                    isInitialScanComplete = true;
                }
            };
        }

        public static void SetAudioImportSettings()
        {
            try
            {
                string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
                int processedCount = 0;
                int totalCount = audioGuids.Length;

                if (totalCount == 0)
                {
                    Debug.Log("[SDKTools] No audio files found to process.");
                    return;
                }

                EditorUtility.DisplayProgressBar("Processing Audio", "Scanning audio files...", 0f);

                try
                {
                    for (int i = 0; i < audioGuids.Length; i++)
                    {
                        string audioGuid = audioGuids[i];
                        string audioPath = AssetDatabase.GUIDToAssetPath(audioGuid);

                        float progress = (float)i / totalCount;
                        if (EditorUtility.DisplayCancelableProgressBar("Processing Audio",
                            $"Processing: {System.IO.Path.GetFileName(audioPath)} ({i + 1}/{totalCount})", progress))
                        {
                            Debug.Log("[SDKTools] Audio processing cancelled by user.");
                            break;
                        }

                        if (ProcessAudioFile(audioPath))
                        {
                            processedCount++;
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (processedCount > 0)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log($"[SDKTools] Processed {processedCount} audio files with load in background.");
                }
                else
                {
                    Debug.Log("[SDKTools] No audio files needed processing.");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[SDKTools] Error in audio processing: {e.Message}");
            }
        }

        private static bool ProcessAudioFile(string audioPath)
        {
            try
            {
                if (string.IsNullOrEmpty(audioPath) || !audioPath.StartsWith("Assets/"))
                {
                    return false;
                }

                if (processedAssets.Contains(audioPath))
                {
                    return false;
                }

                AudioImporter audioImporter = AssetImporter.GetAtPath(audioPath) as AudioImporter;
                if (audioImporter == null)
                {
                    Debug.LogWarning($"[SDKTools] Could not get AudioImporter for: {audioPath}");
                    return false;
                }

                if (audioImporter.loadInBackground)
                {
                    processedAssets.Add(audioPath);
                    return false;
                }

                audioImporter.loadInBackground = true;

                AssetDatabase.ImportAsset(audioPath, ImportAssetOptions.ForceUpdate);
                processedAssets.Add(audioPath);

                Debug.Log($"[SDKTools] Applied load in background to: {System.IO.Path.GetFileName(audioPath)}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Error processing audio file {audioPath}: {e.Message}");
                return false;
            }
        }

        public void OnPostprocessAudio(AudioClip audioClip)
        {
            try
            {
                AudioImporter audioImporter = assetImporter as AudioImporter;
                if (audioImporter == null) return;

                if (processedAssets.Contains(audioImporter.assetPath)) return;

                if (!audioImporter.loadInBackground)
                {
                    audioImporter.loadInBackground = true;
                    processedAssets.Add(audioImporter.assetPath);
                    Debug.Log($"[SDKTools] Applied load in background to new audio: {System.IO.Path.GetFileName(audioImporter.assetPath)}");
                }
                else
                {
                    processedAssets.Add(audioImporter.assetPath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Error in audio postprocessor: {e.Message}");
            }
        }
    }

    public class AudioImportEditor : Editor
    {
        [MenuItem("VRChat SDK/SDKTools/QoL/Set Audio Settings")]
        public static void SetAudioSettings()
        {
            if (EditorUtility.DisplayDialog("Process Audio",
                "This will process all audio files in the Assets folder and enable load in background. Continue?",
                "Yes", "Cancel"))
            {
                AudioImportSettings.SetAudioImportSettings();
            }
        }

        [MenuItem("VRChat SDK/SDKTools/QoL/Set Audio Settings", true)]
        private static bool ValidateSetAudioSettings()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }
    }
}
#endif