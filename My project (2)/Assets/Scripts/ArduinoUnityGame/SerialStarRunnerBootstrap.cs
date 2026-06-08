using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArduinoUnityGame
{
    public static class SerialStarRunnerBootstrap
    {
        private const int CollectibleCount = 8;
#if UNITY_EDITOR
        private const string ImportedRunnerModelPath = "Assets/Models/X Bot.fbx";
        private const string ImportedRunnerControllerPath = "Assets/AnimatorController/XBot.controller";
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BuildGameOnSceneLoad()
        {
            SerialStarRunnerGame existingGame = Object.FindFirstObjectByType<SerialStarRunnerGame>();
            if (existingGame != null)
            {
                ConfigureExistingGame(existingGame);
                return;
            }

            BuildGame(false);
        }

        private static void ConfigureExistingGame(SerialStarRunnerGame game)
        {
            SerialInputReader serialInput = Object.FindFirstObjectByType<SerialInputReader>();
            SerialStarRunnerPlayer player = Object.FindFirstObjectByType<SerialStarRunnerPlayer>();
            SerialStarPickup[] pickups = Object.FindObjectsByType<SerialStarPickup>(FindObjectsSortMode.None);
            SerialStarHazard[] hazards = Object.FindObjectsByType<SerialStarHazard>(FindObjectsSortMode.None);
            SerialStarGoal goal = Object.FindFirstObjectByType<SerialStarGoal>();

            for (int i = 0; i < pickups.Length; i++)
            {
                pickups[i].Configure(game, 10);
            }

            for (int i = 0; i < hazards.Length; i++)
            {
                hazards[i].Configure(game);
            }

            if (goal != null)
            {
                goal.Configure(game);
            }

            if (player != null)
            {
                player.Configure(serialInput, game);
                EnsureImportedRunnerVisual(player);
                ConfigureExistingCamera(player.transform);
            }

            game.Configure(serialInput, player, pickups.Length, FindUiReferences(), pickups);
        }

        public static bool UpgradeScenePresentation()
        {
            SerialStarRunnerPlayer player = Object.FindFirstObjectByType<SerialStarRunnerPlayer>();
            return player != null && EnsureImportedRunnerVisual(player);
        }

        private static SerialStarRunnerGame.UiReferences FindUiReferences()
        {
            SerialStarRunnerGame.UiReferences ui = new SerialStarRunnerGame.UiReferences();
            ui.ScoreLabel = FindNamedComponent<Text>("Score Label");
            ui.TimerLabel = FindNamedComponent<Text>("Timer Label");
            ui.MessageLabel = FindNamedComponent<Text>("Message Label");
            ui.SerialLabel = FindNamedComponent<Text>("Serial Label");
            ui.HelpLabel = FindNamedComponent<Text>("Help Label");
            ui.PortField = FindNamedComponent<InputField>("COM Port Field");
            ui.ConnectButton = FindNamedComponent<Button>("Connect Button");
            ui.GoalRespawnToggle = FindNamedComponent<Toggle>("Goal Respawn Toggle");
            if (ui.GoalRespawnToggle == null)
            {
                ui.GoalRespawnToggle = CreateMissingGoalRespawnToggle();
            }

            return ui;
        }

        private static T FindNamedComponent<T>(string objectName) where T : Component
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].name == objectName)
                {
                    return objects[i].GetComponent<T>();
                }
            }

            return null;
        }

        public static bool BuildGame(bool rebuildExisting)
        {
            SerialStarRunnerGame existingGame = Object.FindFirstObjectByType<SerialStarRunnerGame>();
            if (existingGame != null)
            {
                if (!rebuildExisting)
                {
                    return false;
                }

                GameObject existingRoot = existingGame.gameObject;
                if (Application.isPlaying)
                {
                    Object.Destroy(existingRoot);
                }
                else
                {
                    Object.DestroyImmediate(existingRoot);
                }
            }

            GameObject root = new GameObject("Serial Star Runner");
            SerialInputReader serialInput = root.AddComponent<SerialInputReader>();
            SerialStarRunnerGame game = root.AddComponent<SerialStarRunnerGame>();

            Material trackMaterial = CreateMaterial("Track Material", new Color(0.17f, 0.18f, 0.19f));
            Material laneMaterial = CreateMaterial("Lane Material", new Color(0.07f, 0.1f, 0.12f));
            Material railMaterial = CreateMaterial("Rail Material", new Color(0.16f, 0.45f, 0.66f));
            Material playerMaterial = CreateMaterial("Player Material", new Color(0.12f, 0.64f, 0.9f));
            Material pickupMaterial = CreateMaterial("Energy Core Material", new Color(1f, 0.82f, 0.18f));
            Material hazardMaterial = CreateMaterial("Hazard Material", new Color(0.9f, 0.18f, 0.16f));
            Material goalMaterial = CreateMaterial("Goal Material", new Color(0.2f, 0.8f, 0.38f));

            Transform rootTransform = root.transform;
            BuildLighting(rootTransform);
            BuildTrack(rootTransform, trackMaterial, laneMaterial, railMaterial);

            SerialStarRunnerPlayer player = BuildPlayer(rootTransform, playerMaterial);
            SerialStarPickup[] pickups = BuildPickups(rootTransform, game, pickupMaterial);
            BuildHazards(rootTransform, game, hazardMaterial);
            BuildGoal(rootTransform, game, goalMaterial);
            BuildCamera(rootTransform, player.transform);

            SerialStarRunnerGame.UiReferences ui = BuildUi(rootTransform);
            player.Configure(serialInput, game);
            game.Configure(serialInput, player, pickups.Length, ui, pickups);
            return true;
        }

        private static void BuildLighting(Transform parent)
        {
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.5f);

            GameObject lightObject = new GameObject("Key Light");
            lightObject.transform.SetParent(parent, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        }

        private static void BuildTrack(Transform parent, Material trackMaterial, Material laneMaterial, Material railMaterial)
        {
            GameObject floor = CreateCube(parent, "Main Track", new Vector3(0f, -0.55f, 28f), new Vector3(12f, 1f, 68f), trackMaterial);
            floor.isStatic = true;

            for (int i = 0; i < 7; i++)
            {
                float z = -2f + i * 9.5f;
                CreateCube(parent, "Lane Stripe " + i, new Vector3(0f, 0.02f, z), new Vector3(0.18f, 0.05f, 5.5f), laneMaterial);
            }

            CreateCube(parent, "Left Rail", new Vector3(-6.35f, 0.4f, 28f), new Vector3(0.35f, 1.3f, 68f), railMaterial);
            CreateCube(parent, "Right Rail", new Vector3(6.35f, 0.4f, 28f), new Vector3(0.35f, 1.3f, 68f), railMaterial);

            for (int i = 0; i < 5; i++)
            {
                float z = 4f + i * 12f;
                CreateCube(parent, "Jump Pad " + i, new Vector3((i % 2 == 0 ? -2.5f : 2.5f), 0.06f, z), new Vector3(2.2f, 0.12f, 1.3f), railMaterial);
            }
        }

        private static SerialStarRunnerPlayer BuildPlayer(Transform parent, Material material)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Arduino Runner";
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(0f, 1f, -4f);
            player.transform.localScale = Vector3.one;
            ApplyMaterial(player, material);

            Rigidbody body = player.AddComponent<Rigidbody>();
            body.mass = 1f;
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = 0.2f;
            body.angularDamping = 0.05f;
