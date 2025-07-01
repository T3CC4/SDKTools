#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SDKTools
{
    public class GameObjectCreator : Editor
    {
        private const int MAX_SORTING_ORDER = 32767;
        private const int MIN_SORTING_ORDER = -32768;

        private const float DEFAULT_SCALE = 50f;

        [MenuItem("GameObject/Audio/World Audio", false, 0)]
        static void CreateWorldAudio(MenuCommand menuCommand)
        {
            try
            {
                GameObject audioGO = new GameObject("WorldAudio");

                SetupTransform(audioGO, menuCommand);

                AudioSource audio = audioGO.AddComponent<AudioSource>();
                ConfigureWorldAudio(audio);

                SetGameObjectIcon(audioGO, "AudioSource Icon");

                Selection.activeGameObject = audioGO;
                Undo.RegisterCreatedObjectUndo(audioGO, "Create WorldAudio");

                Debug.Log("[SDKTools] Created WorldAudio GameObject with optimized VRChat settings");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Failed to create WorldAudio: {e.Message}");
            }
        }

        [MenuItem("GameObject/VRChat/Screen Space Animation/Single Sphere", false, 0)]
        static void CreateAnimationSphere(MenuCommand menuCommand)
        {
            CreateSingleAnimationObject(PrimitiveType.Sphere, "AnimationSphere", menuCommand);
        }

        [MenuItem("GameObject/VRChat/Screen Space Animation/Single Cube", false, 0)]
        static void CreateAnimationCube(MenuCommand menuCommand)
        {
            CreateSingleAnimationObject(PrimitiveType.Cube, "AnimationCube", menuCommand);
        }

        [MenuItem("GameObject/VRChat/Screen Space Animation/Multiple Spheres...", false, 20)]
        static void CreateMultipleSpheres()
        {
            ShowMultipleObjectsWindow(PrimitiveType.Sphere);
        }

        [MenuItem("GameObject/VRChat/Screen Space Animation/Multiple Cubes...", false, 21)]
        static void CreateMultipleCubes()
        {
            ShowMultipleObjectsWindow(PrimitiveType.Cube);
        }

        [MenuItem("GameObject/VRChat/Screen Space Animation/Manage Sorting Orders...", false, 40)]
        static void ShowSortingOrderManager()
        {
            SortingOrderManagerWindow.ShowWindow();
        }

        private static void CreateSingleAnimationObject(PrimitiveType primitiveType, string baseName, MenuCommand menuCommand)
        {
            try
            {
                GameObject animationGO = GameObject.CreatePrimitive(primitiveType);
                animationGO.name = baseName;

                SetupTransform(animationGO, menuCommand);
                ConfigureAnimationTransform(animationGO.transform);

                RemoveCollider(animationGO);

                ConfigureAnimationRenderer(animationGO);

                SetGameObjectIcon(animationGO, GetIconName(primitiveType));

                Selection.activeGameObject = animationGO;
                Undo.RegisterCreatedObjectUndo(animationGO, $"Create {baseName}");

                Debug.Log($"[SDKTools] Created {baseName} with screen space animation settings");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Failed to create {baseName}: {e.Message}");
            }
        }

        private static void ShowMultipleObjectsWindow(PrimitiveType primitiveType)
        {
            MultipleObjectsWindow.ShowWindow(primitiveType);
        }

        private static void SetupTransform(GameObject go, MenuCommand menuCommand)
        {
            GameObject parent = menuCommand?.context as GameObject;
            if (parent != null)
            {
                go.transform.SetParent(parent.transform);
            }
            else if (Selection.activeGameObject != null)
            {
                go.transform.SetParent(Selection.activeGameObject.transform);
            }
        }

        private static void ConfigureAnimationTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(DEFAULT_SCALE, DEFAULT_SCALE, DEFAULT_SCALE);
        }

        private static void ConfigureWorldAudio(AudioSource audio)
        {
            audio.clip = null;
            audio.playOnAwake = false;
            audio.loop = true;
            audio.priority = 0;
            audio.volume = 1f;
            audio.pitch = 1f;

            audio.spatialBlend = 0f;
            audio.reverbZoneMix = 0f;
            audio.dopplerLevel = 0f;

            audio.minDistance = 999999f;
            audio.maxDistance = 1000000f;
            audio.rolloffMode = AudioRolloffMode.Custom;

            AnimationCurve customRolloff = new AnimationCurve();
            customRolloff.AddKey(0f, 1f);
            customRolloff.AddKey(1f, 1f);
            audio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customRolloff);
        }

        private static void ConfigureAnimationRenderer(GameObject go)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                if (renderer.sharedMaterial == null)
                {
                    Material defaultMaterial = Resources.GetBuiltinResource<Material>("Default-Material.mat");
                    if (defaultMaterial != null)
                    {
                        renderer.sharedMaterial = defaultMaterial;
                    }
                    else
                    {
                        renderer.sharedMaterial = CreateDefaultMaterial();
                    }
                }
            }
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider[] colliders = go.GetComponents<Collider>();
            foreach (Collider collider in colliders)
            {
                DestroyImmediate(collider);
            }
        }

        private static void SetGameObjectIcon(GameObject go, string iconName)
        {
            try
            {
                Texture2D icon = EditorGUIUtility.IconContent(iconName)?.image as Texture2D;
                if (icon != null)
                {
                    EditorGUIUtility.SetIconForObject(go, icon);
                }
            }
            catch
            {
            }
        }

        private static Material CreateDefaultMaterial()
        {
            try
            {
                Material material = new Material(Shader.Find("Unlit/Color"));
                if (material.shader == null)
                {
                    material.shader = Shader.Find("Standard");
                }

                material.color = Color.white;
                material.name = "ScreenAnimation_DefaultMaterial";
                return material;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Failed to create default material: {e.Message}");
                return null;
            }
        }

        private static string GetIconName(PrimitiveType primitiveType)
        {
            switch (primitiveType)
            {
                case PrimitiveType.Sphere: return "d_Mesh Icon";
                case PrimitiveType.Cube: return "d_Mesh Icon";
                default: return "d_GameObject Icon";
            }
        }

        public static void CreateMultipleAnimationObjects(PrimitiveType primitiveType, int count, int startingSortingOrder, string namePrefix, bool descendingOrder = false)
        {
            try
            {
                if (count <= 0 || count > 100)
                {
                    EditorUtility.DisplayDialog("Invalid Count", "Count must be between 1 and 100", "OK");
                    return;
                }

                if (!ValidateSortingOrderRange(startingSortingOrder, count, descendingOrder))
                {
                    return;
                }

                List<GameObject> createdObjects = new List<GameObject>();

                GameObject parentGroup = new GameObject($"{namePrefix}Group");
                SetupTransform(parentGroup, null);

                for (int i = 0; i < count; i++)
                {
                    int sortingOrder = descendingOrder ?
                        startingSortingOrder - i :
                        startingSortingOrder + i;

                    GameObject animationGO = GameObject.CreatePrimitive(primitiveType);
                    animationGO.name = $"{namePrefix}_{i + 1:D2}_Sort{sortingOrder}";

                    animationGO.transform.SetParent(parentGroup.transform);

                    ConfigureAnimationTransform(animationGO.transform);
                    RemoveCollider(animationGO);
                    ConfigureAnimationRenderer(animationGO);

                    SetSortingOrder(animationGO, sortingOrder);

                    ApplySortingOrderColorCoding(animationGO, sortingOrder, startingSortingOrder, count, descendingOrder);

                    SetGameObjectIcon(animationGO, GetIconName(primitiveType));

                    createdObjects.Add(animationGO);
                }

                Undo.RegisterCreatedObjectUndo(parentGroup, $"Create Multiple {primitiveType}s");
                foreach (GameObject obj in createdObjects)
                {
                    Undo.RegisterCreatedObjectUndo(obj, $"Create Multiple {primitiveType}s");
                }

                Selection.activeGameObject = parentGroup;

                Debug.Log($"[SDKTools] Created {count} {primitiveType}s with sorting orders {startingSortingOrder} to {(descendingOrder ? startingSortingOrder - count + 1 : startingSortingOrder + count - 1)}");

                EditorUtility.DisplayDialog("Objects Created",
                    $"Successfully created {count} {primitiveType}s\n" +
                    $"Sorting orders: {startingSortingOrder} to {(descendingOrder ? startingSortingOrder - count + 1 : startingSortingOrder + count - 1)}\n" +
                    $"Parent group: {parentGroup.name}", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SDKTools] Failed to create multiple objects: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to create objects: {e.Message}", "OK");
            }
        }

        private static bool ValidateSortingOrderRange(int startingOrder, int count, bool descending)
        {
            int endOrder = descending ? startingOrder - count + 1 : startingOrder + count - 1;

            if (endOrder > MAX_SORTING_ORDER || endOrder < MIN_SORTING_ORDER ||
                startingOrder > MAX_SORTING_ORDER || startingOrder < MIN_SORTING_ORDER)
            {
                EditorUtility.DisplayDialog("Invalid Sorting Order Range",
                    $"Sorting order range ({startingOrder} to {endOrder}) exceeds valid range ({MIN_SORTING_ORDER} to {MAX_SORTING_ORDER})", "OK");
                return false;
            }

            return true;
        }

        private static void SetSortingOrder(GameObject go, int sortingOrder)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
        }

        private static void ApplySortingOrderColorCoding(GameObject go, int sortingOrder, int startingSortingOrder, int count, bool descending)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                Material mat = new Material(renderer.material);

                float normalizedPosition = (float)(descending ? startingSortingOrder - sortingOrder : sortingOrder - startingSortingOrder) / (count - 1);
                normalizedPosition = Mathf.Clamp01(normalizedPosition);

                Color color = Color.HSVToRGB(normalizedPosition * 0.8f, 0.7f, 0.9f);
                color.a = 0.8f;

                mat.color = color;
                renderer.material = mat;
            }
        }
    }

    public class MultipleObjectsWindow : EditorWindow
    {
        private PrimitiveType primitiveType;
        private int objectCount = 5;
        private int startingSortingOrder = 100;
        private string namePrefix = "ScreenAnim";
        private bool descendingOrder = false;
        private bool useColorCoding = true;

        public static void ShowWindow(PrimitiveType type)
        {
            MultipleObjectsWindow window = GetWindow<MultipleObjectsWindow>($"Create Multiple {type}s");
            window.primitiveType = type;
            window.namePrefix = type == PrimitiveType.Sphere ? "ScreenSphere" : "ScreenCube";
            window.minSize = new Vector2(350, 300);
            window.maxSize = new Vector2(400, 350);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField($"Create Multiple {primitiveType}s", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            objectCount = EditorGUILayout.IntSlider(
                new GUIContent("Object Count", "Number of objects to create (1-100)"),
                objectCount, 1, 100);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Sorting Order Settings", EditorStyles.boldLabel);

            startingSortingOrder = EditorGUILayout.IntField(
                new GUIContent("Starting Sorting Order", "The sorting order for the first object"),
                startingSortingOrder);

            descendingOrder = EditorGUILayout.Toggle(
                new GUIContent("Descending Order", "Create objects with decreasing sorting order"),
                descendingOrder);

            EditorGUILayout.Space();

            int endOrder = descendingOrder ? startingSortingOrder - objectCount + 1 : startingSortingOrder + objectCount - 1;
            EditorGUILayout.LabelField($"Sorting Order Range: {startingSortingOrder} to {endOrder}");

            bool validRange = endOrder >= -32768 && endOrder <= 32767 && startingSortingOrder >= -32768 && startingSortingOrder <= 32767;
            if (!validRange)
            {
                EditorGUILayout.HelpBox("Sorting order range exceeds valid limits (-32768 to 32767)", MessageType.Error);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Naming Settings", EditorStyles.boldLabel);
            namePrefix = EditorGUILayout.TextField(
                new GUIContent("Name Prefix", "Prefix for object names"),
                namePrefix);

            EditorGUILayout.Space();

            useColorCoding = EditorGUILayout.Toggle(
                new GUIContent("Color Coding", "Apply rainbow colors based on sorting order"),
                useColorCoding);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Example Names:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"• {namePrefix}_01_Sort{startingSortingOrder}", EditorStyles.miniLabel);
            if (objectCount > 1)
            {
                EditorGUILayout.LabelField($"• {namePrefix}_{objectCount:D2}_Sort{endOrder}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();

            GUI.enabled = validRange && !string.IsNullOrEmpty(namePrefix);
            if (GUILayout.Button($"Create {objectCount} {primitiveType}s", GUILayout.Height(30)))
            {
                GameObjectCreator.CreateMultipleAnimationObjects(primitiveType, objectCount, startingSortingOrder, namePrefix, descendingOrder);
                Close();
            }
            GUI.enabled = true;

            EditorGUILayout.Space();

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
        }
    }

    public class SortingOrderManagerWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<RendererInfo> renderers = new List<RendererInfo>();

        private class RendererInfo
        {
            public Renderer renderer;
            public GameObject gameObject;
            public int currentSortingOrder;
            public int newSortingOrder;
            public string path;
        }

        public static void ShowWindow()
        {
            SortingOrderManagerWindow window = GetWindow<SortingOrderManagerWindow>("Sorting Order Manager");
            window.minSize = new Vector2(500, 400);
            window.Show();
            window.RefreshRenderers();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sorting Order Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshRenderers();
            }

            if (GUILayout.Button("Apply All Changes", GUILayout.Width(120)))
            {
                ApplyAllChanges();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField($"Found {renderers.Count} renderers");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (renderers.Count == 0)
            {
                EditorGUILayout.HelpBox("No renderers found in the scene. Click Refresh to scan.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GameObject", EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField("Path", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField("Current", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("New", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < renderers.Count; i++)
            {
                var info = renderers[i];
                if (info.renderer == null)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(info.gameObject.name, EditorStyles.label, GUILayout.Width(150)))
                {
                    Selection.activeGameObject = info.gameObject;
                    EditorGUIUtility.PingObject(info.gameObject);
                }

                EditorGUILayout.LabelField(info.path, GUILayout.Width(200));

                EditorGUILayout.LabelField(info.currentSortingOrder.ToString(), GUILayout.Width(60));

                info.newSortingOrder = EditorGUILayout.IntField(info.newSortingOrder, GUILayout.Width(60));

                bool hasChanged = info.newSortingOrder != info.currentSortingOrder;
                GUI.enabled = hasChanged;
                if (GUILayout.Button("Apply", GUILayout.Width(50)))
                {
                    ApplyChange(info);
                }
                GUI.enabled = true;

                if (GUILayout.Button("↺", GUILayout.Width(25)))
                {
                    info.newSortingOrder = info.currentSortingOrder;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void RefreshRenderers()
        {
            renderers.Clear();

            Renderer[] allRenderers = FindObjectsOfType<Renderer>();

            foreach (var renderer in allRenderers)
            {
                if (renderer.gameObject.scene.IsValid())
                {
                    renderers.Add(new RendererInfo
                    {
                        renderer = renderer,
                        gameObject = renderer.gameObject,
                        currentSortingOrder = renderer.sortingOrder,
                        newSortingOrder = renderer.sortingOrder,
                        path = GetGameObjectPath(renderer.gameObject)
                    });
                }
            }

            renderers = renderers.OrderBy(r => r.currentSortingOrder).ToList();
        }

        private string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private void ApplyChange(RendererInfo info)
        {
            if (info.renderer != null)
            {
                Undo.RecordObject(info.renderer, "Change Sorting Order");
                info.renderer.sortingOrder = info.newSortingOrder;
                info.currentSortingOrder = info.newSortingOrder;

                Debug.Log($"[SDKTools] Updated sorting order for {info.gameObject.name} to {info.newSortingOrder}");
            }
        }

        private void ApplyAllChanges()
        {
            int changedCount = 0;

            foreach (var info in renderers)
            {
                if (info.renderer != null && info.newSortingOrder != info.currentSortingOrder)
                {
                    ApplyChange(info);
                    changedCount++;
                }
            }

            if (changedCount > 0)
            {
                EditorUtility.DisplayDialog("Changes Applied", $"Applied sorting order changes to {changedCount} objects.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Changes", "No sorting order changes to apply.", "OK");
            }
        }
    }
}
#endif