using Executor.Play;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Executor.EditorTools
{
    /// <summary>메뉴 Executor > 와이어프레임 플레이 씬 만들기: Assets/Scenes/Wireframe.unity를 만들고 연다</summary>
    public static class WireframeSceneMenu
    {
        private const string ScenePath = "Assets/Scenes/Wireframe.unity";

        [MenuItem("Executor/와이어프레임 플레이 씬 만들기")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (System.IO.File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log($"[Executor] 이미 있는 {ScenePath}를 열었습니다. Play를 누르세요.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var camera = cam.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x0b, 0x12, 0x1c, 0xff);
            cam.transform.position = new Vector3(0, 0, -10);
            new GameObject("Game", typeof(WireframeGame));

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log($"[Executor] {ScenePath}를 만들었습니다. Play를 누르세요.");
        }
    }
}
