#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Linq;

namespace SDKTools
{
    public class MissingScriptsRemover : Editor
    {
        private static readonly HashSet<GameObject> processedObjects = new HashSet<GameObject>();
        private static bool isProcessing = false;

        [MenuItem("VRChat SDK/SDKTools/QoL/Remove Missing Scripts")]
        public static void RemoveMissingScripts()
        {
            if (isProcessing)
            {
                EditorUtility.DisplayDialog("Operation in Progress",
                    "Missing scripts removal is already in progress. Please wait for it to complete.", "OK");
                return;
            }

            try
            {
                isProcessing = true;
                processedObjects.Clear();

                RemoveMissingScriptsFromHierarchy();
            }
            finally
            {
                isProcessing = false;
                processedObjects.Clear();
            }
        }

        [MenuItem("VRChat SDK/SDKTools/QoL/Remove Missing Scripts (All Scenes)")]
        public static void RemoveMissingScriptsFromAllScenes()
        {
            if (isProcessing)
            {
                EditorUtility.DisplayDialog("Operation in Progress",
                    "Missing scripts removal is already in progress. Please wait for it to complete.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Missing Scripts from All Scenes",
                "This will process all scenes in the project and may take a while. Unsaved changes will be lost. Continue?",
                "Yes", "Cancel"))
            {
                return;
            }

            try
            {
                isProcessing = true;
                processedObjects.Clear();

                RemoveMissingScriptsFromAllScenesInternal();
            }
            finally
            {
                isProcessing = false;
                processedObjects.Clear();
            }
        }

        [MenuItem("VRChat SDK/SDKTools/QoL/Remove Missing Scripts (Assets)")]
        public static void RemoveMissingScriptsFromAssets()
        {
            if (isProcessing)
            {
                EditorUtility.DisplayDialog("Operation in Progress",
                    "Missing scripts removal is already in progress. Please wait for it to complete.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Missing Scripts from Assets",
                "This will process all prefabs and scriptable objects in the project. Continue?",
                "Yes", "Cancel"))
            {
                return;
            }

            try
            {
                isProcessing = true;
                processedObjects.Clear();

                RemoveMissingScriptsFromAssetsInternal();
            }
            finally
            {
                isProcessing = false;
                processedObjects.Clear();
            }
        }

