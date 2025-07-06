#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

namespace SDKTools
{
    [System.Serializable]
    public class SerializableColor
    {
        public float r, g, b, a;

        public SerializableColor()
        {
            r = g = b = a = 0f;
        }

        public SerializableColor(Color color)
        {
            r = color.r;
            g = color.g;
            b = color.b;
            a = color.a;
        }

        public Color ToColor()
        {
            return new Color(r, g, b, a);
        }

        public static implicit operator Color(SerializableColor sc)
        {
            return sc?.ToColor() ?? Color.white;
        }

        public static implicit operator SerializableColor(Color c)
        {
            return new SerializableColor(c);
        }
    }

    [System.Serializable]
    public class SerializableVector4
    {
        public float x, y, z, w;

        public SerializableVector4()
        {
            x = y = z = w = 0f;
        }

        public SerializableVector4(Vector4 vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
            w = vector.w;
        }

        public Vector4 ToVector4()
        {
            return new Vector4(x, y, z, w);
        }

        public static implicit operator Vector4(SerializableVector4 sv)
        {
            return sv?.ToVector4() ?? Vector4.zero;
        }

        public static implicit operator SerializableVector4(Vector4 v)
        {
            return new SerializableVector4(v);
        }
    }

    [System.Serializable]
    public class SerializableVector2
    {
        public float x, y;

        public SerializableVector2()
        {
            x = y = 0f;
        }

        public SerializableVector2(Vector2 vector)
        {
            x = vector.x;
            y = vector.y;
        }

        public Vector2 ToVector2()
        {
            return new Vector2(x, y);
        }

        public static implicit operator Vector2(SerializableVector2 sv)
        {
            return sv?.ToVector2() ?? Vector2.zero;
        }

        public static implicit operator SerializableVector2(Vector2 v)
        {
            return new SerializableVector2(v);
        }
    }
    [System.Serializable]
    public class MaterialPreset
    {
        public string name;
        public string category;
        public string shaderName;
        public DateTime createdTime;
        public DateTime lastUsed;
        public int useCount;
        public List<MaterialProperty> properties;
        public List<TextureProperty> textures;

        public MaterialPreset()
        {
            properties = new List<MaterialProperty>();
            textures = new List<TextureProperty>();
            name = "";
            category = "";
            shaderName = "";
            createdTime = DateTime.Now;
            lastUsed = DateTime.Now;
            useCount = 0;
        }

        public MaterialPreset(string presetName, string presetCategory, Material material)
        {
            name = presetName;
            category = presetCategory;
            shaderName = material.shader.name;
            createdTime = DateTime.Now;
            lastUsed = DateTime.Now;
            useCount = 0;
            properties = new List<MaterialProperty>();
            textures = new List<TextureProperty>();

            ExtractMaterialData(material);
        }

        private void ExtractMaterialData(Material material)
        {
            var shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);

