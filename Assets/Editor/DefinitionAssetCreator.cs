using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window for creating ScriptableObject assets for all concrete subclasses of BaseDefinition<TData>.
/// Features:
/// - Namespace grouping with foldouts
/// - Search / filter
/// - Multi-select + Create All Selected
/// - Automatically uses currently selected folder in Project window
/// - Auto-naming with collision handling
/// - Calls InitializeDefaults() after creation
/// </summary>
public class DefinitionCreatorWindow : EditorWindow
{
	private const string MENU_PATH = "Assets/Create/Open Definition Creator";
	private const string LAST_FOLDER_PREF = "DefinitionCreatorWindow_LastFolder";
	private const string FALLBACK_FOLDER = "Assets/Definitions";

	private Vector2 scroll;
	private string filter = "";
	private bool useTimestamp = false;

	private List<Type> definitionTypes = new();
	private Dictionary<string, bool> namespaceFoldouts = new();
	private HashSet<Type> selectedTypes = new();

	private string defaultFolderPath = "";

	[MenuItem(MENU_PATH, false, 80)]
	private static void OpenWindow()
	{
		var wnd = GetWindow<DefinitionCreatorWindow>("Definition Creator");
		wnd.ResolveDefaultFolderFromSelection();
		wnd.ScanForDefinitions();
		wnd.Show();
	}

	private void OnEnable()
	{
		ResolveDefaultFolderFromSelection();
		ScanForDefinitions();
	}

	private void OnFocus()
	{
		ResolveDefaultFolderFromSelection(); // Always follow current selection
		ScanForDefinitions();
	}

	private void OnGUI()
	{
		DrawHeader();
		DrawControls();
		DrawTypeList();
		DrawFooter();
	}

