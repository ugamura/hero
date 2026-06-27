using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using Hero.ARLayer;
using Hero.Detection;
using Hero.Effects;
using Hero.Game;
using Hero.Input;
using Hero.Network;
using Hero.UI;

namespace Hero.Editor
{
    [InitializeOnLoad]
    public static class HeroProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/ARMainScene.unity";
        private static readonly Color Navy = new Color(0.025f, 0.06f, 0.10f, 0.92f);
        private static readonly Color Panel = new Color(0.055f, 0.10f, 0.15f, 0.94f);
        private static readonly Color Green = new Color(0f, 0.90f, 0.47f, 1f);
        private static readonly Color Muted = new Color(0.64f, 0.70f, 0.74f, 1f);
        private static Font font;

        static HeroProjectBuilder()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            ConfigureProject();
            if (!File.Exists(ScenePath)) GenerateScene(false);
        }

        [MenuItem("Hero/Rebuild Main Scene")]
        public static void RebuildScene()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Hero Scene", "Replace ARMainScene with a fresh generated scene?", "Rebuild", "Cancel")) return;
            GenerateScene(true);
        }

        [MenuItem("Hero/Apply Mobile Build Settings")]
        public static void ApplyBuildSettings()
        {
            ConfigureProject();
            Debug.Log("[Hero] Mobile build settings applied.");
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "Team Hero";
            PlayerSettings.productName = "AR Word Chain";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.teamhero.arwordchain");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.teamhero.arwordchain");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.cameraUsageDescription = "The camera is used to find real-world objects for the AR word-chain game.";
            TryAssignLoader(BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader", "com.unity.xr.arcore");
            TryAssignLoader(BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader", "com.unity.xr.arkit");
        }

        private static void TryAssignLoader(BuildTargetGroup group, string loader, string package)
        {
            try
            {
                Type type = Type.GetType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore, Unity.XR.Management.Editor");
                MethodInfo method = type == null ? null : type.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(BuildTargetGroup), typeof(string), typeof(string) }, null);
                if (method != null) method.Invoke(null, new object[] { group, loader, package });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Hero] XR loader will need manual confirmation: " + exception.Message);
            }
        }

        private static void GenerateScene(bool overwrite)
        {
            if (!overwrite && File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Scenes");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "ARMainScene";

            CreateEventSystem();

            GameObject arSessionObject = new GameObject("AR Session");
            ARSession arSession = arSessionObject.AddComponent<ARSession>();

            GameObject originObject = new GameObject("XR Origin");
            XROrigin origin = originObject.AddComponent<XROrigin>();
            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform, false);
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.045f, 0.07f);
            camera.nearClipPlane = 0.1f;
            cameraObject.AddComponent<AudioListener>();
            ARCameraManager cameraManager = cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            FrameCaptureService frameCapture = cameraObject.AddComponent<FrameCaptureService>();
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = cameraOffset;

            GameObject managersObject = new GameObject("AR Managers");
            ARPlaneManager planeManager = managersObject.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            ARRaycastManager raycastManager = managersObject.AddComponent<ARRaycastManager>();
            ARAnchorManager anchorManager = managersObject.AddComponent<ARAnchorManager>();
            AnchorPlacementService anchorService = managersObject.AddComponent<AnchorPlacementService>();

            GameObject systems = new GameObject("Game Systems");
            DetectionClient client = systems.AddComponent<DetectionClient>();
            WordChainGameManager game = systems.AddComponent<WordChainGameManager>();
            CandidateManager candidates = systems.AddComponent<CandidateManager>();
            InputDispatcher input = systems.AddComponent<InputDispatcher>();
            CommitController commit = systems.AddComponent<CommitController>();
            MockDetectionFeeder mock = systems.AddComponent<MockDetectionFeeder>();
            global::Hero.RuntimeModeController mode = systems.AddComponent<global::Hero.RuntimeModeController>();

            client.endpoint = "http://127.0.0.1:8000/detect";
            candidates.client = client;
            candidates.game = game;
            mock.targetClient = client;
            frameCapture.client = client;
            commit.input = input;
            commit.candidates = candidates;
            commit.game = game;
            mode.mockFeeder = mock;
            mode.frameCapture = frameCapture;
            mode.arSession = arSession;
            mode.planeManager = planeManager;
            mode.raycastManager = raycastManager;
            mode.anchorManager = anchorManager;
            anchorService.arCamera = camera;
            anchorService.anchorManager = anchorManager;
            anchorService.game = game;

            Canvas canvas = CreateCanvas();
            RectTransform bboxLayer = CreateRect("Candidate Layer", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BBoxRenderer bboxRenderer = bboxLayer.gameObject.AddComponent<BBoxRenderer>();
            bboxRenderer.candidates = candidates;

            RectTransform safeArea = CreateRect("Safe Area", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            Image topBar = CreateImage("Top Bar", safeArea, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -75f), new Vector2(0f, 150f), Navy);
            Text score = CreateText("Score", topBar.rectTransform, new Vector2(0f, 0.5f), new Vector2(165f, 0f), new Vector2(280f, 70f), 30, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            Text next = CreateText("Next", topBar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-165f, 0f), new Vector2(250f, 70f), 34, TextAnchor.MiddleCenter, Green, FontStyle.Bold);
            Text timer = CreateText("Timer", topBar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(100f, 0f), new Vector2(170f, 70f), 36, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Text lives = CreateText("Lives", topBar.rectTransform, new Vector2(1f, 0.5f), new Vector2(-245f, 0f), new Vector2(210f, 70f), 25, TextAnchor.MiddleRight, Color.white, FontStyle.Normal);
            Text connection = CreateText("Connection", topBar.rectTransform, new Vector2(1f, 0f), new Vector2(-210f, 28f), new Vector2(210f, 42f), 20, TextAnchor.MiddleRight, Green, FontStyle.Bold);
            connection.text = "DEMO";
            Button serverButton = CreateButton("Server Button", topBar.rectTransform, new Vector2(1f, 0f), new Vector2(-75f, 30f), new Vector2(120f, 48f), "SERVER", 18, new Color(0.11f, 0.19f, 0.25f));

            Image bottomBar = CreateImage("Bottom Bar", safeArea, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 82.5f), new Vector2(0f, 165f), Navy);
            Text history = CreateText("Word History", bottomBar.rectTransform, new Vector2(0f, 0.5f), new Vector2(410f, 22f), new Vector2(760f, 90f), 24, TextAnchor.MiddleLeft, Color.white, FontStyle.Normal);
            history.horizontalOverflow = HorizontalWrapMode.Wrap;
            Button resetButton = CreateButton("Reset Button", bottomBar.rectTransform, new Vector2(1f, 0.5f), new Vector2(-105f, 22f), new Vector2(170f, 72f), "RESET", 24, Green);
            Text instruction = CreateText("Instructions", safeArea, new Vector2(0.5f, 0f), new Vector2(0f, 205f), new Vector2(980f, 48f), 21, TextAnchor.MiddleCenter, Muted, FontStyle.Normal);

            Image hintRoot = CreateImage("Hint Panel", safeArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 190f), Panel);
            Text hintText = CreateText("Hint Text", hintRoot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 130f), 32, TextAnchor.MiddleCenter, Green, FontStyle.Bold);
            HintPanel hintPanel = safeArea.gameObject.AddComponent<HintPanel>();
            hintPanel.panelRoot = hintRoot.gameObject;
            hintPanel.hintText = hintText;
            hintRoot.gameObject.SetActive(false);

            Image detailRoot = CreateImage("Candidate Detail", safeArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), new Vector2(600f, 175f), Panel);
            Text detailWord = CreateText("Detail Word", detailRoot.rectTransform, new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(540f, 65f), 38, TextAnchor.MiddleCenter, Green, FontStyle.Bold);
            Text detailConfidence = CreateText("Detail Confidence", detailRoot.rectTransform, new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(540f, 45f), 23, TextAnchor.MiddleCenter, Muted, FontStyle.Normal);
            CandidateDetailPanel detailPanel = safeArea.gameObject.AddComponent<CandidateDetailPanel>();
            detailPanel.panelRoot = detailRoot.gameObject;
            detailPanel.wordText = detailWord;
            detailPanel.confidenceText = detailConfidence;
            detailRoot.gameObject.SetActive(false);

            Image gameOverRoot = CreateImage("Game Over", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.03f, 0.05f, 0.92f));
            Text gameOverText = CreateText("Game Over Text", gameOverRoot.rectTransform, new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(900f, 250f), 62, TextAnchor.MiddleCenter, Green, FontStyle.Bold);
            Button playAgain = CreateButton("Play Again", gameOverRoot.rectTransform, new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(360f, 100f), "PLAY AGAIN", 30, Green);
            gameOverRoot.gameObject.SetActive(false);

            HUDController hud = safeArea.gameObject.AddComponent<HUDController>();
            hud.game = game;
            hud.scoreText = score;
            hud.requiredLetterText = next;
            hud.livesText = lives;
            hud.timerText = timer;
            hud.usedWordsText = history;
            hud.statusText = instruction;
            hud.resetButton = resetButton;
            hud.playAgainButton = playAgain;
            hud.gameOverPanel = gameOverRoot.gameObject;
            hud.gameOverText = gameOverText;

            ConnectionStatusController statusController = safeArea.gameObject.AddComponent<ConnectionStatusController>();
            statusController.client = client;
            statusController.statusText = connection;

            Image settingsRoot = CreateImage("Server Settings", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.03f, 0.05f, 0.96f));
            Image settingsCard = CreateImage("Settings Card", settingsRoot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 500f), Panel);
            Text settingsTitle = CreateText("Settings Title", settingsCard.rectTransform, new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(820f, 70f), 38, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            settingsTitle.text = "DETECTION SERVER";
            InputField endpointInput = CreateInputField("Endpoint", settingsCard.rectTransform, new Vector2(0.5f, 0.57f), Vector2.zero, new Vector2(790f, 85f));
            Text validation = CreateText("Validation", settingsCard.rectTransform, new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(760f, 50f), 20, TextAnchor.MiddleCenter, Muted, FontStyle.Normal);
            Button apply = CreateButton("Apply", settingsCard.rectTransform, new Vector2(0.38f, 0.18f), Vector2.zero, new Vector2(300f, 80f), "SAVE", 26, Green);
            Button close = CreateButton("Close", settingsCard.rectTransform, new Vector2(0.68f, 0.18f), Vector2.zero, new Vector2(300f, 80f), "CANCEL", 26, new Color(0.18f, 0.23f, 0.27f));
            ServerEndpointController endpointController = safeArea.gameObject.AddComponent<ServerEndpointController>();
            endpointController.client = client;
            endpointController.panelRoot = settingsRoot.gameObject;
            endpointController.endpointInput = endpointInput;
            endpointController.openButton = serverButton;
            endpointController.applyButton = apply;
            endpointController.closeButton = close;
            endpointController.validationText = validation;
            settingsRoot.gameObject.SetActive(false);

            Image tutorialRoot = CreateImage("Tutorial", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.03f, 0.05f, 0.97f));
            Image tutorialCard = CreateImage("Tutorial Card", tutorialRoot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 850f), Panel);
            Text tutorialTitle = CreateText("Tutorial Title", tutorialCard.rectTransform, new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(800f, 110f), 56, TextAnchor.MiddleCenter, Green, FontStyle.Bold);
            Text tutorialBody = CreateText("Tutorial Body", tutorialCard.rectTransform, new Vector2(0.5f, 0.51f), Vector2.zero, new Vector2(780f, 260f), 31, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            Text tutorialPage = CreateText("Tutorial Page", tutorialCard.rectTransform, new Vector2(0.5f, 0.29f), Vector2.zero, new Vector2(300f, 55f), 22, TextAnchor.MiddleCenter, Muted, FontStyle.Normal);
            Button tutorialNext = CreateButton("Tutorial Next", tutorialCard.rectTransform, new Vector2(0.5f, 0.14f), Vector2.zero, new Vector2(400f, 95f), "NEXT", 30, Green);
            Button tutorialClose = CreateButton("Tutorial Close", tutorialCard.rectTransform, new Vector2(0.92f, 0.92f), Vector2.zero, new Vector2(65f, 65f), "×", 34, new Color(0.18f, 0.23f, 0.27f));
            TutorialController tutorial = safeArea.gameObject.AddComponent<TutorialController>();
            tutorial.tutorialRoot = tutorialRoot.gameObject;
            tutorial.titleText = tutorialTitle;
            tutorial.bodyText = tutorialBody;
            tutorial.pageText = tutorialPage;
            tutorial.nextButton = tutorialNext;
            tutorial.closeButton = tutorialClose;

            commit.hintPanel = hintPanel;
            commit.detailPanel = detailPanel;

            GameObject effects = new GameObject("Effects");
            FXSpawner fx = effects.AddComponent<FXSpawner>();
            SoundManager sound = effects.AddComponent<SoundManager>();
            HapticFeedback haptic = effects.AddComponent<HapticFeedback>();
            fx.game = game;
            fx.fallbackCanvas = canvas;
            sound.game = game;
            haptic.game = game;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = systems;
            Debug.Log("[Hero] ARMainScene generated. Press Play for the Editor demo.");
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystem = new GameObject("Event System");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color, FontStyle style)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = obj.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.text = name;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string label, int fontSize, Color color)
        {
            Image background = CreateImage(name, parent, anchor, anchor, position, size, color);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
            button.colors = colors;
            Text text = CreateText("Label", background.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            text.text = label;
            return button;
        }

        private static InputField CreateInputField(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Image background = CreateImage(name, parent, anchor, anchor, position, size, new Color(0.025f, 0.055f, 0.08f, 1f));
            InputField input = background.gameObject.AddComponent<InputField>();
            Text text = CreateText("Text", background.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x - 45f, size.y - 20f), 25, TextAnchor.MiddleLeft, Color.white, FontStyle.Normal);
            text.raycastTarget = true;
            Text placeholder = CreateText("Placeholder", background.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x - 45f, size.y - 20f), 25, TextAnchor.MiddleLeft, Muted, FontStyle.Italic);
            placeholder.text = "http://192.168.x.x:8000/detect";
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }
    }
}
