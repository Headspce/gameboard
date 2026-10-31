using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Assets/Scenes/GameBoard.unity from Tyler's sketch photo:
/// a thick pale slab (the game board) floating over a sea of clouds, with
/// five semi-transparent red oval pieces on top. The clouds are the photo's
/// own clouds (ink removed by tools/make_cloud_textures.py), shown as two
/// large single-texture planes — Wrap = Clamp, non-repeating — wired to the
/// ParallaxLayers runtime script.
///
/// Invoked from CI via: -executeMethod BuildGameBoardScene.Build
/// (GameBoardScreenshot.Capture calls this too, so the screenshot always
/// shows the scene exactly as this builder produces it.)
/// </summary>
public static class BuildGameBoardScene
{
    public const string ScenePath = "Assets/Scenes/GameBoard.unity";
    private const string FarTexturePath = "Assets/Textures/clouds.png";
    private const string NearTexturePath = "Assets/Textures/clouds_near.png";

    public static void Build()
    {
        ConfigureTextureImporter(FarTexturePath);
        ConfigureTextureImporter(NearTexturePath);

        var farTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FarTexturePath);
        var nearTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NearTexturePath);
        if (farTex == null || nearTex == null)
        {
            Debug.LogError("BuildGameBoardScene: cloud textures missing under Assets/Textures/.");
            EditorApplication.Exit(1);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Lighting / atmosphere: soft sky ambient + one warm sun. ---
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.52f, 0.59f, 0.70f);
        RenderSettings.fog = false;

        var sun = new GameObject("Sun Light");
        var sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1.0f, 0.95f, 0.86f);
        sunLight.intensity = 1.15f;
        sunLight.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        SceneManager.MoveGameObjectToScene(sun, scene);

        // --- Main camera: 3/4 view from front-above, like the sketch. ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.58f, 0.66f, 0.80f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 220f;
        camGo.transform.position = new Vector3(0f, 6.8f, -11.8f);
        camGo.transform.LookAt(new Vector3(0f, -0.2f, 0f));
        SceneManager.MoveGameObjectToScene(camGo, scene);

        // --- The board: one solid slab with a BoxCollider. Top at y = 0. ---
        var boardMat = new Material(Shader.Find("Standard"))
        {
            name = "BoardMat",
            color = new Color(0.93f, 0.94f, 0.96f)
        };
        boardMat.SetFloat("_Metallic", 0f);
        boardMat.SetFloat("_Glossiness", 0.15f);

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "GameBoard";
        board.transform.position = new Vector3(0f, -0.5f, 0f);
        board.transform.localScale = new Vector3(10f, 1f, 6.6f);
        board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;
        // CreatePrimitive(Cube) already includes a BoxCollider: solid.
        SceneManager.MoveGameObjectToScene(board, scene);

        // --- The pieces: five semi-transparent red oval discs. ---
        var pieceMat = new Material(Shader.Find("Standard")) { name = "PieceRed" };
        pieceMat.color = new Color(1f, 0.08f, 0.08f, 0.5f);
        pieceMat.SetFloat("_Mode", 3f); // Transparent
        pieceMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        pieceMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        pieceMat.SetInt("_ZWrite", 0);
        pieceMat.DisableKeyword("_ALPHATEST_ON");
        pieceMat.EnableKeyword("_ALPHABLEND_ON");
        pieceMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        pieceMat.renderQueue = 3000;

        // Back row of three, front row of two — the sketch's marker layout.
        var piecePositions = new[]
        {
            new Vector2(-2.6f, -1.70f),
            new Vector2(0.1f, -1.35f),
            new Vector2(2.9f, -1.80f),
            new Vector2(-3.1f, 0.75f),
            new Vector2(2.5f, 0.85f)
        };

        for (int i = 0; i < piecePositions.Length; i++)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piece.name = "Piece_" + (i + 1);
            var capsule = piece.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                Object.DestroyImmediate(capsule);
            }
            piece.AddComponent<BoxCollider>();

            // Scale from the real mesh bounds so the disc ends up exactly
            // 1.15 x 0.80 in plan and 0.18 thick, standing on the board top.
            var bounds = piece.GetComponent<MeshFilter>().sharedMesh.bounds;
            piece.transform.localScale = new Vector3(
                1.15f / bounds.size.x,
                0.18f / bounds.size.y,
                0.80f / bounds.size.z);
            piece.transform.position = new Vector3(piecePositions[i].x, 0.09f, piecePositions[i].y);
            piece.GetComponent<MeshRenderer>().sharedMaterial = pieceMat;
            SceneManager.MoveGameObjectToScene(piece, scene);
        }

        // --- Clouds background: two large planes, one texture each, no tiling. ---
        var farMat = new Material(Shader.Find("Unlit/Texture"))
        {
            name = "CloudsFarMat",
            mainTexture = farTex,
            color = new Color(0.94f, 0.97f, 1.0f)
        };
        var farPlane = MakeCloudPlane("CloudsFar", farMat, scene);
        farPlane.transform.position = new Vector3(0f, 3f, 36f);
        farPlane.transform.localScale = new Vector3(7.2f, 1f, 4.0f); // 72 x 40 world units

        var nearMat = new Material(Shader.Find("Unlit/Transparent"))
        {
            name = "CloudsNearMat",
            mainTexture = nearTex,
            color = new Color(1.0f, 0.99f, 0.97f)
        };
        var nearPlane = MakeCloudPlane("CloudsNear", nearMat, scene);
        nearPlane.transform.position = new Vector3(0f, -8.5f, 22f);
        nearPlane.transform.localScale = new Vector3(6.0f, 1f, 1.9f); // 60 x 19 world units

        // --- Parallax wiring. ---
        var parallaxGo = new GameObject("Parallax");
        var parallax = parallaxGo.AddComponent<ParallaxLayers>();
        parallax.layers = new[]
        {
            new ParallaxLayers.Layer
            {
                target = farPlane.transform,
                depthFactor = 0.08f,
                driftSpeed = 0.021f,
                driftAmount = 1.6f
            },
            new ParallaxLayers.Layer
            {
                target = nearPlane.transform,
                depthFactor = 0.22f,
                driftSpeed = 0.037f,
                driftAmount = 1.1f
            }
        };
        SceneManager.MoveGameObjectToScene(parallaxGo, scene);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("BuildGameBoardScene: saved " + ScenePath);
    }

    /// <summary>
    /// A 10x10 Plane primitive rotated to face the camera (-Z), collider removed.
    /// The texture spans the whole plane exactly once (UV 0..1, Clamp wrap).
    /// </summary>
    private static GameObject MakeCloudPlane(string name, Material mat, Scene scene)
    {
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = name;
        var collider = plane.GetComponent<MeshCollider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
        // Plane's normal is +Y; rotating -90 about X turns it to face -Z.
        plane.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        plane.GetComponent<MeshRenderer>().sharedMaterial = mat;
        SceneManager.MoveGameObjectToScene(plane, scene);
        return plane;
    }

    /// <summary>Non-repeating background: force Clamp wrap on the importer.</summary>
    private static void ConfigureTextureImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("BuildGameBoardScene: no importer for " + path);
            return;
        }
        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Debug.Log("BuildGameBoardScene: set Wrap=Clamp on " + path);
        }
    }
}