#else
            body.drag = 0.2f;
            body.angularDrag = 0.05f;
#endif

            SerialStarRunnerPlayer runnerPlayer = player.AddComponent<SerialStarRunnerPlayer>();
            EnsureImportedRunnerVisual(runnerPlayer);
            return runnerPlayer;
        }

        private static bool EnsureImportedRunnerVisual(SerialStarRunnerPlayer player)
        {
            Animator existingAnimator = player.GetComponentInChildren<Animator>();
            if (existingAnimator != null)
            {
                ApplyImportedAnimatorController(existingAnimator);
                player.BindAnimator(existingAnimator);
                HideRootRenderer(player.gameObject);
                return false;
            }

            bool attachedModel;
            Animator importedAnimator = TryAttachImportedRunnerModel(player.gameObject, out attachedModel);
            if (attachedModel)
            {
                HideRootRenderer(player.gameObject);
                player.BindAnimator(importedAnimator);
            }

            return attachedModel;
        }

        private static void HideRootRenderer(GameObject player)
        {
            Renderer renderer = player.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private static Animator TryAttachImportedRunnerModel(GameObject player, out bool attachedModel)
        {
            attachedModel = false;

#if UNITY_EDITOR
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedRunnerModelPath);
            if (modelPrefab == null)
            {
                return null;
            }

            GameObject model = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (model == null)
            {
                model = Object.Instantiate(modelPrefab);
            }

            model.name = "X Bot";
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            attachedModel = true;

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                ApplyImportedAnimatorController(animator);
                animator.applyRootMotion = false;
            }

            return animator;
#else
            return null;