	private void DrawHeader()
	{
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Definition Creator", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("Creates ScriptableObject assets for concrete subclasses of BaseDefinition<TData>.\n" +
								"The currently selected folder in the Project window is used by default.", MessageType.Info);
	}

	private void DrawControls()
	{
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Filter", GUILayout.Width(40));
		string newFilter = EditorGUILayout.TextField(filter);
		if (newFilter != filter)
		{
			filter = newFilter;
			Repaint();
		}

		if (GUILayout.Button("Refresh", GUILayout.Width(80)))
			ScanForDefinitions();

		GUILayout.FlexibleSpace();

		useTimestamp = GUILayout.Toggle(useTimestamp, new GUIContent("Timestamp", "Append timestamp to generated filename"));

		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Target Folder", GUILayout.Width(85));
		EditorGUILayout.SelectableLabel(defaultFolderPath, GUILayout.Height(16));
		if (GUILayout.Button("Choose...", GUILayout.Width(90)))
		{
			var chosen = EditorUtility.OpenFolderPanel("Select folder to create assets in", Application.dataPath, "");
			if (!string.IsNullOrEmpty(chosen))
			{
				string projectPath = Application.dataPath;
				if (chosen.StartsWith(projectPath))
				{
					string rel = "Assets" + chosen.Substring(projectPath.Length);
					defaultFolderPath = rel;
					EditorPrefs.SetString(LAST_FOLDER_PREF, defaultFolderPath);
				}
				else
				{
					EditorUtility.DisplayDialog("Invalid folder", "Please choose a folder inside this Unity project.", "OK");
				}
			}
		}

		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Space();
	}

	private void DrawTypeList()
	{
		scroll = EditorGUILayout.BeginScrollView(scroll);

		if (definitionTypes.Count == 0)
		{
			EditorGUILayout.HelpBox("No definition types found. Ensure you have non-abstract classes inheriting BaseDefinition<TData>.", MessageType.Warning);
			EditorGUILayout.EndScrollView();
			return;
		}

		var grouped = definitionTypes
			.Where(t => string.IsNullOrEmpty(filter) || t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
						|| (t.Namespace ?? "<global>").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
			.GroupBy(t => string.IsNullOrEmpty(t.Namespace) ? "<global>" : t.Namespace)
			.OrderBy(g => g.Key);

		foreach (var group in grouped)
		{
			if (!namespaceFoldouts.TryGetValue(group.Key, out bool fold))
				namespaceFoldouts[group.Key] = true;

			namespaceFoldouts[group.Key] = EditorGUILayout.BeginFoldoutHeaderGroup(namespaceFoldouts[group.Key], group.Key);
			if (namespaceFoldouts[group.Key])
			{
				EditorGUI.indentLevel++;
				foreach (var t in group.OrderBy(tt => tt.Name))
					DrawTypeRow(t);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
		}

		EditorGUILayout.EndScrollView();
	}

	private void DrawTypeRow(Type t)
	{
		EditorGUILayout.BeginHorizontal("box");

		GUIContent iconContent = EditorGUIUtility.ObjectContent(null, t);
		Texture icon = iconContent.image;

		if (icon != null)
			GUILayout.Label(icon, GUILayout.Width(22), GUILayout.Height(18));
		else
			GUILayout.Space(26);

		EditorGUILayout.BeginVertical();
		EditorGUILayout.LabelField(t.Name, EditorStyles.boldLabel);
		EditorGUILayout.LabelField(t.Namespace ?? "<global>", EditorStyles.miniLabel);
		EditorGUILayout.EndVertical();

		GUILayout.FlexibleSpace();

		bool isSelected = selectedTypes.Contains(t);
		bool newSelected = GUILayout.Toggle(isSelected, "Multi-Select", GUILayout.Width(90));
		if (newSelected && !isSelected) selectedTypes.Add(t);
		if (!newSelected && isSelected) selectedTypes.Remove(t);

		if (GUILayout.Button("Create", GUILayout.Width(120)))
			CreateAssetForType(t);

		EditorGUILayout.EndHorizontal();
	}

	private void DrawFooter()
	{
		EditorGUILayout.Space();
		EditorGUILayout.BeginHorizontal();

		if (selectedTypes.Count > 0 && GUILayout.Button("Create All Selected", GUILayout.Height(30)))
			CreateAllSelected();

		GUILayout.FlexibleSpace();

		EditorGUILayout.LabelField($"Found: {definitionTypes.Count}   Selected: {selectedTypes.Count}", EditorStyles.miniLabel);
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Space();
	}

	private void ScanForDefinitions()
	{
		definitionTypes.Clear();
		selectedTypes.Clear();

		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (var asm in assemblies)
		{
			Type[] types;
			try { types = asm.GetTypes(); }
			catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(x => x != null).ToArray(); }

			foreach (var type in types)
			{
				if (type == null) continue;
				if (type.IsAbstract) continue;
				if (!typeof(ScriptableObject).IsAssignableFrom(type)) continue;

				if (IsSubclassOfRawGeneric(typeof(BaseDefinition<>), type))
					definitionTypes.Add(type);
			}
		}

		definitionTypes = definitionTypes.Distinct()
			.OrderBy(t => t.Namespace ?? "<global>")
			.ThenBy(t => t.Name)
			.ToList();
	}

	private void ResolveDefaultFolderFromSelection()
	{
		string selectionPath = GetSelectedProjectFolderPath();
		if (!string.IsNullOrEmpty(selectionPath))
		{
			defaultFolderPath = selectionPath;
			EditorPrefs.SetString(LAST_FOLDER_PREF, defaultFolderPath);
			return;
		}

		string last = EditorPrefs.GetString(LAST_FOLDER_PREF, "");
		if (!string.IsNullOrEmpty(last))
		{
			defaultFolderPath = last;
			return;
		}

		if (!AssetDatabase.IsValidFolder(FALLBACK_FOLDER))
			AssetDatabase.CreateFolder("Assets", "Definitions");

		defaultFolderPath = FALLBACK_FOLDER;
		EditorPrefs.SetString(LAST_FOLDER_PREF, defaultFolderPath);
	}

	private static string GetSelectedProjectFolderPath()
	{
		UnityEngine.Object obj = Selection.activeObject;
		if (obj == null) return null;

		string path = AssetDatabase.GetAssetPath(obj);
		if (string.IsNullOrEmpty(path)) return null;
		if (AssetDatabase.IsValidFolder(path)) return path;

		int lastSlash = path.LastIndexOf('/');
		if (lastSlash > 0)
			return path.Substring(0, lastSlash);
		return null;
	}

	private void CreateAllSelected()
	{
		if (!EnsureTargetFolderExists()) return;

		int created = 0;
		foreach (var t in selectedTypes.ToList())
		{
			if (CreateAssetInternal(t, out string _))
				created++;
		}

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		EditorUtility.DisplayDialog("Create All Selected", $"Created {created} asset(s) in {defaultFolderPath}", "OK");
	}

	private void CreateAssetForType(Type type)
	{
		if (!EnsureTargetFolderExists()) return;

		if (CreateAssetInternal(type, out string createdPath))
		{
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			var asset = AssetDatabase.LoadMainAssetAtPath(createdPath);
			Selection.activeObject = asset;
			EditorUtility.FocusProjectWindow();
		}
	}

	private bool EnsureTargetFolderExists()
	{
		if (string.IsNullOrEmpty(defaultFolderPath))
			ResolveDefaultFolderFromSelection();

		if (!AssetDatabase.IsValidFolder(defaultFolderPath))
		{
			string[] parts = defaultFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			string cur = parts[0];
			for (int i = 1; i < parts.Length; i++)
			{
				string next = cur + "/" + parts[i];
				if (!AssetDatabase.IsValidFolder(next))
					AssetDatabase.CreateFolder(cur, parts[i]);
				cur = next;
			}
		}

		EditorPrefs.SetString(LAST_FOLDER_PREF, defaultFolderPath);
		return true;
	}

	private bool CreateAssetInternal(Type type, out string createdPath)
	{
		createdPath = null;
		try
		{
			string baseName = type.Name + ".asset";
			string fileName = useTimestamp ? $"{type.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.asset" : baseName;
			string desiredPath = $"{defaultFolderPath}/{fileName}";
			string uniquePath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);

			ScriptableObject instance = ScriptableObject.CreateInstance(type);

			var initMethod = type.GetMethod("InitializeDefaults", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (initMethod != null) initMethod.Invoke(instance, null);

			AssetDatabase.CreateAsset(instance, uniquePath);
			createdPath = uniquePath;
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error creating asset for type {type.FullName}: {ex}");
			return false;
		}
	}

	private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
	{
		while (toCheck != null && toCheck != typeof(object))
		{
			Type cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
			if (cur == generic)
				return true;
			toCheck = toCheck.BaseType;
		}
		return false;
	}
}
