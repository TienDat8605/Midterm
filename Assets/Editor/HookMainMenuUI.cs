using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class HookMainMenuUI
{
    [MenuItem("Tools/Hook MainMenu UI")]
    public static void HookIt()
    {
        // Create the GameObject
        var go = new GameObject("MainMenu UI");
        var uiDoc = go.AddComponent<UIDocument>();

        // Assign the newly created UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/MainMenu.uxml");
        if (visualTree != null)
        {
            uiDoc.visualTreeAsset = visualTree;
            Debug.Log("Successfully assigned MainMenu.uxml!");
        }
        else
        {
            Debug.LogError("Could not find Assets/UI Toolkit/MainMenu.uxml");
        }

        // Auto-assign the first available Panel Settings
        string[] panelGuids = AssetDatabase.FindAssets("t:PanelSettings");
        if (panelGuids.Length > 0)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(panelGuids[0]));
            uiDoc.panelSettings = panelSettings;
        }
        else
        {
            Debug.LogWarning("No PanelSettings found in the project. Please create one (Right Click > Create > UI Toolkit > Panel Settings Asset) and assign it.");
        }

        // Register for Undo and select it in the Hierarchy
        Undo.RegisterCreatedObjectUndo(go, "Create MainMenu UI");
        Selection.activeGameObject = go;
    }
}
