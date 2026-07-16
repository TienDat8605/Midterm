using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class InjectNetworkManager
{
    [MenuItem("Midterm/Inject NetworkManager")]
    public static void DoInject()
    {
        var scenePath = "Assets/Scenes/UI.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        var nm = GameObject.FindObjectOfType<NetworkManager>();
        if (nm == null)
        {
            var go = new GameObject("NetworkManager");
            go.AddComponent<NetworkManager>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("NetworkManager GameObject created and saved in Init.unity.");
        }
        else
        {
            Debug.Log("NetworkManager already exists in Init.unity.");
        }
    }
}
