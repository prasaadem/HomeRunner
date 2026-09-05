using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HomeRunner.Editor
{
    public static class HomeRunnerSetup
    {
        [MenuItem("HomeRunner/Create Start Scene")]
        public static void CreateScene()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            System.IO.Directory.CreateDirectory("Assets/HomeRunner/Scenes");
            EditorSceneManager.SaveScene(scene,"Assets/HomeRunner/Scenes/HomeRunner.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/HomeRunner/Scenes/HomeRunner.unity",true)};
            Debug.Log("HomeRunner scene created. Press Play. Runtime generates the prototype.");
        }
    }
}