        private static void RemoveMissingScriptsFromHierarchy()
        {
            try
            {
                GameObject[] gameObjects = GetAllGameObjectsInHierarchy();

                if (gameObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("No Objects Found",
                        "No GameObjects found in the current scene hierarchy.", "OK");
                    return;
                }

                int totalComponentCount = 0;
                int processedObjectCount = 0;

                EditorUtility.DisplayProgressBar("Removing Missing Scripts", "Scanning hierarchy...", 0f);

                try
                {
                    for (int i = 0; i < gameObjects.Length; i++)
                    {
                        GameObject go = gameObjects[i];

                        float progress = (float)i / gameObjects.Length;
                        if (EditorUtility.DisplayCancelableProgressBar("Removing Missing Scripts",
                            $"Processing: {go.name} ({i + 1}/{gameObjects.Length})", progress))
                        {
                            Debug.Log("[SDKTools] Missing scripts removal cancelled by user.");
                            return;
                        }

                        int removedCount = ProcessGameObject(go);
                        if (removedCount > 0)
                        {
                            totalComponentCount += removedCount;
                            processedObjectCount++;
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (totalComponentCount > 0)
                {
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }

                ShowResultDialog(totalComponentCount, processedObjectCount, gameObjects.Length, "hierarchy");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[SDKTools] Error removing missing scripts from hierarchy: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"An error occurred: {e.Message}", "OK");
            }
        }

        private static void RemoveMissingScriptsFromAllScenesInternal()
        {
            try
            {
                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
                string[] scenePaths = sceneGuids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray();

                if (scenePaths.Length == 0)
                {
                    EditorUtility.DisplayDialog("No Scenes Found",
                        "No scenes found in the project.", "OK");
                    return;
                }

                int totalComponentCount = 0;
                int totalObjectCount = 0;
                int processedScenes = 0;

                string originalScenePath = SceneManager.GetActiveScene().path;

                EditorUtility.DisplayProgressBar("Processing Scenes", "Loading scenes...", 0f);

                try
                {
                    for (int i = 0; i < scenePaths.Length; i++)
                    {
                        string scenePath = scenePaths[i];

                        float progress = (float)i / scenePaths.Length;
                        if (EditorUtility.DisplayCancelableProgressBar("Processing Scenes",
                            $"Processing scene: {System.IO.Path.GetFileNameWithoutExtension(scenePath)} ({i + 1}/{scenePaths.Length})", progress))
                        {
                            Debug.Log("[SDKTools] Scene processing cancelled by user.");
                            break;
                        }

                        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                        if (!scene.IsValid())
                        {
                            Debug.LogWarning($"[SDKTools] Could not load scene: {scenePath}");
                            continue;
                        }

                        var result = ProcessScene(scene);
                        if (result.removedComponents > 0)
                        {
                            totalComponentCount += result.removedComponents;
                            totalObjectCount += result.processedObjects;
                            processedScenes++;

                            EditorSceneManager.SaveScene(scene);
                            Debug.Log($"[SDKTools] Processed scene '{scene.name}': {result.removedComponents} missing scripts removed from {result.processedObjects} objects.");
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();

                    if (!string.IsNullOrEmpty(originalScenePath) && System.IO.File.Exists(originalScenePath))
                    {
                        EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                    }
                }

                ShowResultDialog(totalComponentCount, totalObjectCount, scenePaths.Length, $"{processedScenes} scenes");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[SDKTools] Error processing scenes: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"An error occurred: {e.Message}", "OK");
            }
        }

        private static void RemoveMissingScriptsFromAssetsInternal()
        {
            try
            {
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                string[] prefabPaths = prefabGuids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray();

                if (prefabPaths.Length == 0)
                {
                    EditorUtility.DisplayDialog("No Prefabs Found",
                        "No prefabs found in the project.", "OK");
                    return;
                }

                int totalComponentCount = 0;
                int totalObjectCount = 0;
                int processedPrefabs = 0;

                EditorUtility.DisplayProgressBar("Processing Prefabs", "Loading prefabs...", 0f);

                try
                {
                    for (int i = 0; i < prefabPaths.Length; i++)
                    {
                        string prefabPath = prefabPaths[i];

                        float progress = (float)i / prefabPaths.Length;
                        if (EditorUtility.DisplayCancelableProgressBar("Processing Prefabs",
                            $"Processing: {System.IO.Path.GetFileNameWithoutExtension(prefabPath)} ({i + 1}/{prefabPaths.Length})", progress))
                        {
                            Debug.Log("[SDKTools] Prefab processing cancelled by user.");
                            break;
                        }

                        var result = ProcessPrefab(prefabPath);
                        if (result.removedComponents > 0)
                        {
                            totalComponentCount += result.removedComponents;
                            totalObjectCount += result.processedObjects;
                            processedPrefabs++;
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (totalComponentCount > 0)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                ShowResultDialog(totalComponentCount, totalObjectCount, prefabPaths.Length, $"{processedPrefabs} prefabs");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[SDKTools] Error processing prefabs: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"An error occurred: {e.Message}", "OK");
            }
        }

        private static GameObject[] GetAllGameObjectsInHierarchy()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.scene.IsValid() && go.scene == SceneManager.GetActiveScene())
                .ToArray();
        }

        private static (int removedComponents, int processedObjects) ProcessScene(Scene scene)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            int totalRemoved = 0;
            int totalProcessed = 0;

            foreach (GameObject rootObject in rootObjects)
            {
                GameObject[] allObjects = new GameObject[] { rootObject }
                    .Concat(rootObject.GetComponentsInChildren<Transform>(true)
                        .Select(t => t.gameObject))
                    .Distinct()
                    .ToArray();

                foreach (GameObject go in allObjects)
                {
                    int removed = ProcessGameObject(go);
                    if (removed > 0)
                    {
                        totalRemoved += removed;
                        totalProcessed++;
                    }
                }
            }

            return (totalRemoved, totalProcessed);
        }

        private static (int removedComponents, int processedObjects) ProcessPrefab(string prefabPath)
        {
            try
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                {
                    Debug.LogWarning($"[SDKTools] Could not load prefab: {prefabPath}");
                    return (0, 0);
                }

                GameObject[] allObjects = new GameObject[] { prefabAsset }
                    .Concat(prefabAsset.GetComponentsInChildren<Transform>(true)
                        .Select(t => t.gameObject))
                    .Distinct()
                    .ToArray();

                int totalRemoved = 0;
                int totalProcessed = 0;
                bool prefabModified = false;

                foreach (GameObject go in allObjects)
                {
                    int componentCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (componentCount > 0)
                    {
                        Undo.RecordObject(go, "Remove Missing Scripts from Prefab");

                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

                        totalRemoved += componentCount;
                        totalProcessed++;
                        prefabModified = true;
                    }
                }

                if (prefabModified)
                {
                    EditorUtility.SetDirty(prefabAsset);
                    Debug.Log($"[SDKTools] Processed prefab '{prefabAsset.name}': {totalRemoved} missing scripts removed from {totalProcessed} objects.");
                }

                return (totalRemoved, totalProcessed);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Error processing prefab {prefabPath}: {e.Message}");
                return (0, 0);
            }
        }

        private static int ProcessGameObject(GameObject go)
        {
            try
            {
                if (go == null || processedObjects.Contains(go))
                {
                    return 0;
                }

                processedObjects.Add(go);

                int componentCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (componentCount > 0)
                {
                    Undo.RecordObject(go, "Remove Missing Scripts");

                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

                    Debug.Log($"[SDKTools] Removed {componentCount} missing scripts from '{go.name}'");
                    return componentCount;
                }

                return 0;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Error processing GameObject '{go?.name}': {e.Message}");
                return 0;
            }
        }

        private static void ShowResultDialog(int totalComponents, int processedObjects, int totalScanned, string context)
        {
            string message;

            if (totalComponents > 0)
            {
                message = $"Successfully removed {totalComponents} missing script(s) from {processedObjects} object(s) in {context}.\n\n" +
                         $"Total objects scanned: {totalScanned}";
            }
            else
            {
                message = $"No missing scripts found in {context}.\n\n" +
                         $"Total objects scanned: {totalScanned}";
            }

            EditorUtility.DisplayDialog("Remove Missing Scripts - Complete", message, "OK");
            Debug.Log($"[SDKTools] Missing scripts removal complete: {totalComponents} components removed from {processedObjects} objects in {context}");
        }

        [MenuItem("VRChat SDK/SDKTools/QoL/Remove Missing Scripts", true)]
        [MenuItem("VRChat SDK/SDKTools/QoL/Remove Missing Scripts (All Scenes)", true)]
        [MenuItem("VRChat SDK/SDKTools/QoL/Remove Missing Scripts (Assets)", true)]
        private static bool ValidateRemoveMissingScripts()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating &&
                   !isProcessing &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
#endif