#endif
        }

        private static void ApplyImportedAnimatorController(Animator animator)
        {
#if UNITY_EDITOR
            if (animator == null)
            {
                return;
            }

            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ImportedRunnerControllerPath);
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
#endif
        }

        private static SerialStarPickup[] BuildPickups(Transform parent, SerialStarRunnerGame game, Material material)
        {
            Vector3[] positions =
            {
                new Vector3(-3.5f, 1.05f, 2f),
                new Vector3(2.7f, 1.05f, 7f),
                new Vector3(0.2f, 1.05f, 12f),
                new Vector3(-2.8f, 1.05f, 18f),
                new Vector3(3.6f, 1.05f, 23f),
                new Vector3(-0.8f, 1.05f, 31f),
                new Vector3(2.2f, 1.05f, 39f),
                new Vector3(-3.3f, 1.05f, 47f)
            };

            SerialStarPickup[] pickups = new SerialStarPickup[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                pickup.name = "Energy Core " + (i + 1);
                pickup.transform.SetParent(parent, false);
                pickup.transform.position = positions[i];
                pickup.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                ApplyMaterial(pickup, material);

                Collider collider = pickup.GetComponent<Collider>();
                collider.isTrigger = true;

                SerialStarPickup component = pickup.AddComponent<SerialStarPickup>();
                component.Configure(game, 10);
                pickups[i] = component;
            }

            return pickups;
        }

        private static void BuildHazards(Transform parent, SerialStarRunnerGame game, Material material)
        {
            Vector3[] positions =
            {
                new Vector3(0f, 0.65f, 5f),
                new Vector3(-3.1f, 0.65f, 15f),
                new Vector3(3.2f, 0.65f, 20f),
                new Vector3(0.4f, 0.65f, 28f),
                new Vector3(-3.9f, 0.65f, 36f),
                new Vector3(3.4f, 0.65f, 44f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject hazard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hazard.name = "Red Hazard " + (i + 1);
                hazard.transform.SetParent(parent, false);
                hazard.transform.position = positions[i];
                hazard.transform.localScale = new Vector3(1.2f, 0.75f, 1.2f);
                ApplyMaterial(hazard, material);

                Collider collider = hazard.GetComponent<Collider>();
                collider.isTrigger = true;

                SerialStarHazard component = hazard.AddComponent<SerialStarHazard>();
                component.Configure(game);
            }
        }

        private static void BuildGoal(Transform parent, SerialStarRunnerGame game, Material material)
        {
            GameObject goal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goal.name = "Finish Gate";
            goal.transform.SetParent(parent, false);
            goal.transform.position = new Vector3(0f, 1.75f, 56f);
            goal.transform.localScale = new Vector3(8.5f, 3.5f, 0.5f);
            ApplyMaterial(goal, material);

            Collider collider = goal.GetComponent<Collider>();
            collider.isTrigger = true;

            SerialStarGoal component = goal.AddComponent<SerialStarGoal>();
            component.Configure(game);
        }

        private static void BuildCamera(Transform parent, Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.transform.SetParent(parent, false);
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            else
            {
                camera.transform.SetParent(parent, true);
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f);
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.transform.position = target.position + new Vector3(0f, 7f, -10f);
            camera.transform.LookAt(target.position + new Vector3(0f, 1f, 4f));

            SimpleCameraFollow follow = camera.GetComponent<SimpleCameraFollow>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<SimpleCameraFollow>();
            }

            follow.Configure(target);
        }

        private static void ConfigureExistingCamera(Transform target)
        {
            if (target == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f);
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;

            SimpleCameraFollow follow = camera.GetComponent<SimpleCameraFollow>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<SimpleCameraFollow>();
            }

            follow.Configure(target);
        }

        private static SerialStarRunnerGame.UiReferences BuildUi(Transform parent)
        {
            EnsureEventSystem(parent);

            Font font = GetUiFont();
            GameObject canvasObject = new GameObject("Game HUD");
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();

            SerialStarRunnerGame.UiReferences ui = new SerialStarRunnerGame.UiReferences();
            ui.ScoreLabel = CreateText(canvasObject.transform, "Score Label", font, new Vector2(24f, -22f), TextAnchor.UpperLeft, 25, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(520f, 34f));
            ui.TimerLabel = CreateText(canvasObject.transform, "Timer Label", font, new Vector2(-24f, -22f), TextAnchor.UpperRight, 25, Color.white, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(420f, 34f));
            ui.MessageLabel = CreateText(canvasObject.transform, "Message Label", font, new Vector2(0f, -64f), TextAnchor.UpperCenter, 28, new Color(1f, 0.9f, 0.45f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 48f));
            ui.SerialLabel = CreateText(canvasObject.transform, "Serial Label", font, new Vector2(24f, 22f), TextAnchor.LowerLeft, 17, new Color(0.78f, 0.91f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(800f, 76f));
            ui.HelpLabel = CreateText(canvasObject.transform, "Help Label", font, new Vector2(0f, 16f), TextAnchor.LowerCenter, 16, new Color(0.82f, 0.84f, 0.86f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1080f, 34f));
            ui.PortField = CreateInputField(canvasObject.transform, font, new Vector2(-148f, 28f));
            ui.ConnectButton = CreateButton(canvasObject.transform, font, new Vector2(-24f, 28f));
            ui.GoalRespawnToggle = CreateGoalRespawnToggle(canvasObject.transform, font, new Vector2(-24f, 68f));

            return ui;
        }

        private static Toggle CreateMissingGoalRespawnToggle()
        {
            Canvas canvas = FindNamedComponent<Canvas>("Game HUD");
            if (canvas == null)
            {
                canvas = Object.FindFirstObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                return null;
            }

            return CreateGoalRespawnToggle(canvas.transform, GetUiFont(), new Vector2(-24f, 68f));
        }

        private static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(parent, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static Text CreateText(Transform parent, string name, Font font, Vector2 anchoredPosition, TextAnchor alignment, int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static InputField CreateInputField(Transform parent, Font font, Vector2 anchoredPosition)
        {
            GameObject fieldObject = new GameObject("COM Port Field");
            fieldObject.transform.SetParent(parent, false);

            RectTransform rect = fieldObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(96f, 32f);

            Image background = fieldObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.48f);

            InputField field = fieldObject.AddComponent<InputField>();
            field.textComponent = CreateChildInputText(fieldObject.transform, "Text", font, "", Color.white, TextAnchor.MiddleLeft);
            field.placeholder = CreateChildInputText(fieldObject.transform, "Placeholder", font, "COM3", new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleLeft);
            field.characterLimit = 8;
            field.text = "COM3";
            field.colors = BuildSelectableColors();
            return field;
        }

        private static Button CreateButton(Transform parent, Font font, Vector2 anchoredPosition)
        {
            GameObject buttonObject = new GameObject("Connect Button");
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(108f, 32f);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.45f, 0.68f, 0.85f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = BuildSelectableColors();

            Text label = CreateChildInputText(buttonObject.transform, "Label", font, "Connect", Color.white, TextAnchor.MiddleCenter);
            label.fontSize = 16;
            return button;
        }

        private static Toggle CreateGoalRespawnToggle(Transform parent, Font font, Vector2 anchoredPosition)
        {
            GameObject toggleObject = new GameObject("Goal Respawn Toggle");
            toggleObject.transform.SetParent(parent, false);

            RectTransform rect = toggleObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(196f, 28f);

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.colors = BuildSelectableColors();

            GameObject boxObject = new GameObject("Box");
            boxObject.transform.SetParent(toggleObject.transform, false);
            RectTransform boxRect = boxObject.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = new Vector2(0f, 0f);
            boxRect.sizeDelta = new Vector2(22f, 22f);

            Image box = boxObject.AddComponent<Image>();
            box.color = new Color(0f, 0f, 0f, 0.58f);

            GameObject checkObject = new GameObject("Checkmark");
            checkObject.transform.SetParent(boxObject.transform, false);
            RectTransform checkRect = checkObject.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = Vector2.zero;
            checkRect.sizeDelta = new Vector2(12f, 12f);

            Image check = checkObject.AddComponent<Image>();
            check.color = new Color(0.3f, 0.9f, 0.45f, 1f);

            Text label = CreateChildInputText(toggleObject.transform, "Label", font, "Loop finish", Color.white, TextAnchor.MiddleLeft);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.offsetMin = new Vector2(30f, 0f);
            labelRect.offsetMax = new Vector2(0f, 0f);
            label.fontSize = 15;

            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        private static Text CreateChildInputText(Transform parent, string name, Font font, string value, Color color, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 2f);
            rect.offsetMax = new Vector2(-8f, -2f);

            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = 15;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static ColorBlock BuildSelectableColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.95f, 1f);
            colors.pressedColor = new Color(0.65f, 0.8f, 0.95f);
            colors.selectedColor = new Color(0.85f, 0.95f, 1f);
            return colors;
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            ApplyMaterial(cube, material);
            return cube;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = name;
            material.color = color;
            return material;
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Font GetUiFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }
    }
}
