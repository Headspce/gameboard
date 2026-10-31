using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// CI visual verification: (re)builds the GameBoard scene, then renders its
/// Main Camera to Screenshots/gameboard.png at 1920x1080.
/// Invoked from GitHub Actions via:
///   -executeMethod GameBoardScreenshot.Capture
/// Must run WITHOUT -nographics (needs a GL context; the workflow wraps it
/// in xvfb-run with Mesa software rendering).
/// </summary>
public static class GameBoardScreenshot
{
    public static void Capture()
    {
        BuildGameBoardScene.Build();

        var scene = SceneManager.GetActiveScene();
        Debug.Log("GameBoardScreenshot: active scene = " + scene.path);

        var camGo = GameObject.Find("Main Camera");
        if (camGo == null)
        {
            Debug.LogError("GameBoardScreenshot: Main Camera not found.");
            EditorApplication.Exit(1);
            return;
        }
        var cam = camGo.GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("GameBoardScreenshot: Main Camera has no Camera component.");
            EditorApplication.Exit(1);
            return;
        }

        const int w = 1920;
        const int h = 1080;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);

        Directory.CreateDirectory("Screenshots");
        var path = Path.Combine("Screenshots", "gameboard.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("GameBoardScreenshot: saved " + path);
    }
}
