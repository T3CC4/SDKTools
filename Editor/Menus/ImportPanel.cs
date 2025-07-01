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
    public class ImportPanel : EditorWindow
    {
        private string searchQuery = "";
        private bool showBulkImport = false;
        private Vector2 scrollPosition;

        private static string jsonFilePath;
        public static Dictionary<string, List<string>> fileCategories;

        private Dictionary<string, bool> selectedPackages = new Dictionary<string, bool>();
        private bool selectAll = false;

        private Dictionary<string, bool> categoryFoldStates = new Dictionary<string, bool>();

        private string draggedPackage = "";
        private string draggedFromCategory = "";
        private bool isDragging = false;
        private Vector2 dragStartPosition;

        private const float TOOLBAR_HEIGHT = 28f;
        private const float SECTION_SPACING = 8f;
        private const float ITEM_HEIGHT = 24f;
        private const float PANEL_PADDING = 12f;
        private const float BUTTON_WIDTH = 85f;
        private const float MINI_BUTTON_WIDTH = 65f;

        [MenuItem("VRChat SDK/SDKTools/Import Panel")]
        public static void ShowWindow()
        {
            var window = GetWindow<ImportPanel>("SDKTools Import Panel");
            window.minSize = new Vector2(520, 400);
            window.Show();
        }

        private void OnEnable()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools");
            if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath);
            MoveFilesToFolder(Path.Combine(Directory.GetCurrentDirectory(), "Packages/SDKTools/Runtime/Imports"), destinationPath);
            LoadFileCategories();
            AssignUncategorizedCategory();
        }

        private void OnGUI()
        {
            HandleDragAndDrop();
            HandlePackageDragAndDrop();

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            GUILayout.Space(PANEL_PADDING);

            DrawHeader();
            DrawToolbar();

            GUILayout.Space(SECTION_SPACING);

            DrawMainContent();

            GUILayout.Space(PANEL_PADDING);
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);
            var titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField("SDKTools Import Panel", titleStyle);
            GUILayout.Space(15);
        }

        private void DrawToolbar()
        {
            var toolbarStyle = new GUIStyle(EditorStyles.toolbar)
            {
                fixedHeight = TOOLBAR_HEIGHT + 6
            };

            EditorGUILayout.BeginHorizontal(toolbarStyle);

            GUILayout.Space(8);

            GUILayout.Label("🔍", GUILayout.Width(20));
            var newSearchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(220));
            if (newSearchQuery != searchQuery)
            {
                searchQuery = newSearchQuery;
            }

            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton, GUILayout.Width(BUTTON_WIDTH + 10)))
            {
                LoadFileCategories();
                AssignUncategorizedCategory();
            }

            GUILayout.Space(3);

            if (GUILayout.Button("📁 Add Package", EditorStyles.toolbarButton, GUILayout.Width(105)))
            {
                AddPackageFromFile();
            }

            GUILayout.Space(3);

            var bulkContent = new GUIContent(showBulkImport ? "📦 Hide Bulk" : "📦 Bulk Import");
            if (GUILayout.Button(bulkContent, EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                showBulkImport = !showBulkImport;
                if (!showBulkImport) selectedPackages.Clear();
            }

            GUILayout.Space(3);

            var historyContent = new GUIContent("📦 Package Tracker");
            if (GUILayout.Button(historyContent, EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                PackageTrackerWindow.ShowWindow();
            }

            GUILayout.Space(8);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMainContent()
        {
            if (showBulkImport)
            {
                DrawBulkImportPanel();
                GUILayout.Space(SECTION_SPACING);
            }

            DrawDragDropZone();
            GUILayout.Space(SECTION_SPACING);

            DrawPackageList();
        }

        private void DrawBulkImportPanel()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };
            EditorGUILayout.LabelField("📦 Bulk Import", headerStyle);
            GUILayout.Space(4);

            var panelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };

            EditorGUILayout.BeginVertical(panelStyle);

            EditorGUILayout.BeginHorizontal();

            bool newSelectAll = EditorGUILayout.Toggle("Select All", selectAll, GUILayout.Width(85));
            if (newSelectAll != selectAll)
            {
                selectAll = newSelectAll;
                foreach (var category in fileCategories)
                {
                    foreach (var file in category.Value)
                    {
                        selectedPackages[file] = selectAll;
                    }
                }
            }

            GUILayout.Space(20);

            int selectedCount = selectedPackages.Values.Count(x => x);
            var countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField($"Selected: {selectedCount} packages", countStyle);

            GUILayout.FlexibleSpace();

            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button("⚡ Import Selected", GUILayout.Width(130)))
            {
                ImportSelectedPackages();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawDragDropZone()
        {
            var rect = GUILayoutUtility.GetRect(0, 65, GUILayout.ExpandWidth(true));

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            var borderColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, 2), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + rect.height - 4, rect.width - 4, 2), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + 2, 2, rect.height - 4), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 4, rect.y + 2, 2, rect.height - 4), borderColor);

            for (int i = 0; i < 10; i += 3)
            {
                EditorGUI.DrawRect(new Rect(rect.x + 6 + i, rect.y + 6, 2, 2), borderColor);
                EditorGUI.DrawRect(new Rect(rect.x + rect.width - 8 - i, rect.y + rect.height - 8, 2, 2), borderColor);
            }

            GUI.Box(rect, "📦 Drag & Drop .unitypackage files here\n\nOr use 'Add Package' button above", style);
        }

        private void DrawPackageList()
        {
            if (fileCategories == null) return;

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📚 Package Library", headerStyle);
            GUILayout.FlexibleSpace();

            var instructionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Italic
            };
            EditorGUILayout.LabelField("Right-click for options • Drag to move between categories", instructionStyle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            var categoriesCopy = new Dictionary<string, List<string>>(fileCategories);

            foreach (var category in categoriesCopy)
            {
                string categoryName = category.Key;

                var matchingFiles = category.Value
                    .Where(filePath => string.IsNullOrEmpty(searchQuery) ||
                           filePath.ToLower().Contains(searchQuery.ToLower()))
                    .ToList();

                if (matchingFiles.Count == 0) continue;

                DrawCategorySection(categoryName, matchingFiles);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            var separatorRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            GUILayout.Space(6);

            int totalFiles = fileCategories?.Values.Sum(list => list.Count) ?? 0;
            var footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField($"📊 Total Files: {totalFiles}", footerStyle);
        }

        private void DrawCategorySection(string categoryName, List<string> files)
        {
            GUILayout.Space(6);
            var categoryRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));

            if (isDragging && categoryName != draggedFromCategory)
            {
                EditorGUI.DrawRect(categoryRect, new Color(0.3f, 0.6f, 1f, 0.2f));
                EditorGUI.DrawRect(new Rect(categoryRect.x, categoryRect.y, categoryRect.width, 2), new Color(0.3f, 0.6f, 1f, 0.8f));
                EditorGUI.DrawRect(new Rect(categoryRect.x, categoryRect.y + categoryRect.height - 2, categoryRect.width, 2), new Color(0.3f, 0.6f, 1f, 0.8f));
            }

            GUILayout.Space(-28);

            if (!categoryFoldStates.ContainsKey(categoryName))
                categoryFoldStates[categoryName] = true;

            var foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            EditorGUILayout.BeginHorizontal(GUILayout.Height(28));

            categoryFoldStates[categoryName] = EditorGUILayout.Foldout(
                categoryFoldStates[categoryName],
                $"📁 {categoryName} ({files.Count})",
                foldoutStyle
            );

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("⋮", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                ShowCategoryContextMenu(categoryName);
            }

            EditorGUILayout.EndHorizontal();

            if (isDragging && categoryRect.Contains(Event.current.mousePosition) && categoryName != draggedFromCategory)
            {
                if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    MoveFileToCategory(draggedPackage, draggedFromCategory, categoryName);
                    isDragging = false;
                    draggedPackage = "";
                    draggedFromCategory = "";
                    Event.current.Use();
                    Repaint();
                }
            }

            if (categoryFoldStates[categoryName])
            {
                var contentHeight = files.Count * (ITEM_HEIGHT + 3) + 12;
                var contentRect = GUILayoutUtility.GetRect(0, contentHeight, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(contentRect, new Color(0f, 0f, 0f, 0.04f));
                GUILayout.Space(-contentHeight);

                GUILayout.Space(6);
                EditorGUI.indentLevel++;

                foreach (var filePath in files)
                {
                    DrawPackageItem(filePath, categoryName);
                    GUILayout.Space(3);
                }

                EditorGUI.indentLevel--;
                GUILayout.Space(6);
            }

            GUILayout.Space(SECTION_SPACING);
        }

        private void DrawPackageItem(string filePath, string categoryName)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools/");

            var itemRect = GUILayoutUtility.GetRect(0, ITEM_HEIGHT, GUILayout.ExpandWidth(true));

            if (isDragging && draggedPackage == filePath)
            {
                EditorGUI.DrawRect(itemRect, new Color(1f, 1f, 0f, 0.3f));
            }

            GUILayout.Space(-ITEM_HEIGHT);

            EditorGUILayout.BeginHorizontal(GUILayout.Height(ITEM_HEIGHT));

            if (showBulkImport)
            {
                bool isSelected = selectedPackages.ContainsKey(filePath) ? selectedPackages[filePath] : false;
                selectedPackages[filePath] = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                GUILayout.Space(5);
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);

            var dragRect = GUILayoutUtility.GetRect(15, ITEM_HEIGHT);
            GUI.Label(dragRect, "⋮⋮", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });

            if (dragRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    isDragging = true;
                    draggedPackage = filePath;
                    draggedFromCategory = categoryName;
                    dragStartPosition = Event.current.mousePosition;
                    Event.current.Use();
                }
            }

            if (itemRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2)
                {
                    string fullPath = destinationPath + filePath;
                    AssetDatabase.ImportPackage(fullPath, true);
                    Event.current.Use();
                    return;
                }
            }

            var buttonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Normal
            };

            if (GUILayout.Button($"📦 {fileName}", buttonStyle, GUILayout.MinWidth(200)))
            {
                string fullPath = destinationPath + filePath;

                PackageTrackerWindow.TrackPackageImport(fullPath);
                AssetDatabase.ImportPackage(fullPath, true);
                PackageTrackerWindow.CompletePackageTracking(fileName, fullPath);
            }

            GUILayout.FlexibleSpace();

            try
            {
                var fileInfo = new FileInfo(destinationPath + filePath);
                var sizeText = FormatFileSize(fileInfo.Length);
                var dateText = fileInfo.LastWriteTime.ToString("MMM dd");

                var infoStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };

                EditorGUILayout.LabelField($"{sizeText}", infoStyle, GUILayout.Width(65));
                GUILayout.Space(8);
                EditorGUILayout.LabelField($"{dateText}", infoStyle, GUILayout.Width(50));
            }
            catch { }

            if (GUILayout.Button("⋮", EditorStyles.miniButton, GUILayout.Width(18)))
            {
                ShowPackageContextMenu(filePath, categoryName);
            }

            EditorGUILayout.EndHorizontal();

            if (itemRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ContextClick)
            {
                ShowPackageContextMenu(filePath, categoryName);
                Event.current.Use();
            }
        }

        private void ShowPackageContextMenu(string filePath, string categoryName)
        {
            GenericMenu menu = new GenericMenu();

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools/");
            string fullPath = destinationPath + filePath;

            menu.AddItem(new GUIContent("📦 Import Package"), false, () => {
                PackageTrackerWindow.TrackPackageImport(fullPath);
                AssetDatabase.ImportPackage(fullPath, true);
                PackageTrackerWindow.CompletePackageTracking(fileName, fullPath);
            });

            menu.AddItem(new GUIContent("⚡ Import Silently"), false, () => {
                PackageTrackerWindow.TrackPackageImport(fullPath);
                AssetDatabase.ImportPackage(fullPath, false);
                PackageTrackerWindow.CompletePackageTracking(fileName, fullPath);
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("📁 Move to Category/➕ New Category..."), false, () => {
                CreateNewCategoryForPackage(filePath, categoryName);
            });

            foreach (var cat in fileCategories.Keys)
            {
                if (cat != categoryName)
                {
                    string targetCategory = cat;
                    menu.AddItem(new GUIContent($"📁 Move to Category/📂 {targetCategory}"), false, () => {
                        MoveFileToCategory(filePath, categoryName, targetCategory);
                    });
                }
            }

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("🔍 Show in Explorer"), false, () => {
                EditorUtility.RevealInFinder(fullPath);
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("🗑️ Delete Package"), false, () => {
                if (EditorUtility.DisplayDialog("Delete Package",
                    $"Are you sure you want to delete '{fileName}'?\n\nThis action cannot be undone.",
                    "Delete", "Cancel"))
                {
                    try
                    {
                        File.Delete(fullPath);
                        LoadFileCategories();
                        AssignUncategorizedCategory();
                        Debug.Log($"Deleted package: {fileName}");
                    }
                    catch (Exception e)
                    {
                        EditorUtility.DisplayDialog("Error", $"Failed to delete package: {e.Message}", "OK");
                    }
                }
            });

            menu.ShowAsContext();
        }

        private void ShowCategoryContextMenu(string categoryName)
        {
            GenericMenu menu = new GenericMenu();

            if (categoryName != "Uncategorized")
            {
                menu.AddItem(new GUIContent("✏️ Rename Category"), false, () => {
                    RenameCategoryDialog(categoryName);
                });

                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent("➕ Create New Category"), false, () => {
                CreateNewCategoryDialog();
            });

            if (categoryName != "Uncategorized" && fileCategories[categoryName].Count == 0)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("🗑️ Delete Empty Category"), false, () => {
                    if (EditorUtility.DisplayDialog("Delete Category",
                        $"Are you sure you want to delete the empty category '{categoryName}'?",
                        "Delete", "Cancel"))
                    {
                        fileCategories.Remove(categoryName);
                        categoryFoldStates.Remove(categoryName);
                        SaveCategoriesToJson();
                        Debug.Log($"Deleted category: {categoryName}");
                    }
                });
            }

            menu.ShowAsContext();
        }

        private void CreateNewCategoryForPackage(string filePath, string currentCategory)
        {
            string newCategoryName = EditorUtility.DisplayDialog("Create New Category",
                "Enter name for new category:", "Create", "Cancel") ?
                ShowInputDialog("Create New Category", "Category name:", "New Category") : "";

            if (!string.IsNullOrEmpty(newCategoryName) && !fileCategories.ContainsKey(newCategoryName))
            {
                MoveFileToCategory(filePath, currentCategory, newCategoryName);
                EditorUtility.DisplayDialog("Success", $"Created category '{newCategoryName}' and moved package.", "OK");
            }
            else if (fileCategories.ContainsKey(newCategoryName))
            {
                EditorUtility.DisplayDialog("Error", "Category already exists!", "OK");
            }
        }

        private void CreateNewCategoryDialog()
        {
            string newCategoryName = ShowInputDialog("Create New Category", "Category name:", "New Category");

            if (!string.IsNullOrEmpty(newCategoryName))
            {
                if (!fileCategories.ContainsKey(newCategoryName))
                {
                    fileCategories[newCategoryName] = new List<string>();
                    SaveCategoriesToJson();
                    EditorUtility.DisplayDialog("Success", $"Created category '{newCategoryName}'.", "OK");
                    Debug.Log($"Created new category: {newCategoryName}");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Category already exists!", "OK");
                }
            }
        }

        private void RenameCategoryDialog(string oldCategoryName)
        {
            string newCategoryName = ShowInputDialog("Rename Category", "New category name:", oldCategoryName);

            if (!string.IsNullOrEmpty(newCategoryName) && newCategoryName != oldCategoryName)
            {
                if (!fileCategories.ContainsKey(newCategoryName))
                {
                    var files = fileCategories[oldCategoryName];
                    fileCategories[newCategoryName] = files;
                    fileCategories.Remove(oldCategoryName);

                    if (categoryFoldStates.ContainsKey(oldCategoryName))
                    {
                        categoryFoldStates[newCategoryName] = categoryFoldStates[oldCategoryName];
                        categoryFoldStates.Remove(oldCategoryName);
                    }

                    SaveCategoriesToJson();
                    EditorUtility.DisplayDialog("Success", $"Renamed category to '{newCategoryName}'.", "OK");
                    Debug.Log($"Renamed category from '{oldCategoryName}' to '{newCategoryName}'");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Category name already exists!", "OK");
                }
            }
        }

        private string ShowInputDialog(string title, string message, string defaultValue)
        {
            string result = "";

            var popup = ScriptableObject.CreateInstance<InputDialogPopup>();
            popup.Initialize(title, message, defaultValue, (input) => {
                result = input;
            });

            popup.ShowModal();

            return result;
        }

        public class InputDialogPopup : EditorWindow
        {
            private string title;
            private string message;
            private string inputText;
            private System.Action<string> onComplete;
            private bool isComplete = false;

            public void Initialize(string windowTitle, string promptMessage, string defaultText, System.Action<string> callback)
            {
                title = windowTitle;
                message = promptMessage;
                inputText = defaultText;
                onComplete = callback;
            }

            public void ShowModal()
            {
                titleContent = new GUIContent(title);
                position = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 75, 400, 150);
                ShowModalUtility();
            }

            private void OnGUI()
            {
                GUILayout.Space(20);

                EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);

                GUILayout.Space(10);

                GUI.SetNextControlName("InputField");
                inputText = EditorGUILayout.TextField(inputText);

                if (!isComplete)
                {
                    EditorGUI.FocusTextInControl("InputField");
                    isComplete = true;
                }

                GUILayout.Space(20);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                {
                    onComplete?.Invoke("");
                    Close();
                }

                GUILayout.Space(10);

                if (GUILayout.Button("OK", GUILayout.Width(80)) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
                {
                    onComplete?.Invoke(inputText);
                    Close();
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            return $"{bytes / (1024 * 1024):F1} MB";
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                bool validDrag = false;
                foreach (string draggedPath in DragAndDrop.paths)
                {
                    if (draggedPath.EndsWith(".unitypackage"))
                    {
                        validDrag = true;
                        break;
                    }
                }

                if (validDrag)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (string draggedPath in DragAndDrop.paths)
                        {
                            if (draggedPath.EndsWith(".unitypackage"))
                            {
                                AddPackage(draggedPath);
                            }
                        }
                    }
                    evt.Use();
                }
            }
        }

        private void HandlePackageDragAndDrop()
        {
            Event evt = Event.current;

            if (isDragging)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    Repaint();
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp && evt.button == 0)
                {
                    isDragging = false;
                    draggedPackage = "";
                    draggedFromCategory = "";
                    evt.Use();
                    Repaint();
                }
                else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    isDragging = false;
                    draggedPackage = "";
                    draggedFromCategory = "";
                    evt.Use();
                    Repaint();
                }
            }
        }

        private void AddPackageFromFile()
        {
            string filePath = EditorUtility.OpenFilePanel("Select Unity Package", "", "unitypackage");
            if (!string.IsNullOrEmpty(filePath))
            {
                AddPackage(filePath);
            }
        }

        private void AddPackage(string filePath)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools");
            string fileName = Path.GetFileName(filePath);

            try
            {
                File.Copy(filePath, Path.Combine(destinationPath, fileName), true);
                AssignUncategorizedCategory();
                Debug.Log($"Added package: {fileName}");

                EditorUtility.DisplayDialog("Package Added", $"Successfully added '{fileName}' to the library.", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to add package: {e.Message}", "OK");
            }
        }

        private void ImportSelectedPackages()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools/");

            var packagesToImport = selectedPackages.Where(x => x.Value).ToList();

            if (packagesToImport.Count == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select packages to import first.", "OK");
                return;
            }

            bool importSilently = EditorUtility.DisplayDialog("Bulk Import",
                $"Import {packagesToImport.Count} selected packages?\n\nChoose 'Yes' for interactive import or 'No' for silent import.",
                "Interactive", "Silent");

            foreach (var selected in packagesToImport)
            {
                string fullPath = destinationPath + selected.Key;
                if (File.Exists(fullPath))
                {
                    string packageName = Path.GetFileNameWithoutExtension(selected.Key);

                    PackageTrackerWindow.TrackPackageImport(fullPath);
                    AssetDatabase.ImportPackage(fullPath, importSilently);
                    PackageTrackerWindow.CompletePackageTracking(packageName, fullPath);
                }
            }

            selectedPackages.Clear();
            showBulkImport = false;

            EditorUtility.DisplayDialog("Bulk Import Complete",
                $"Successfully imported {packagesToImport.Count} packages.", "OK");
        }

        private void MoveFilesToFolder(string sourceFolderPath, string destinationFolderPath)
        {
            if (!Directory.Exists(sourceFolderPath)) return;
            if (!Directory.Exists(destinationFolderPath)) Directory.CreateDirectory(destinationFolderPath);

            string[] files = Directory.GetFiles(sourceFolderPath);

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string destinationFilePath = Path.Combine(destinationFolderPath, fileName);

                if (File.Exists(destinationFilePath))
                {
                    File.Delete(destinationFilePath);
                }

                File.Move(filePath, destinationFilePath);
            }

            Directory.Delete(sourceFolderPath, true);
            AssetDatabase.Refresh();
        }

        private void LoadFileCategories()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            jsonFilePath = Path.Combine(appDataPath, "SDKTools/categories.json");
            string destinationPath = Path.Combine(appDataPath, "SDKTools");

            if (!string.IsNullOrEmpty(jsonFilePath) && File.Exists(jsonFilePath))
            {
                try
                {
                    string json = File.ReadAllText(jsonFilePath);
                    fileCategories = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

                    var categoriesToRemove = new List<string>();

                    foreach (var category in fileCategories)
                    {
                        var filesToRemove = new List<string>();

                        foreach (var filePath in category.Value)
                        {
                            string fullPath = Path.Combine(destinationPath, filePath);

                            if (!File.Exists(fullPath))
                            {
                                filesToRemove.Add(filePath);
                            }
                        }

                        foreach (var filePath in filesToRemove)
                        {
                            category.Value.Remove(filePath);
                        }

                        if (category.Value.Count == 0 && category.Key != "Uncategorized")
                        {
                            categoriesToRemove.Add(category.Key);
                        }
                    }

                    foreach (var categoryToRemove in categoriesToRemove)
                    {
                        fileCategories.Remove(categoryToRemove);
                    }
                }
                catch
                {
                    fileCategories = new Dictionary<string, List<string>>();
                }
            }
            else
            {
                fileCategories = new Dictionary<string, List<string>>();
            }
        }

        private void AssignUncategorizedCategory()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools");

            if (!fileCategories.ContainsKey("Uncategorized"))
            {
                fileCategories["Uncategorized"] = new List<string>();
            }

            string[] unityPackages = Directory.GetFiles(destinationPath, "*.unitypackage");

            foreach (string packagePath in unityPackages)
            {
                string fileName = Path.GetFileName(packagePath);

                bool isAssigned = false;
                foreach (var category in fileCategories)
                {
                    if (category.Value.Contains(fileName))
                    {
                        isAssigned = true;
                        break;
                    }
                }

                if (!isAssigned)
                {
                    fileCategories["Uncategorized"].Add(fileName);
                }
            }

            SaveCategoriesToJson();
        }

        private void MoveFileToCategory(string filePath, string currentCategory, string newCategory)
        {
            if (fileCategories.ContainsKey(currentCategory))
            {
                fileCategories[currentCategory].Remove(filePath);

                if (fileCategories[currentCategory].Count == 0 && currentCategory != "Uncategorized")
                {
                    fileCategories.Remove(currentCategory);
                }
            }

            if (!fileCategories.ContainsKey(newCategory))
            {
                fileCategories[newCategory] = new List<string>();
            }
            fileCategories[newCategory].Add(filePath);

            SaveCategoriesToJson();

            Debug.Log($"Moved '{Path.GetFileNameWithoutExtension(filePath)}' from '{currentCategory}' to '{newCategory}'");
        }

        public static void SaveCategoriesToJson()
        {
            try
            {
                string json = JsonConvert.SerializeObject(fileCategories, Formatting.Indented);
                File.WriteAllText(jsonFilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save categories: {e.Message}");
            }
        }
    }
}
#endif