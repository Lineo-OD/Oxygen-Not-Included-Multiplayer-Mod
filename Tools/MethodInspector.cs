using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace OniMultiplayer.Tools
{
    /// <summary>
    /// Utility to inspect ONI class methods at runtime.
    /// Press F10 in-game to dump method signatures to the log.
    /// </summary>
    public class MethodInspector : MonoBehaviour
    {
        private static MethodInspector _instance;
        private bool _showGui = false;
        private string _className = "DigTool";
        private string _lastResult = "";
        private Vector2 _scrollPos;

        public static void Initialize()
        {
            if (_instance != null) return;
            var go = new GameObject("OniMP_MethodInspector");
            _instance = go.AddComponent<MethodInspector>();
            DontDestroyOnLoad(go);
            OniMultiplayerMod.Log("MethodInspector initialized - Press F10 to open");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _showGui = !_showGui;
            }
        }

        private void OnGUI()
        {
            if (!_showGui) return;

            GUI.Window(54321, new Rect(50, 50, 700, 500), DrawWindow, "ONI Method Inspector (F10 to close)");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Enter class name to inspect:");
            GUILayout.BeginHorizontal();
            _className = GUILayout.TextField(_className, GUILayout.Width(300));
            if (GUILayout.Button("Inspect", GUILayout.Width(100)))
            {
                _lastResult = InspectClass(_className);
            }
            if (GUILayout.Button("Copy to Log", GUILayout.Width(100)))
            {
                OniMultiplayerMod.Log($"\n=== Methods for {_className} ===\n{_lastResult}");
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Quick inspect common classes:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("DigTool")) { _className = "DigTool"; _lastResult = InspectClass(_className); }
            if (GUILayout.Button("BuildTool")) { _className = "BuildTool"; _lastResult = InspectClass(_className); }
            if (GUILayout.Button("Chore")) { _className = "Chore"; _lastResult = InspectClass(_className); }
            if (GUILayout.Button("ChoreDriver")) { _className = "ChoreDriver"; _lastResult = InspectClass(_className); }
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ChoreConsumer")) { _className = "ChoreConsumer"; _lastResult = InspectClass(_className); }
            if (GUILayout.Button("Workable")) { _className = "Workable"; _lastResult = InspectClass(_className); }
            if (GUILayout.Button("Deconstructable")) { _className = "Deconstructable"; _lastResult = InspectClass(_className); }
            if (GUILayout.Button("Navigator")) { _className = "Navigator"; _lastResult = InspectClass(_className); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("KBatchedAnimController")) { _className = "KBatchedAnimController"; _lastResult = InspectClass(_className); }
            if (GUILayout.Button("MinionIdentity")) { _className = "MinionIdentity"; _lastResult = InspectClass(_className); }
            if (GUILayout.Button("WorldDamage")) { _className = "WorldDamage"; _lastResult = InspectClass(_className); }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Results:");

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(300));
            GUILayout.TextArea(_lastResult, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private string InspectClass(string className)
        {
            var sb = new StringBuilder();

            try
            {
                // Search in all loaded assemblies
                Type targetType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className);
                        if (targetType != null) break;
                    }
                    catch { /* Skip assemblies that throw on GetTypes() */ }
                }

                if (targetType == null)
                {
                    return $"Class '{className}' not found in any loaded assembly.";
                }

                sb.AppendLine($"=== {targetType.FullName} ===");
                sb.AppendLine($"Assembly: {targetType.Assembly.GetName().Name}");
                sb.AppendLine($"Base: {targetType.BaseType?.Name ?? "none"}");
                sb.AppendLine();

                // Get all methods
                var methods = targetType.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | 
                    BindingFlags.Instance | BindingFlags.Static | 
                    BindingFlags.DeclaredOnly);

                sb.AppendLine($"--- Methods ({methods.Length}) ---");
                foreach (var method in methods.OrderBy(m => m.Name))
                {
                    var modifiers = new StringBuilder();
                    if (method.IsPublic) modifiers.Append("public ");
                    else if (method.IsPrivate) modifiers.Append("private ");
                    else if (method.IsFamily) modifiers.Append("protected ");
                    
                    if (method.IsStatic) modifiers.Append("static ");
                    if (method.IsVirtual) modifiers.Append("virtual ");
                    if (method.IsAbstract) modifiers.Append("abstract ");

                    var parameters = method.GetParameters()
                        .Select(p => $"{p.ParameterType.Name} {p.Name}")
                        .ToArray();

                    sb.AppendLine($"{modifiers}{method.ReturnType.Name} {method.Name}({string.Join(", ", parameters)})");
                }

                sb.AppendLine();
                sb.AppendLine("--- Fields ---");
                var fields = targetType.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.DeclaredOnly);

                foreach (var field in fields.OrderBy(f => f.Name).Take(30)) // Limit to 30
                {
                    var modifiers = field.IsPublic ? "public" : field.IsPrivate ? "private" : "protected";
                    if (field.IsStatic) modifiers += " static";
                    sb.AppendLine($"{modifiers} {field.FieldType.Name} {field.Name}");
                }

                if (fields.Length > 30)
                {
                    sb.AppendLine($"... and {fields.Length - 30} more fields");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error: {ex.Message}");
            }

            return sb.ToString();
        }
    }
}