#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SDKTools
{
    [InitializeOnLoad]
    public class TextureImportSettings : AssetPostprocessor
    {
        private static readonly HashSet<string> processedAssets = new HashSet<string>();
        private static bool isInitialScanComplete = false;

        static TextureImportSettings()
        {
            EditorApplication.delayCall += () => {
                if (!isInitialScanComplete)
                {
                    SetTextureImportSettings();
                    isInitialScanComplete = true;
                }
            };
        }

        public static void SetTextureImportSettings()
        {
            try
            {
                string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
                int processedCount = 0;
                int totalCount = textureGuids.Length;

                if (totalCount == 0)
                {
                    Debug.Log("[SDKTools] No textures found to process.");
                    return;
                }

                EditorUtility.DisplayProgressBar("Processing Textures", "Scanning textures...", 0f);

                try
                {
                    for (int i = 0; i < textureGuids.Length; i++)
                    {
                        string textureGuid = textureGuids[i];
                        string texturePath = AssetDatabase.GUIDToAssetPath(textureGuid);

                        float progress = (float)i / totalCount;
                        if (EditorUtility.DisplayCancelableProgressBar("Processing Textures",
                            $"Processing: {System.IO.Path.GetFileName(texturePath)} ({i + 1}/{totalCount})", progress))
                        {
                            Debug.Log("[SDKTools] Texture processing cancelled by user.");
                            break;
                        }

                        if (ProcessTexture(texturePath))
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
                    Debug.Log($"[SDKTools] Processed {processedCount} textures with streaming mipmaps.");
                }
                else
                {
                    Debug.Log("[SDKTools] No textures needed processing.");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[SDKTools] Error in texture processing: {e.Message}");
            }
        }

        private static bool ProcessTexture(string texturePath)
        {
            try
            {
                if (string.IsNullOrEmpty(texturePath) || !texturePath.StartsWith("Assets/"))
                {
                    return false;
                }

                if (processedAssets.Contains(texturePath))
                {
                    return false;
                }

                TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (textureImporter == null)
                {
                    Debug.LogWarning($"[SDKTools] Could not get TextureImporter for: {texturePath}");
                    return false;
                }

                if (!ShouldProcessTexture(textureImporter))
                {
                    processedAssets.Add(texturePath);
                    return false;
                }

                bool wasModified = false;

                if (textureImporter.textureType != TextureImporterType.Default)
                {
                    textureImporter.textureType = TextureImporterType.Default;
                    wasModified = true;
                }

                if (!textureImporter.mipmapEnabled)
                {
                    textureImporter.mipmapEnabled = true;
                    wasModified = true;
                }

                if (!textureImporter.streamingMipmaps)
                {
                    textureImporter.streamingMipmaps = true;
                    wasModified = true;
                }

                if (textureImporter.streamingMipmapsPriority != 0)
                {
                    textureImporter.streamingMipmapsPriority = 0;
                    wasModified = true;
                }

                if (wasModified)
                {
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                    processedAssets.Add(texturePath);
                    Debug.Log($"[SDKTools] Applied streaming mipmaps to: {System.IO.Path.GetFileName(texturePath)}");
                    return true;
                }

                processedAssets.Add(texturePath);
                return false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Error processing texture {texturePath}: {e.Message}");
                return false;
            }
        }

        private static bool ShouldProcessTexture(TextureImporter importer)
        {
            if (importer.streamingMipmaps &&
                importer.mipmapEnabled &&
                importer.textureType == TextureImporterType.Default &&
                importer.streamingMipmapsPriority == 0)
            {
                return false;
            }

            if (importer.textureType != TextureImporterType.Default)
            {
                return false;
            }

            return true;
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            try
            {
                TextureImporter textureImporter = assetImporter as TextureImporter;
                if (textureImporter == null) return;

                if (processedAssets.Contains(textureImporter.assetPath)) return;

                if (textureImporter.textureType != TextureImporterType.Default) return;

                bool needsReimport = false;

                if (!textureImporter.mipmapEnabled)
                {
                    textureImporter.mipmapEnabled = true;
                    needsReimport = true;
                }

                if (!textureImporter.streamingMipmaps)
                {
                    textureImporter.streamingMipmaps = true;
                    needsReimport = true;
                }

                if (textureImporter.streamingMipmapsPriority != 0)
                {
                    textureImporter.streamingMipmapsPriority = 0;
                    needsReimport = true;
                }

                if (needsReimport)
                {
                    processedAssets.Add(textureImporter.assetPath);
                    Debug.Log($"[SDKTools] Applied streaming mipmaps to new texture: {System.IO.Path.GetFileName(textureImporter.assetPath)}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Error in texture postprocessor: {e.Message}");
            }
        }
    }

    public class TextureImportEditor : Editor
    {
        [MenuItem("VRChat SDK/SDKTools/QoL/Set Texture Settings")]
        public static void SetTextureSettings()
        {
            if (EditorUtility.DisplayDialog("Process Textures",
                "This will process all textures in the Assets folder and enable streaming mipmaps. Continue?",
                "Yes", "Cancel"))
            {
                TextureImportSettings.SetTextureImportSettings();
            }
        }

        [MenuItem("VRChat SDK/SDKTools/QoL/Set Texture Settings", true)]
        private static bool ValidateSetTextureSettings()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }
    }
}
#endif