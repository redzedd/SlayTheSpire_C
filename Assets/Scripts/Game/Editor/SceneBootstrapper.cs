using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using STS.Game.DataAssets;

namespace STS.Game.Editor
{
    /// <summary>
    /// 建置 Main.unity:GameRoot(GameController 自動接線)+ EventSystem(InputSystemUIInputModule,專案鐵律)。
    /// 場景檔一律經編輯器 API 生成/覆寫,絕不手改 YAML。
    /// </summary>
    public static class SceneBootstrapper
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("STS/建置主場景(Main.unity)")]
        public static void BuildMainScene()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabaseAsset>("Assets/Data/Resources/GameDatabase.asset");
            if (database == null)
            {
                Debug.LogError("找不到 Assets/Data/Resources/GameDatabase.asset——先跑選單 STS/重新匯入資料");
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Resources/NotoSansTC SDF.asset");
            if (font == null)
            {
                Debug.LogError("找不到 Assets/Fonts/Resources/NotoSansTC SDF.asset——先跑選單 STS/生成 TMP 繁中字型資產");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.07f, 0.1f);
            }

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();   // 忘記換這個模組,uGUI 拖曳會無聲失效

            var rootGo = new GameObject("GameRoot");
            var controller = rootGo.AddComponent<GameController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("database").objectReferenceValue = database;
            serialized.FindProperty("mainFont").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"主場景已建置:{ScenePath}");
        }
    }
}