            for (int i = 0; i < propertyCount; i++)
            {
                string propName = ShaderUtil.GetPropertyName(shader, i);
                var propType = ShaderUtil.GetPropertyType(shader, i);

                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        properties.Add(new MaterialProperty
                        {
                            name = propName,
                            type = PropertyType.Color,
                            colorValue = new SerializableColor(material.GetColor(propName))
                        });
                        break;

                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        properties.Add(new MaterialProperty
                        {
                            name = propName,
                            type = PropertyType.Float,
                            floatValue = material.GetFloat(propName)
                        });
                        break;

                    case ShaderUtil.ShaderPropertyType.Vector:
                        properties.Add(new MaterialProperty
                        {
                            name = propName,
                            type = PropertyType.Vector,
                            vectorValue = new SerializableVector4(material.GetVector(propName))
                        });
                        break;

                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var texture = material.GetTexture(propName);
                        if (texture != null)
                        {
                            textures.Add(new TextureProperty
                            {
                                name = propName,
                                texturePath = AssetDatabase.GetAssetPath(texture),
                                textureGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture)),
                                offset = new SerializableVector2(material.GetTextureOffset(propName)),
                                scale = new SerializableVector2(material.GetTextureScale(propName))
                            });
                        }
                        break;
                }
            }
        }

        public void ApplyToMaterial(Material material)
        {
            if (material == null)
            {
                Debug.LogWarning("Cannot apply preset to null material");
                return;
            }

            if (string.IsNullOrEmpty(shaderName))
            {
                Debug.LogWarning("Preset has no shader name, skipping shader assignment");
            }
            else if (material.shader.name != shaderName)
            {
                var shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    material.shader = shader;
                }
                else
                {
                    Debug.LogWarning($"Shader '{shaderName}' not found. Cannot apply preset fully.");
                    return;
                }
            }

            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    if (prop == null || string.IsNullOrEmpty(prop.name)) continue;

                    try
                    {
                        switch (prop.type)
                        {
                            case PropertyType.Color:
                                if (prop.colorValue != null)
                                    material.SetColor(prop.name, prop.colorValue.ToColor());
                                break;
                            case PropertyType.Float:
                                material.SetFloat(prop.name, prop.floatValue);
                                break;
                            case PropertyType.Vector:
                                if (prop.vectorValue != null)
                                    material.SetVector(prop.name, prop.vectorValue.ToVector4());
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to set property '{prop.name}': {e.Message}");
                    }
                }
            }

            if (textures != null)
            {
                foreach (var tex in textures)
                {
                    if (tex == null || string.IsNullOrEmpty(tex.name)) continue;

                    try
                    {
                        string assetPath = "";
                        if (!string.IsNullOrEmpty(tex.textureGUID))
                        {
                            assetPath = AssetDatabase.GUIDToAssetPath(tex.textureGUID);
                        }

                        if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(tex.texturePath))
                        {
                            assetPath = tex.texturePath;
                        }

                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            var texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                            if (texture != null)
                            {
                                material.SetTexture(tex.name, texture);

                                if (tex.offset != null)
                                    material.SetTextureOffset(tex.name, tex.offset.ToVector2());
                                if (tex.scale != null)
                                    material.SetTextureScale(tex.name, tex.scale.ToVector2());
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to set texture '{tex.name}': {e.Message}");
                    }
                }
            }

            lastUsed = DateTime.Now;
            useCount++;

            EditorUtility.SetDirty(material);
        }
    }

    [System.Serializable]
    public class MaterialProperty
    {
        public string name;
        public PropertyType type;
        public SerializableColor colorValue;
        public float floatValue;
        public SerializableVector4 vectorValue;

        public MaterialProperty()
        {
            name = "";
            type = PropertyType.Float;
            colorValue = new SerializableColor();
            floatValue = 0f;
            vectorValue = new SerializableVector4();
        }
    }

    [System.Serializable]
    public class TextureProperty
    {
        public string name;
        public string texturePath;
        public string textureGUID;
        public SerializableVector2 offset;
        public SerializableVector2 scale;

        public TextureProperty()
        {
            name = "";
            texturePath = "";
            textureGUID = "";
            offset = new SerializableVector2();
            scale = new SerializableVector2(Vector2.one);
        }
    }

    public enum PropertyType
    {
        Color,
        Float,
        Vector
    }

    public partial class MaterialPresetManager : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchQuery = "";
        private string selectedCategory = "All";
        private bool showCreatePreset = false;
        private Material selectedMaterial;

        private string newPresetName = "";
        private string newPresetCategory = "General";
        private Material presetSourceMaterial;

        private static List<MaterialPreset> materialPresets = new List<MaterialPreset>();
        private static string presetsDataPath;
        private Dictionary<string, bool> categoryFoldStates = new Dictionary<string, bool>();
        private List<string> availableCategories = new List<string>();

        private const float ITEM_HEIGHT = 24f;
        private const float BUTTON_WIDTH = 80f;
        private const float SMALL_BUTTON_WIDTH = 60f;

        [MenuItem("VRChat SDK/SDKTools/Material Preset Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialPresetManager>("Material Presets");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            InitializePresetManager();
            LoadPresets();
            UpdateCategoryList();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();

            if (showCreatePreset)
            {
                DrawCreatePresetPanel();
                GUILayout.Space(10);
            }

            DrawPresetsList();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);

            var titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField("🎨 Material Preset Manager", titleStyle);

            var subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
            EditorGUILayout.LabelField("Save and apply material configurations easily", subtitleStyle);

            EditorGUILayout.Space(10);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("🔍", GUILayout.Width(20));
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(180));

            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }

            GUILayout.Space(10);

            GUILayout.Label("Category:", GUILayout.Width(60));
            var categoryOptions = new List<string> { "All" };
            categoryOptions.AddRange(availableCategories);
            int selectedIndex = Mathf.Max(0, categoryOptions.IndexOf(selectedCategory));
            selectedIndex = EditorGUILayout.Popup(selectedIndex, categoryOptions.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(100));
            selectedCategory = categoryOptions[selectedIndex];

            GUILayout.FlexibleSpace();

            selectedMaterial = (Material)EditorGUILayout.ObjectField(selectedMaterial, typeof(Material), false, GUILayout.Width(150));

            GUILayout.Space(5);

            var createContent = new GUIContent(showCreatePreset ? "➖ Hide Create" : "➕ Create Preset");
            if (GUILayout.Button(createContent, EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                showCreatePreset = !showCreatePreset;
                if (showCreatePreset && selectedMaterial != null)
                {
                    presetSourceMaterial = selectedMaterial;
                    newPresetName = selectedMaterial.name + " Preset";
                }
            }

            if (GUILayout.Button(new GUIContent("🔄 Refresh"), EditorStyles.toolbarButton, GUILayout.Width(BUTTON_WIDTH)))
            {
                LoadPresets();
                UpdateCategoryList();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCreatePresetPanel()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };
            EditorGUILayout.LabelField("➕ Create New Preset", headerStyle);

            var panelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };

            EditorGUILayout.BeginVertical(panelStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source Material:", GUILayout.Width(100));
            presetSourceMaterial = (Material)EditorGUILayout.ObjectField(presetSourceMaterial, typeof(Material), false);
            EditorGUILayout.EndHorizontal();

            if (presetSourceMaterial != null)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Preset Name:", GUILayout.Width(100));
                newPresetName = EditorGUILayout.TextField(newPresetName);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Category:", GUILayout.Width(100));

                if (availableCategories.Contains(newPresetCategory))
                {
                    int catIndex = availableCategories.IndexOf(newPresetCategory);
                    catIndex = EditorGUILayout.Popup(catIndex, availableCategories.ToArray(), GUILayout.Width(120));
                    newPresetCategory = availableCategories[catIndex];
                }
                else
                {
                    newPresetCategory = EditorGUILayout.TextField(newPresetCategory, GUILayout.Width(120));
                }

                if (GUILayout.Button("📝 New Category", EditorStyles.miniButton, GUILayout.Width(90)))
                {
                    newPresetCategory = "";
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                var infoStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Italic
                };
                EditorGUILayout.LabelField($"Shader: {presetSourceMaterial.shader.name}", infoStyle);

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUI.enabled = !string.IsNullOrEmpty(newPresetName) && !string.IsNullOrEmpty(newPresetCategory);
                if (GUILayout.Button("✨ Create Preset", GUILayout.Width(120)))
                {
                    CreatePreset();
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                var helpStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontStyle = FontStyle.Italic
                };
                EditorGUILayout.LabelField("Select a material to create a preset from", helpStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetsList()
        {
            if (materialPresets == null || materialPresets.Count == 0)
            {
                EditorGUILayout.Space(50);
                var emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 14
                };
                EditorGUILayout.LabelField("No material presets saved yet", emptyStyle);
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Create your first preset using the toolbar above", emptyStyle);
                return;
            }

            var filteredPresets = materialPresets
                .Where(preset =>
                    (selectedCategory == "All" || preset.category == selectedCategory) &&
                    (string.IsNullOrEmpty(searchQuery) ||
                     preset.name.ToLower().Contains(searchQuery.ToLower()) ||
                     preset.category.ToLower().Contains(searchQuery.ToLower()) ||
                     preset.shaderName.ToLower().Contains(searchQuery.ToLower())))
                .ToList();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var groupedPresets = filteredPresets.GroupBy(p => p.category).OrderBy(g => g.Key);

            foreach (var categoryGroup in groupedPresets)
            {
                DrawCategoryGroup(categoryGroup.Key, categoryGroup.ToList());
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCategoryGroup(string category, List<MaterialPreset> presets)
        {
            GUILayout.Space(5);

            if (!categoryFoldStates.ContainsKey(category))
                categoryFoldStates[category] = true;

            var foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            EditorGUILayout.BeginHorizontal();
            categoryFoldStates[category] = EditorGUILayout.Foldout(
                categoryFoldStates[category],
                $"📁 {category} ({presets.Count})",
                foldoutStyle
            );

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("⋮", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                ShowCategoryContextMenu(category);
            }

            EditorGUILayout.EndHorizontal();

            if (categoryFoldStates[category])
            {
                EditorGUI.indentLevel++;

                var sortedPresets = presets.OrderByDescending(p => p.useCount).ThenBy(p => p.name);

                foreach (var preset in sortedPresets)
                {
                    DrawPresetItem(preset);
                }

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(5);
        }

        private void DrawPresetItem(MaterialPreset preset)
        {
            var itemRect = GUILayoutUtility.GetRect(0, ITEM_HEIGHT + 4, GUILayout.ExpandWidth(true));

            if (preset.useCount > 0)
            {
                EditorGUI.DrawRect(itemRect, new Color(0.2f, 0.4f, 0.2f, 0.1f));
            }

            GUILayout.Space(-(ITEM_HEIGHT + 4));

            EditorGUILayout.BeginHorizontal(GUILayout.Height(ITEM_HEIGHT + 4));

            EditorGUILayout.BeginVertical();

            var nameStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField($"🎨 {preset.name}", nameStyle);

            var detailText = $"{preset.shaderName}";
            if (preset.useCount > 0)
            {
                detailText += $" • Used {preset.useCount} times";
            }
            EditorGUILayout.LabelField(detailText, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUI.enabled = selectedMaterial != null;
            if (GUILayout.Button(new GUIContent("🎯 Apply"), EditorStyles.miniButton, GUILayout.Width(SMALL_BUTTON_WIDTH + 10)))
            {
                ApplyPreset(preset);
            }
            GUI.enabled = true;

            if (GUILayout.Button(new GUIContent("📋 Copy"), EditorStyles.miniButton, GUILayout.Width(SMALL_BUTTON_WIDTH + 10)))
            {
                DuplicatePreset(preset);
            }

            if (GUILayout.Button("⋮", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                ShowPresetContextMenu(preset);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(5);

            var separatorRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(separatorRect, Color.gray);

            EditorGUILayout.BeginHorizontal();

            var totalPresets = materialPresets.Count;
            var categoriesCount = materialPresets.Select(p => p.category).Distinct().Count();

            EditorGUILayout.LabelField($"🎨 Presets: {totalPresets}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"📁 Categories: {categoriesCount}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        #region Preset Operations

        private void CreatePreset()
        {
            if (presetSourceMaterial == null || string.IsNullOrEmpty(newPresetName) || string.IsNullOrEmpty(newPresetCategory))
                return;

            if (materialPresets.Any(p => p.name == newPresetName && p.category == newPresetCategory))
            {
                if (!EditorUtility.DisplayDialog("Duplicate Name",
                    $"A preset named '{newPresetName}' already exists in category '{newPresetCategory}'. Overwrite?",
                    "Overwrite", "Cancel"))
                    return;

                materialPresets.RemoveAll(p => p.name == newPresetName && p.category == newPresetCategory);
            }

            var preset = new MaterialPreset(newPresetName, newPresetCategory, presetSourceMaterial);
            materialPresets.Add(preset);

            SavePresets();
            UpdateCategoryList();

            EditorUtility.DisplayDialog("Success", $"Created preset '{newPresetName}' in category '{newPresetCategory}'", "OK");

            newPresetName = "";
            showCreatePreset = false;
        }

        private void ApplyPreset(MaterialPreset preset)
        {
            if (selectedMaterial == null)
            {
                EditorUtility.DisplayDialog("No Material Selected", "Please select a material to apply the preset to.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Apply Preset",
                $"Apply preset '{preset.name}' to material '{selectedMaterial.name}'?\n\nThis will overwrite current material settings.",
                "Apply", "Cancel"))
            {
                Undo.RecordObject(selectedMaterial, $"Apply Material Preset: {preset.name}");
                preset.ApplyToMaterial(selectedMaterial);
                SavePresets();

                Debug.Log($"Applied material preset '{preset.name}' to '{selectedMaterial.name}'");
            }
        }

        private void DuplicatePreset(MaterialPreset preset)
        {
            string newName = $"{preset.name} Copy";
            int counter = 1;

            while (materialPresets.Any(p => p.name == newName && p.category == preset.category))
            {
                newName = $"{preset.name} Copy {counter}";
                counter++;
            }

            var duplicatePreset = JsonConvert.DeserializeObject<MaterialPreset>(JsonConvert.SerializeObject(preset));
            duplicatePreset.name = newName;
            duplicatePreset.createdTime = DateTime.Now;
            duplicatePreset.useCount = 0;

            materialPresets.Add(duplicatePreset);
            SavePresets();

            Debug.Log($"Duplicated preset as '{newName}'");
        }

        #endregion

        #region Context Menus

        private void ShowPresetContextMenu(MaterialPreset preset)
        {
            GenericMenu menu = new GenericMenu();

            if (selectedMaterial != null)
            {
                menu.AddItem(new GUIContent("🎯 Apply to Selected Material"), false, () => ApplyPreset(preset));
            }

            menu.AddItem(new GUIContent("📋 Duplicate Preset"), false, () => DuplicatePreset(preset));
            menu.AddItem(new GUIContent("✏️ Rename Preset"), false, () => RenamePreset(preset));
            menu.AddItem(new GUIContent("📁 Change Category"), false, () => ChangePresetCategory(preset));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("📄 Export Preset"), false, () => ExportPreset(preset));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("🗑️ Delete Preset"), false, () => DeletePreset(preset));

            menu.ShowAsContext();
        }

        private void ShowCategoryContextMenu(string category)
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("✏️ Rename Category"), false, () => RenameCategory(category));

            var categoryPresets = materialPresets.Where(p => p.category == category).ToList();
            if (categoryPresets.Count > 0)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent($"🗑️ Delete Category ({categoryPresets.Count} presets)"), false, () => DeleteCategory(category));
            }

            menu.ShowAsContext();
        }

        private void RenamePreset(MaterialPreset preset)
        {
            string newName = EditorUtility.DisplayDialog("Rename Preset", "", "Rename", "Cancel") ?
                "New Name" : "";

            if (!string.IsNullOrEmpty(newName) && newName != preset.name)
            {
                preset.name = newName;
                SavePresets();
            }
        }

        private void ChangePresetCategory(MaterialPreset preset)
        {
            if (availableCategories.Count > 0)
            {
                Debug.Log($"Change category for {preset.name}");
            }
        }

        private void ExportPreset(MaterialPreset preset)
        {
            string path = EditorUtility.SaveFilePanel("Export Material Preset", "", $"{preset.name}.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = JsonConvert.SerializeObject(preset, Formatting.Indented);
                    File.WriteAllText(path, json);
                    EditorUtility.DisplayDialog("Success", $"Exported preset to {path}", "OK");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to export preset: {e.Message}", "OK");
                }
            }
        }

        private void DeletePreset(MaterialPreset preset)
        {
            if (EditorUtility.DisplayDialog("Delete Preset",
                $"Delete preset '{preset.name}'?\n\nThis action cannot be undone.",
                "Delete", "Cancel"))
            {
                materialPresets.Remove(preset);
                SavePresets();
                UpdateCategoryList();
            }
        }

        private void RenameCategory(string oldCategoryName)
        {
            Debug.Log($"Rename category: {oldCategoryName}");
        }

        private void DeleteCategory(string category)
        {
            var categoryPresets = materialPresets.Where(p => p.category == category).ToList();

            if (EditorUtility.DisplayDialog("Delete Category",
                $"Delete category '{category}' and all {categoryPresets.Count} presets in it?\n\nThis action cannot be undone.",
                "Delete", "Cancel"))
            {
                materialPresets.RemoveAll(p => p.category == category);
                SavePresets();
                UpdateCategoryList();
            }
        }

        #endregion

        #region Data Management

        private static void InitializePresetManager()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            presetsDataPath = Path.Combine(appDataPath, "SDKTools/material_presets.json");
        }

        private static void LoadPresets()
        {
            if (File.Exists(presetsDataPath))
            {
                try
                {
                    string json = File.ReadAllText(presetsDataPath);

                    if (string.IsNullOrEmpty(json))
                    {
                        materialPresets = new List<MaterialPreset>();
                        return;
                    }

                    var jsonSettings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        Error = (sender, args) =>
                        {
                            Debug.LogWarning($"JSON deserialization warning: {args.ErrorContext.Error.Message}");
                            args.ErrorContext.Handled = true;
                        }
                    };

                    var loadedPresets = JsonConvert.DeserializeObject<List<MaterialPreset>>(json, jsonSettings);

                    materialPresets = new List<MaterialPreset>();
                    if (loadedPresets != null)
                    {
                        foreach (var preset in loadedPresets)
                        {
                            if (preset != null && !string.IsNullOrEmpty(preset.name))
                            {
                                if (preset.properties == null)
                                    preset.properties = new List<MaterialProperty>();
                                if (preset.textures == null)
                                    preset.textures = new List<TextureProperty>();

                                materialPresets.Add(preset);
                            }
                        }
                    }

                    Debug.Log($"[MaterialPresetManager] Loaded {materialPresets.Count} valid presets");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MaterialPresetManager] Failed to load presets: {e.Message}");
                    materialPresets = new List<MaterialPreset>();

                    try
                    {
                        string backupPath = presetsDataPath + ".backup";
                        File.Copy(presetsDataPath, backupPath, true);
                        Debug.Log($"[MaterialPresetManager] Backed up corrupted file to: {backupPath}");
                    }
                    catch
                    {
                        Debug.LogWarning("[MaterialPresetManager] Could not create backup of corrupted file");
                    }
                }
            }
            else
            {
                materialPresets = new List<MaterialPreset>();
            }
        }

        private static void SavePresets()
        {
            try
            {
                string directory = Path.GetDirectoryName(presetsDataPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var jsonSettings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                string json = JsonConvert.SerializeObject(materialPresets, jsonSettings);
                File.WriteAllText(presetsDataPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MaterialPresetManager] Failed to save presets: {e.Message}");
            }
        }

        private void UpdateCategoryList()
        {
            availableCategories = materialPresets.Select(p => p.category).Distinct().OrderBy(c => c).ToList();

            if (!availableCategories.Contains("General"))
            {
                availableCategories.Insert(0, "General");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Apply a preset by name to a material
        /// </summary>
        public static bool ApplyPresetByName(string presetName, Material material)
        {
            InitializePresetManager();
            LoadPresets();

            var preset = materialPresets.FirstOrDefault(p => p.name == presetName);
            if (preset != null && material != null)
            {
                preset.ApplyToMaterial(material);
                SavePresets();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get all preset names for a category
        /// </summary>
        public static List<string> GetPresetNames(string category = null)
        {
            InitializePresetManager();
            LoadPresets();

            return materialPresets
                .Where(p => string.IsNullOrEmpty(category) || p.category == category)
                .Select(p => p.name)
                .ToList();
        }

        #endregion
    }

    public static class MaterialPresetContextMenu
    {
        [MenuItem("CONTEXT/Material/Save as Material Preset")]
        private static void SaveMaterialAsPreset(MenuCommand command)
        {
            Material material = command.context as Material;
            if (material != null)
            {
                MaterialPresetManager.ShowWindow();
                var window = EditorWindow.GetWindow<MaterialPresetManager>();
                window.SetMaterialForPreset(material);
            }
        }

        [MenuItem("CONTEXT/Material/Apply Material Preset")]
        private static void ApplyMaterialPreset(MenuCommand command)
        {
            Material material = command.context as Material;
            if (material != null)
            {
                MaterialPresetManager.ShowWindow();
                var window = EditorWindow.GetWindow<MaterialPresetManager>();
                window.SetSelectedMaterial(material);
            }
        }
    }

    public static class MaterialPresetExtensions
    {
        public static void SaveAsPreset(this Material material, string name, string category = "General")
        {
            MaterialPresetManager.CreatePresetFromMaterial(material, name, category);
        }

        public static bool ApplyPreset(this Material material, string presetName)
        {
            return MaterialPresetManager.ApplyPresetByName(presetName, material);
        }
    }

    [CustomEditor(typeof(Material))]
    public class MaterialPresetInspector : MaterialEditor
    {
        private bool showPresetSection = false;
        private string selectedPresetName = "";
        private List<string> availablePresets = new List<string>();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(10);

            var sectionStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };

            showPresetSection = EditorGUILayout.Foldout(showPresetSection, "🎨 Material Presets", sectionStyle);

            if (showPresetSection)
            {
                EditorGUI.indentLevel++;
                DrawPresetSection();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawPresetSection()
        {
            var panelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 8, 8)
            };

            EditorGUILayout.BeginVertical(panelStyle);

            EditorGUILayout.LabelField("Quick Apply", EditorStyles.boldLabel);

            RefreshAvailablePresets();

            if (availablePresets.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();

                int selectedIndex = Mathf.Max(0, availablePresets.IndexOf(selectedPresetName));
                selectedIndex = EditorGUILayout.Popup("Preset:", selectedIndex, availablePresets.ToArray());

                if (selectedIndex >= 0 && selectedIndex < availablePresets.Count)
                {
                    selectedPresetName = availablePresets[selectedIndex];
                }

                if (GUILayout.Button("🎯 Apply", GUILayout.Width(60)))
                {
                    ApplySelectedPreset();
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("No presets available", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("💾 Save as Preset"))
            {
                SaveCurrentMaterialAsPreset();
            }

            if (GUILayout.Button("🎨 Open Preset Manager"))
            {
                MaterialPresetManager.ShowWindow();
                var window = EditorWindow.GetWindow<MaterialPresetManager>();
                window.SetSelectedMaterial(target as Material);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void RefreshAvailablePresets()
        {
            availablePresets = MaterialPresetManager.GetPresetNames();
        }

        private void ApplySelectedPreset()
        {
            if (!string.IsNullOrEmpty(selectedPresetName) && target != null)
            {
                var material = target as Material;
                if (MaterialPresetManager.ApplyPresetByName(selectedPresetName, material))
                {
                    EditorUtility.SetDirty(material);
                    Debug.Log($"Applied preset '{selectedPresetName}' to material '{material.name}'");
                }
                else
                {
                    Debug.LogWarning($"Failed to apply preset '{selectedPresetName}'");
                }
            }
        }

        private void SaveCurrentMaterialAsPreset()
        {
            var material = target as Material;
            if (material != null)
            {
                MaterialPresetManager.ShowWindow();
                var window = EditorWindow.GetWindow<MaterialPresetManager>();
                window.SetMaterialForPreset(material);
                window.ShowCreatePresetPanel();
            }
        }
    }

    public partial class MaterialPresetManager : EditorWindow
    {
        public void SetSelectedMaterial(Material material)
        {
            selectedMaterial = material;
            Repaint();
        }

        public void SetMaterialForPreset(Material material)
        {
            presetSourceMaterial = material;
            selectedMaterial = material;
            if (material != null)
            {
                newPresetName = material.name + " Preset";
            }
            showCreatePreset = true;
            Repaint();
        }

        public void ShowCreatePresetPanel()
        {
            showCreatePreset = true;
            Repaint();
        }

        public static void CreatePresetFromMaterial(Material material, string name, string category)
        {
            InitializePresetManager();
            LoadPresets();

            if (materialPresets.Any(p => p.name == name && p.category == category))
            {
                Debug.LogWarning($"Preset '{name}' already exists in category '{category}'");
                return;
            }

            var preset = new MaterialPreset(name, category, material);
            materialPresets.Add(preset);
            SavePresets();

            Debug.Log($"Created material preset '{name}' in category '{category}'");
        }
    }

    public static class MaterialPresetBatchOperations
    {
        [MenuItem("VRChat SDK/SDKTools/Batch Apply Material Preset")]
        public static void ShowBatchApplyWindow()
        {
            BatchMaterialPresetWindow.ShowWindow();
        }
    }

    public class BatchMaterialPresetWindow : EditorWindow
    {
        private List<Material> targetMaterials = new List<Material>();
        private string selectedPreset = "";
        private Vector2 scrollPosition;

        public static void ShowWindow()
        {
            var window = GetWindow<BatchMaterialPresetWindow>("Batch Apply Presets");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Batch Apply Material Presets", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            var availablePresets = MaterialPresetManager.GetPresetNames();
            if (availablePresets.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Preset to Apply:", GUILayout.Width(100));

                int selectedIndex = Mathf.Max(0, availablePresets.IndexOf(selectedPreset));
                selectedIndex = EditorGUILayout.Popup(selectedIndex, availablePresets.ToArray());

                if (selectedIndex >= 0 && selectedIndex < availablePresets.Count)
                {
                    selectedPreset = availablePresets[selectedIndex];
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Target Materials:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Selected Materials"))
            {
                AddSelectedMaterials();
            }
            if (GUILayout.Button("Clear All"))
            {
                targetMaterials.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            for (int i = targetMaterials.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();

                targetMaterials[i] = (Material)EditorGUILayout.ObjectField(targetMaterials[i], typeof(Material), false);

                if (GUILayout.Button("✕", GUILayout.Width(25)))
                {
                    targetMaterials.RemoveAt(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            var newMaterial = (Material)EditorGUILayout.ObjectField("Add Material:", null, typeof(Material), false);
            if (newMaterial != null && !targetMaterials.Contains(newMaterial))
            {
                targetMaterials.Add(newMaterial);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            GUI.enabled = !string.IsNullOrEmpty(selectedPreset) && targetMaterials.Count > 0 && targetMaterials.All(m => m != null);

            if (GUILayout.Button($"🎯 Apply '{selectedPreset}' to {targetMaterials.Count} Materials", GUILayout.Height(30)))
            {
                ApplyPresetToAllMaterials();
            }

            GUI.enabled = true;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Materials: {targetMaterials.Count}", EditorStyles.miniLabel);
        }

        private void AddSelectedMaterials()
        {
            var selectedObjects = Selection.objects;
            foreach (var obj in selectedObjects)
            {
                if (obj is Material material && !targetMaterials.Contains(material))
                {
                    targetMaterials.Add(material);
                }
                else if (obj is GameObject gameObject)
                {
                    var renderers = gameObject.GetComponentsInChildren<Renderer>();
                    foreach (var renderer in renderers)
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat != null && !targetMaterials.Contains(mat))
                            {
                                targetMaterials.Add(mat);
                            }
                        }
                    }
                }
            }
        }

        private void ApplyPresetToAllMaterials()
        {
            if (EditorUtility.DisplayDialog("Batch Apply Preset",
                $"Apply preset '{selectedPreset}' to {targetMaterials.Count} materials?\n\nThis will overwrite current material settings.",
                "Apply", "Cancel"))
            {
                int successCount = 0;

                foreach (var material in targetMaterials)
                {
                    if (material != null)
                    {
                        Undo.RecordObject(material, $"Batch Apply Preset: {selectedPreset}");
                        if (MaterialPresetManager.ApplyPresetByName(selectedPreset, material))
                        {
                            successCount++;
                        }
                    }
                }

                EditorUtility.DisplayDialog("Batch Apply Complete",
                    $"Successfully applied preset to {successCount}/{targetMaterials.Count} materials.", "OK");
            }
        }
    }
}
#endif