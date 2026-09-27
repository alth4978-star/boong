using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Taiyaki.EditorTools
{
    /// <summary>
    /// Resources/Art 아래 이미지를 게임에 맞게 자동 임포트한다.
    /// - 2의 제곱이 아닌 크기도 그대로 (배경 640×360 등)
    /// - 밉맵 끔, 무압축 (도트가 뭉개지지 않게)
    /// 필터 모드(Point/Bilinear)는 런타임에 Art.cs 에서 경로별로 지정한다.
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
        }
    }

    public static class TaiyakiMenu
    {
        internal const string MainScene = "Assets/Scenes/Main.unity";

        [MenuItem("Taiyaki/Open Main Scene")]
        static void OpenMain()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MainScene);
        }

        [MenuItem("Taiyaki/Reimport Art")]
        static void ReimportArt()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Taiyaki/Delete Save Data")]
        static void DeleteSave()
        {
            PlayerPrefs.DeleteKey("taiyaki_romance_save_v1");
            PlayerPrefs.Save();
            Debug.Log("[Taiyaki] 세이브 데이터를 삭제했습니다.");
        }
    }

    /// <summary>프로젝트를 처음 열었을 때 빈 씬이면 Main 씬을 자동으로 연다.</summary>
    [InitializeOnLoad]
    static class OpenMainOnFirstLoad
    {
        static OpenMainOnFirstLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty && System.IO.File.Exists(TaiyakiMenu.MainScene))
                    EditorSceneManager.OpenScene(TaiyakiMenu.MainScene);
            };
        }
    }
}
