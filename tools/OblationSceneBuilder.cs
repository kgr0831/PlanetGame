using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Run once with: unity command run_script --file tools/OblationSceneBuilder.cs --entry OblationSceneBuilder.Build
// Update an existing scene's role labels with --entry OblationSceneBuilder.ApplyFigmaUi.
// This file is intentionally outside Assets: it authors persistent Inspector objects, not runtime objects.
public static class OblationSceneBuilder
{
    static readonly string[] Names =
    {
        "VESPER", "NEMESIS", "KHEPRI", "TALOS", "MORROW", "EIDOLON",
        "ORISON", "CINDER", "HALCYON", "PERIHELION", "GOLGOTHA", "SERAPH"
    };

    static readonly Vector3[] Positions =
    {
        new Vector3(0, 0, 0), new Vector3(9, 0, 9), new Vector3(0, 0, -4.5f),
        new Vector3(-1, 0, -9), new Vector3(4.5f, 0, 0), new Vector3(9, 0, -1),
        new Vector3(0, 0, 4.5f), new Vector3(-1, 0, 9), new Vector3(-4.5f, 0, 0),
        new Vector3(-9, 0, 1), new Vector3(-5, 0, -5), new Vector3(5, 0, 5)
    };

    static readonly int[,] Edges =
    {
        {0,2},{0,4},{0,6},{0,8},{2,3},{2,10},{3,10},{4,5},{4,11},{5,1},
        {5,11},{6,7},{6,11},{7,11},{7,9},{8,9},{8,10},{9,10},{1,11},{2,4},{6,8}
    };

    static readonly Color Cyan = new Color(.12f, .78f, 1f, 1f);
    static readonly Color White = new Color(.82f, .9f, .96f, 1f);
    static readonly Color Muted = new Color(.62f, .72f, .8f, 1f);
    static readonly Color Panel = new Color(.025f, .045f, .075f, .94f);
    static readonly Color Red = new Color(.62f, .09f, .19f, 1f);

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before authoring the scene.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open Assets/Scenes/SampleScene.unity before building.");

        GameObject root = GameObject.Find("Root") ?? GameObject.Find("{name:Root}");
        if (root != null && root.transform.Find("GameUI") != null) return "Scene already contains the Inspector hierarchy.";
        if (root != null && root.transform.childCount != 0)
            throw new InvalidOperationException("Root has existing children; refusing to overwrite them.");
        if (root == null) root = new GameObject("Root");
        root.name = "Root";

        Transform gameUi = Child(root.transform, "GameUI");
        Transform hudUi = Child(root.transform, "HudUI");
        Transform util = Child(root.transform, "Util");
        Transform cameras = Child(util, "Cameras");
        Transform lighting = Child(util, "Lighting");
        Transform environment = Child(util, "Environment");
        Transform world = Child(util, "World");
        Transform runtime = Child(util, "Runtime");
        Transform audio = Child(util, "Audio");
        Transform effects = Child(runtime, "Effects");

        GameObject cameraObject = GameObject.Find("Main Camera");
        if (cameraObject == null || cameraObject.GetComponent<Camera>() == null)
            throw new InvalidOperationException("The existing Main Camera was not found.");
        cameraObject.transform.SetParent(cameras, true);
        cameraObject.tag = "MainCamera";
        GameObject lightObject = GameObject.Find("Directional Light");
        if (lightObject != null)
        {
            lightObject.transform.SetParent(lighting, true);
            Light light = lightObject.GetComponent<Light>();
            if (light != null) light.enabled = false;
        }
        GameObject volumeObject = GameObject.Find("Global Volume");
        if (volumeObject != null) volumeObject.transform.SetParent(environment, true);

        Material planetTemplate = MaterialAsset("PlanetTemplate", "Oblation/Procedural Planet");
        Material haloTemplate = MaterialAsset("HaloTemplate", "Oblation/Conquest Halo");
        Material lineMaterial = MaterialAsset("GalaxyLine", "Oblation/Galaxy Line");
        Material starfieldMaterial = MaterialAsset("Starfield", "Oblation/Starfield");
        BuildWorld(world, planetTemplate, haloTemplate, lineMaterial, starfieldMaterial,
            out OblationPlanetView[] planetViews, out LineRenderer[] routes,
            out GameObject selectionRing, out LineRenderer selectionLine);

        AudioSource music = Child(audio, "Music").gameObject.AddComponent<AudioSource>();
        AudioSource sfx = Child(audio, "Effects").gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        Transform eventSystem = Child(util, "UIEventSystem");
        eventSystem.gameObject.AddComponent<EventSystem>();
        eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        var references = new Dictionary<string, UnityEngine.Object>();
        BuildHud(hudUi, references, out Text[] planetLabels);
        BuildMenus(gameUi, references, out Button[] traitButtons, out Button[] unitButtons,
            out Button[] upgradeButtons, out Button[] exterminatusButtons);

        OblationGame game = Child(runtime, "GameManager").gameObject.AddComponent<OblationGame>();
        references.Add("gameCamera", cameraObject.GetComponent<Camera>());
        references.Add("musicSource", music);
        references.Add("sfxSource", sfx);
        references.Add("effectsRoot", effects);
        references.Add("haloTemplate", haloTemplate);
        references.Add("lineMaterial", lineMaterial);
        references.Add("selectionRing", selectionRing);
        references.Add("selectionLine", selectionLine);
        OblationCatalogSO catalog = AssetDatabase.LoadAssetAtPath<OblationCatalogSO>("Assets/Data/OblationCatalog.asset");
        if (catalog != null) references.Add("catalog", catalog);
        SerializedObject serialized = new SerializedObject(game);
        foreach (var entry in references)
        {
            SerializedProperty property = serialized.FindProperty(entry.Key);
            if (property == null) throw new InvalidOperationException("Missing serialized field: " + entry.Key);
            property.objectReferenceValue = entry.Value;
        }
        SetArray(serialized, "planetViews", planetViews);
        SetArray(serialized, "routeViews", routes);
        SetArray(serialized, "planetLabels", planetLabels);
        SetArray(serialized, "traitButtons", traitButtons);
        SetArray(serialized, "unitButtons", unitButtons);
        SetArray(serialized, "upgradeButtons", upgradeButtons);
        SetArray(serialized, "exterminatusButtons", exterminatusButtons);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Built Root/GameUI/HudUI/Util with 12 Inspector planets, 21 routes, and serialized uGUI.";
    }

    public static string ApplyFigmaUi()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing the scene.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open Assets/Scenes/SampleScene.unity first.");
        GameObject root = GameObject.Find("Root");
        if (root == null) throw new InvalidOperationException("Root was not found.");

        Text resources = SceneText(root.transform, "HudUI/HudCanvas/HudScreen/TopBar/Resources");
        resources.fontSize = 15;
        SceneText(root.transform, "HudUI/HudCanvas/HudScreen/TopBar/Controls").text =
            "WASD 이동  |  휠 확대  |  F 연구  |  Enter 지도 전환";
        SceneText(root.transform, "HudUI/HudCanvas/HudScreen/TopBar/DoctrineButton/Label").text = "연구";
        string cards = "GameUI/GameCanvas/AssignmentModal/Card/";
        SceneText(root.transform, cards + "ExtractorButton/Label").text = "유닛 특화\n\n생산 시간 단축";
        SceneText(root.transform, cards + "ForgeButton/Label").text = "자원 특화\n\n광물 수급 강화";
        SceneText(root.transform, cards + "PsionicButton/Label").text = "연구 특화\n\n신경 에너지 강화";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Updated Inspector UI labels for the Figma planet roles.";
    }

    public static string ApplyPlanetPreview()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing the scene.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open Assets/Scenes/SampleScene.unity first.");
        Transform root = GameObject.Find("Root")?.transform;
        Transform details = root?.Find("HudUI/HudCanvas/HudScreen/PlanetDetails");
        OblationGame game = root?.Find("Util/Runtime/GameManager")?.GetComponent<OblationGame>();
        if (details == null || game == null) throw new InvalidOperationException("Planet details are missing.");
        if (details.Find("InteriorPreview") != null) return "Planet interior preview already exists.";
        RectTransform frame = Fixed(details, "InteriorPreview", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(248, -14), new Vector2(104, 80));
        Paint(frame, new Color(.1f, .42f, .55f, 1f), false);
        RectTransform viewport = Fixed(frame, "Image", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(2, -2), new Vector2(100, 76));
        RawImage image = viewport.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.color = Color.white;
        SceneText(root, "HudUI/HudCanvas/HudScreen/PlanetDetails/PlanetName").rectTransform.sizeDelta = new Vector2(220, 32);
        SceneText(root, "HudUI/HudCanvas/HudScreen/PlanetDetails/PlanetMeta").rectTransform.sizeDelta = new Vector2(220, 25);
        SerializedObject serialized = new SerializedObject(game);
        serialized.FindProperty("planetInteriorImage").objectReferenceValue = image;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Added serialized planet interior preview to the status panel.";
    }

    public static string ShowPlanetPreviewForReview()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        OblationGame game = GameObject.Find("Root")?.transform.Find("Util/Runtime/GameManager")?.GetComponent<OblationGame>();
        if (game == null) throw new InvalidOperationException("GameManager was not found.");
        SerializedObject serialized = new SerializedObject(game);
        Button start = serialized.FindProperty("startButton")?.objectReferenceValue as Button;
        if (start == null) throw new InvalidOperationException("Start button was not found.");
        start.onClick.Invoke();
        return "Opened the playable HUD for visual review.";
    }

    public static string ApplyFeedbackMap()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing the scene.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open Assets/Scenes/SampleScene.unity first.");
        Transform root = GameObject.Find("Root")?.transform;
        Transform world = root?.Find("Util/World");
        Transform planetRoot = world?.Find("Planets");
        Transform orbitRoot = world?.Find("Orbits");
        Transform hud = root?.Find("HudUI/HudCanvas/HudScreen");
        OblationGame game = root?.Find("Util/Runtime/GameManager")?.GetComponent<OblationGame>();
        if (planetRoot == null || orbitRoot == null || hud == null || game == null)
            throw new InvalidOperationException("The saved Inspector hierarchy is incomplete.");

        SerializedObject serialized = new SerializedObject(game);
        SerializedProperty routeViews = serialized.FindProperty("routeViews");
        if (routeViews == null || routeViews.arraySize != Edges.GetLength(0))
            throw new InvalidOperationException("Route references do not match the feedback map.");
        for (int i = 0; i < Names.Length; i++)
            if (planetRoot.Find(Names[i]) == null) throw new InvalidOperationException("Missing planet: " + Names[i]);
        for (int i = 0; i < routeViews.arraySize; i++)
            if (!(routeViews.GetArrayElementAtIndex(i).objectReferenceValue is LineRenderer))
                throw new InvalidOperationException("Missing route: " + i);

        Transform star = world.Find("Background/Hungry Star");
        if (star != null) Undo.DestroyObjectImmediate(star.gameObject);
        for (int i = 1; i <= 5; i++)
        {
            Transform ring = orbitRoot.Find("Orbit " + i);
            if (ring != null) Undo.DestroyObjectImmediate(ring.gameObject);
        }
        for (int i = 0; i < Names.Length; i++)
        {
            Transform planet = planetRoot.Find(Names[i]);
            Undo.RecordObject(planet, "Spread planets in four directions");
            planet.localPosition = Positions[i];
            EditorUtility.SetDirty(planet);
            Transform orbit = orbitRoot.Find(Names[i] + " Orbit");
            if (orbit == null) continue;
            LineRenderer line = orbit.GetComponent<LineRenderer>();
            if (line == null) continue;
            Undo.RecordObject(line, "Move planet orbit");
            float radius = planet.Find("Body").localScale.x * 1.55f;
            for (int point = 0; point < line.positionCount; point++)
            {
                float angle = point / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(point, Positions[i] + new Vector3(Mathf.Cos(angle) * radius, -.2f, Mathf.Sin(angle) * radius));
            }
            EditorUtility.SetDirty(line);
        }
        for (int i = 0; i < routeViews.arraySize; i++)
        {
            LineRenderer route = (LineRenderer)routeViews.GetArrayElementAtIndex(i).objectReferenceValue;
            int a = Edges[i, 0], b = Edges[i, 1];
            Undo.RecordObject(route, "Reconnect four-direction routes");
            route.gameObject.name = Names[a] + " - " + Names[b];
            route.SetPosition(0, Positions[a] + Vector3.down * .18f);
            route.SetPosition(1, Positions[b] + Vector3.down * .18f);
            EditorUtility.SetDirty(route);
        }

        Transform focus = hud.Find("PlanetFocus");
        if (focus == null)
        {
            var references = new Dictionary<string, UnityEngine.Object>();
            BuildFocusUi(hud, references);
            foreach (var entry in references)
                serialized.FindProperty(entry.Key).objectReferenceValue = entry.Value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Removed the central star, spread 12 planets across four directions, reconnected 21 routes, and added the focus UI.";
    }

    public static string ApplyNotionUi()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing the scene.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open Assets/Scenes/SampleScene.unity first.");
        Transform root = GameObject.Find("Root")?.transform;
        Transform canvas = root?.Find("GameUI/GameCanvas");
        RectTransform details = root?.Find("HudUI/HudCanvas/HudScreen/PlanetDetails")?.GetComponent<RectTransform>();
        OblationGame game = root?.Find("Util/Runtime/GameManager")?.GetComponent<OblationGame>();
        if (canvas == null || details == null || game == null)
            throw new InvalidOperationException("The saved Inspector UI hierarchy is incomplete.");

        var references = new Dictionary<string, UnityEngine.Object>();
        details.sizeDelta = new Vector2(370, 480);
        Transform open = details.Find("OperationsButton");
        if (open == null)
            references.Add("operationsOpenButton", Button(details, "OperationsButton", "행성 운영",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -424), new Vector2(330, 40)));
        else references.Add("operationsOpenButton", open.GetComponent<Button>());

        if (canvas.Find("OperationsModal") != null)
            throw new InvalidOperationException("OperationsModal already exists; refusing to duplicate it.");
        BuildOperationsUi(canvas, references, out Button[] traitButtons, out Button[] unitButtons,
            out Button[] upgradeButtons, out Button[] exterminatusButtons);
        SerializedObject serialized = new SerializedObject(game);
        foreach (var entry in references)
        {
            SerializedProperty property = serialized.FindProperty(entry.Key);
            if (property == null) throw new InvalidOperationException("Missing serialized field: " + entry.Key);
            property.objectReferenceValue = entry.Value;
        }
        SetArray(serialized, "traitButtons", traitButtons);
        SetArray(serialized, "unitButtons", unitButtons);
        SetArray(serialized, "upgradeButtons", upgradeButtons);
        SetArray(serialized, "exterminatusButtons", exterminatusButtons);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SceneText(root, "GameUI/GameCanvas/DoctrineModal/Card/Heading").text = "연구";
        SceneText(root, "GameUI/GameCanvas/DoctrineModal/Card/Description").text = "한 번에 하나의 연구만 진행할 수 있습니다.";
        SceneText(root, "HudUI/HudCanvas/HudScreen/PlanetDetails/SurpriseButton/Label").text = "역병";
        SceneText(root, "HudUI/HudCanvas/HudScreen/DefenseAlert/DefendButton/Label").text = "방어 광물15";
        SceneText(root, "GameUI/GameCanvas/AssignmentModal/Card/ExtractorButton/Label").text = "유닛 특화\n\n생산 시간 단축";
        SceneText(root, "GameUI/GameCanvas/AssignmentModal/Card/ForgeButton/Label").text = "자원 특화\n\n광물 수급 강화";
        SceneText(root, "GameUI/GameCanvas/AssignmentModal/Card/PsionicButton/Label").text = "연구 특화\n\n신경 에너지 강화";
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Added the serialized planet operations UI and updated research/planet labels.";
    }

    static Text SceneText(Transform root, string path)
    {
        Transform target = root.Find(path);
        if (target == null || target.GetComponent<Text>() == null)
            throw new InvalidOperationException("UI text not found: " + path);
        return target.GetComponent<Text>();
    }

    static void SetArray<T>(SerializedObject serialized, string name, T[] values) where T : UnityEngine.Object
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException("Missing serialized array: " + name);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    static Material MaterialAsset(string name, string shaderName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Oblation")) AssetDatabase.CreateFolder("Assets/Materials", "Oblation");
        string path = "Assets/Materials/Oblation/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("Shader unavailable: " + shaderName);
        material = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void BuildWorld(Transform world, Material planetTemplate, Material haloTemplate,
        Material lineMaterial, Material starfieldMaterial, out OblationPlanetView[] views,
        out LineRenderer[] routes, out GameObject selectionRing, out LineRenderer selectionLine)
    {
        Transform backgroundRoot = Child(world, "Background");
        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        background.name = "Starfield";
        background.transform.SetParent(backgroundRoot, false);
        background.transform.localScale = Vector3.one * 160;
        UnityEngine.Object.DestroyImmediate(background.GetComponent<Collider>());
        background.GetComponent<Renderer>().sharedMaterial = starfieldMaterial;

        Transform orbits = Child(world, "Orbits");

        Transform planetRoot = Child(world, "Planets");
        views = new OblationPlanetView[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            float scale = i == 0 || i == 1 ? 1.2f : .72f + (i % 4) * .12f;
            float seed = 13.7f + i * 9.31f;
            Transform anchor = Child(planetRoot, Names[i]);
            anchor.localPosition = Positions[i];
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(anchor, false);
            body.transform.localScale = Vector3.one * scale;
            body.AddComponent<PlanetMarker>().index = i;
            Material bodyMaterial = MaterialAsset(Names[i], "Oblation/Procedural Planet");
            bodyMaterial.SetColor("_BaseColor", Color.HSVToRGB((seed * .071f) % 1f, .52f, .46f));
            bodyMaterial.SetColor("_AccentColor", Color.HSVToRGB((seed * .071f + .13f) % 1f, .65f, .92f));
            bodyMaterial.SetFloat("_Seed", seed);
            bodyMaterial.SetFloat("_Emission", .45f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            EditorUtility.SetDirty(bodyMaterial);

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Conquest Halo";
            halo.transform.SetParent(body.transform, false);
            halo.transform.localScale = Vector3.one * 1.18f;
            UnityEngine.Object.DestroyImmediate(halo.GetComponent<Collider>());
            Material haloMaterial = MaterialAsset(Names[i] + "Halo", "Oblation/Conquest Halo");
            haloMaterial.SetFloat("_Seed", seed);
            halo.GetComponent<Renderer>().sharedMaterial = haloMaterial;
            EditorUtility.SetDirty(haloMaterial);

            OblationPlanetView view = anchor.gameObject.AddComponent<OblationPlanetView>();
            view.index = i;
            view.bodyRenderer = body.GetComponent<Renderer>();
            view.haloRenderer = halo.GetComponent<Renderer>();
            views[i] = view;
            if (i % 4 == 0)
                Circle(orbits, Names[i] + " Orbit", Positions[i], scale * 1.55f,
                    new Color(.65f, .72f, 1f, .34f), .025f, 64, lineMaterial);
        }

        Transform routeRoot = Child(world, "Routes");
        routes = new LineRenderer[Edges.GetLength(0)];
        for (int i = 0; i < routes.Length; i++)
        {
            int a = Edges[i, 0], b = Edges[i, 1];
            LineRenderer route = Line(routeRoot, Names[a] + " - " + Names[b],
                new Color(.18f, .4f, .65f, .36f), .035f, false, lineMaterial);
            route.positionCount = 2;
            route.SetPosition(0, Positions[a] + Vector3.down * .18f);
            route.SetPosition(1, Positions[b] + Vector3.down * .18f);
            routes[i] = route;
        }

        Transform selection = Child(world, "Selection");
        selectionRing = Child(selection, "Selection Ring").gameObject;
        selectionLine = selectionRing.AddComponent<LineRenderer>();
        ConfigureLine(selectionLine, Cyan, .055f, true, lineMaterial);
        selectionLine.positionCount = 65;
        selectionRing.SetActive(false);
    }

    static LineRenderer Line(Transform parent, string name, Color color, float width, bool loop, Material material)
    {
        LineRenderer line = Child(parent, name).gameObject.AddComponent<LineRenderer>();
        ConfigureLine(line, color, width, loop, material);
        return line;
    }

    static void ConfigureLine(LineRenderer line, Color color, float width, bool loop, Material material)
    {
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.loop = loop;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.numCapVertices = line.numCornerVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    static void Circle(Transform parent, string name, Vector3 center, float radius,
        Color color, float width, int segments, Material material)
    {
        LineRenderer line = Line(parent, name, color, width, true, material);
        line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, -.2f, Mathf.Sin(angle) * radius));
        }
    }

    static Canvas MakeCanvas(Transform parent, string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        return canvas;
    }

    static RectTransform Full(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static RectTransform Fixed(Transform parent, string name, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static Image Paint(RectTransform rect, Color color, bool blocksRaycasts = true)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = blocksRaycasts;
        return image;
    }

    static Text Label(Transform parent, string name, string value, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        RectTransform rect = Fixed(parent, name, anchor, pivot, position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Button Button(Transform parent, string name, string caption, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, bool danger = false, int fontSize = 18)
    {
        RectTransform rect = Fixed(parent, name, anchor, pivot, position, size);
        Image image = Paint(rect, danger ? Red : new Color(.06f, .33f, .47f, .98f));
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        RectTransform labelRect = Full(rect, "Label");
        Text text = labelRect.gameObject.AddComponent<Text>();
        text.text = caption;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return button;
    }

    static Slider SliderControl(Transform parent, string name, Vector2 position, Vector2 size, float min, float max)
    {
        RectTransform root = Fixed(parent, name, new Vector2(0, 1), new Vector2(0, 1), position, size);
        Slider slider = root.gameObject.AddComponent<Slider>();
        Paint(Full(root, "Track"), new Color(.12f, .18f, .25f, 1f));
        RectTransform fill = Full(root, "Fill");
        Image fillImage = Paint(fill, Cyan, false);
        RectTransform handle = Fixed(root, "Handle", new Vector2(0, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, size.y + 10));
        Image handleImage = Paint(handle, Color.white);
        slider.fillRect = fillImage.rectTransform;
        slider.handleRect = handleImage.rectTransform;
        slider.targetGraphic = handleImage;
        slider.minValue = min;
        slider.maxValue = max;
        return slider;
    }

    static RectTransform Modal(Transform canvas, string name, Vector2 size, out RectTransform card)
    {
        RectTransform overlay = Full(canvas, name);
        Paint(overlay, new Color(0, 0, 0, .72f));
        card = Fixed(overlay, "Card", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size);
        Paint(card, Panel);
        overlay.gameObject.SetActive(false);
        return overlay;
    }

    static void Bind(Dictionary<string, UnityEngine.Object> references, string field, UnityEngine.Object value)
    {
        references.Add(field, value);
    }

    static void BuildHud(Transform parent, Dictionary<string, UnityEngine.Object> references, out Text[] planetLabels)
    {
        Canvas canvas = MakeCanvas(parent, "HudCanvas", 0);
        Bind(references, "hudUiScaler", canvas.GetComponent<CanvasScaler>());
        RectTransform hud = Full(canvas.transform, "HudScreen");
        Bind(references, "hudScreen", hud.gameObject);
        hud.gameObject.SetActive(false);

        RectTransform header = Full(hud, "TopBar");
        header.anchorMin = new Vector2(0, 1);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(.5f, 1);
        header.sizeDelta = new Vector2(0, 76);
        header.anchoredPosition = Vector2.zero;
        Paint(header, Panel);
        Label(header, "Brand", "OBLATION // 은하 지도", new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -13), new Vector2(275, 32), 22, Cyan);
        Bind(references, "resourcesText", Label(header, "Resources", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(315, -16), new Vector2(880, 34), 15, White));
        Bind(references, "ownershipText", Label(header, "Ownership", "", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-345, -16), new Vector2(320, 28), 17, White));
        Label(header, "Controls", "WASD 이동  |  휠 확대  |  F 연구  |  Enter 지도 전환", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-345, -44), new Vector2(330, 22), 12, Muted);
        Bind(references, "doctrineOpenButton", Button(header, "DoctrineButton", "연구", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-135, -10), new Vector2(64, 46), false, 15));
        Bind(references, "pauseOpenButton", Button(header, "PauseButton", "일시정지", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -10), new Vector2(104, 46), false, 15));

        RectTransform chronicle = Fixed(hud, "Chronicle", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -92), new Vector2(312, 230));
        Paint(chronicle, Panel);
        Label(chronicle, "Heading", "연대기", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -16), new Vector2(270, 30), 22, Cyan);
        Bind(references, "eventLogText", Label(chronicle, "EventLog", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -53), new Vector2(276, 172), 14, Muted));

        RectTransform status = Full(hud, "StatusBar");
        status.anchorMin = Vector2.zero;
        status.anchorMax = new Vector2(1, 0);
        status.pivot = new Vector2(.5f, 0);
        status.sizeDelta = new Vector2(-36, 52);
        status.anchoredPosition = new Vector2(0, 18);
        Paint(status, Panel);
        Bind(references, "statusText", Label(status, "Status", "", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(1500, 30), 16, White, TextAnchor.MiddleLeft));

        BuildFocusUi(hud, references);

        RectTransform details = Fixed(hud, "PlanetDetails", Vector2.one, Vector2.one, new Vector2(-18, -92), new Vector2(370, 480));
        Paint(details, Panel);
        Bind(references, "planetPanel", details.gameObject);
        Bind(references, "planetNameText", Label(details, "PlanetName", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -18), new Vector2(330, 32), 24, Cyan));
        Bind(references, "planetMetaText", Label(details, "PlanetMeta", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -70), new Vector2(330, 25), 16, White));
        Bind(references, "planetStatsText", Label(details, "PlanetStats", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -104), new Vector2(330, 60), 16, White));
        Bind(references, "planetTraitText", Label(details, "PlanetTrait", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -175), new Vector2(330, 54), 14, Muted));
        Bind(references, "planetProductionText", Label(details, "Production", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -246), new Vector2(330, 48), 14, Muted));
        Bind(references, "sourceButton", Button(details, "SourceButton", "출발지 지정", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -317), new Vector2(155, 42)));
        Bind(references, "zoomButton", Button(details, "ZoomButton", "행성 집중", new Vector2(0, 1), new Vector2(0, 1), new Vector2(195, -317), new Vector2(155, 42)));
        Bind(references, "surpriseButton", Button(details, "SurpriseButton", "역병", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -317), new Vector2(102, 42)));
        Bind(references, "assaultButton", Button(details, "AssaultButton", "강습", new Vector2(0, 1), new Vector2(0, 1), new Vector2(132, -317), new Vector2(102, 42)));
        Bind(references, "orbitalButton", Button(details, "OrbitalButton", "궤도 폭격", new Vector2(0, 1), new Vector2(0, 1), new Vector2(244, -317), new Vector2(106, 42), true, 15));
        Bind(references, "planetHintText", Label(details, "Hint", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -365), new Vector2(330, 50), 12, Muted));
        Bind(references, "operationsOpenButton", Button(details, "OperationsButton", "행성 운영", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -424), new Vector2(330, 40)));
        details.gameObject.SetActive(false);

        RectTransform defense = Fixed(hud, "DefenseAlert", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -92), new Vector2(460, 78));
        Paint(defense, Panel);
        Bind(references, "defensePanel", defense.gameObject);
        Bind(references, "defenseText", Label(defense, "DefenseText", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -19), new Vector2(270, 35), 18, Red));
        Bind(references, "defendButton", Button(defense, "DefendButton", "방어 광물15", new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, -18), new Vector2(140, 42), true, 15));
        defense.gameObject.SetActive(false);

        RectTransform labels = Full(hud, "PlanetLabels");
        planetLabels = new Text[Names.Length];
        for (int i = 0; i < Names.Length; i++)
            planetLabels[i] = Label(labels, Names[i] + " Label", Names[i], new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 24), 13, Cyan, TextAnchor.MiddleCenter);
    }

    static void BuildFocusUi(Transform hud, Dictionary<string, UnityEngine.Object> references)
    {
        RectTransform focus = Fixed(hud, "PlanetFocus", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(0, -265), new Vector2(560, 106));
        Paint(focus, new Color(.015f, .035f, .065f, .92f), false);
        CanvasGroup group = focus.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        group.interactable = false;
        group.blocksRaycasts = false;
        Bind(references, "focusUi", group);
        RectTransform accent = Fixed(focus, "Accent", new Vector2(0, 1), new Vector2(0, 1),
            Vector2.zero, new Vector2(560, 4));
        Paint(accent, Cyan, false);
        Bind(references, "focusHeadingText", Label(focus, "Heading", "행성 관측", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(22, -16), new Vector2(516, 34), 23, Cyan));
        Bind(references, "focusInfoText", Label(focus, "Info", "", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(22, -59), new Vector2(516, 32), 15, White));
        focus.gameObject.SetActive(false);
    }

    static void BuildOperationsUi(Transform canvas, Dictionary<string, UnityEngine.Object> references,
        out Button[] traitButtons, out Button[] unitButtons, out Button[] upgradeButtons,
        out Button[] exterminatusButtons)
    {
        RectTransform card;
        RectTransform overlay = Modal(canvas, "OperationsModal", new Vector2(1180, 820), out card);
        Bind(references, "operationsModal", overlay.gameObject);
        Label(card, "Heading", "행성 운영", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(30, -22), new Vector2(1100, 46), 34, Cyan);
        Bind(references, "operationsSummaryText", Label(card, "Summary", "", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(30, -74), new Vector2(1100, 58), 17, White));
        Bind(references, "antennaButton", Button(card, "AntennaButton", "안테나 설치  광물 120",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -140), new Vector2(250, 46), false, 15));
        traitButtons = new Button[3];
        string[] traitNames = { "자원 특화", "유닛 특화", "연구 특화" };
        for (int i = 0; i < traitButtons.Length; i++)
            traitButtons[i] = Button(card, "Trait" + i, traitNames[i], new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(300 + i * 280, -140), new Vector2(260, 46), false, 15);

        Label(card, "UnitsHeading", "유닛 생산", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(30, -204), new Vector2(1100, 30), 20, Cyan);
        unitButtons = new Button[8];
        for (int i = 0; i < unitButtons.Length; i++)
            unitButtons[i] = Button(card, "Unit" + i, "유닛 " + (i + 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(30 + i % 4 * 280, -239 - i / 4 * 72), new Vector2(260, 64), false, 14);

        Label(card, "UpgradeHeading", "유닛 강화", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(30, -390), new Vector2(1100, 30), 20, Cyan);
        upgradeButtons = new Button[6];
        for (int i = 0; i < upgradeButtons.Length; i++)
            upgradeButtons[i] = Button(card, "Upgrade" + i, "강화 " + (i + 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(30 + i % 3 * 373, -430 - i / 3 * 58), new Vector2(350, 48), false, 14);

        Label(card, "ExterminatusHeading", "익스터미나투스", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(30, -555), new Vector2(1100, 30), 20, Red);
        exterminatusButtons = new Button[3];
        string[] endings = { "역병 절멸", "공성 절멸", "궤도 절멸" };
        for (int i = 0; i < exterminatusButtons.Length; i++)
            exterminatusButtons[i] = Button(card, "Exterminatus" + i, endings[i], new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(30 + i * 373, -594), new Vector2(350, 64), true, 16);
        Bind(references, "operationsCloseButton", Button(card, "CloseButton", "은하로 돌아가기",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(440, -737), new Vector2(300, 48)));
    }

    static void BuildMenus(Transform parent, Dictionary<string, UnityEngine.Object> references,
        out Button[] traitButtons, out Button[] unitButtons, out Button[] upgradeButtons,
        out Button[] exterminatusButtons)
    {
        Canvas canvas = MakeCanvas(parent, "GameCanvas", 10);
        Bind(references, "gameUiScaler", canvas.GetComponent<CanvasScaler>());
        RectTransform title = Full(canvas.transform, "TitleScreen");
        Paint(title, new Color(0, 0, 0, .48f));
        Bind(references, "titleScreen", title.gameObject);
        Label(title, "Title", "OBLATION", new Vector2(0, 1), new Vector2(0, 1), new Vector2(172, -175), new Vector2(900, 90), 62, White);
        Label(title, "Subtitle", "굶주린 신에게 바칠 은하", new Vector2(0, 1), new Vector2(0, 1), new Vector2(178, -260), new Vector2(780, 42), 26, Cyan);
        Label(title, "Description", "싱글플레이 실시간 행성 정복\n외교는 없다. 오직 공물뿐.", new Vector2(0, 1), new Vector2(0, 1), new Vector2(178, -318), new Vector2(680, 90), 20, White);
        Bind(references, "startButton", Button(title, "StartButton", "공물 징수 시작", new Vector2(0, 1), new Vector2(0, 1), new Vector2(178, -620), new Vector2(310, 54)));
        Bind(references, "titleOptionsButton", Button(title, "OptionsButton", "설정", new Vector2(0, 1), new Vector2(0, 1), new Vector2(178, -688), new Vector2(310, 54)));
        Bind(references, "quitButton", Button(title, "QuitButton", "종료", new Vector2(0, 1), new Vector2(0, 1), new Vector2(178, -756), new Vector2(310, 54), true));
        Label(title, "Prototype", "피그마 설계 / 셰이더 기반 시제품", Vector2.one, Vector2.one, new Vector2(-30, -40), new Vector2(390, 25), 13, Muted, TextAnchor.MiddleRight);

        RectTransform card;
        RectTransform doctrine = Modal(canvas.transform, "DoctrineModal", new Vector2(1040, 570), out card);
        Bind(references, "doctrineModal", doctrine.gameObject);
        Label(card, "Heading", "연구", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -25), new Vector2(900, 60), 43, Cyan);
        Label(card, "Description", "한 번에 하나의 연구만 진행할 수 있습니다.", new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -92), new Vector2(940, 32), 18, White);
        DoctrineCard(card, "Viral", "역병 성전례", "역병 함대가 방어를 우회합니다.\n기습 피해 +38.", "정신력 45", 34, out Button viral);
        DoctrineCard(card, "Foundry", "전쟁 주조소", "질량 가속기와 공성 함선을 건조합니다.\n강습 피해 +34.", "광물 55 / 산업력 45", 370, out Button foundry);
        DoctrineCard(card, "Orbital", "궤도 칙령", "행성을 파괴하는 궤도 폭격을 해금합니다.", "정신력 55 / 산업력 70", 706, out Button orbital);
        Bind(references, "viralButton", viral);
        Bind(references, "foundryButton", foundry);
        Bind(references, "orbitalTechButton", orbital);
        Bind(references, "doctrineCloseButton", Button(card, "CloseButton", "은하로 돌아가기", new Vector2(0, 1), new Vector2(0, 1), new Vector2(390, -500), new Vector2(260, 46)));

        BuildOperationsUi(canvas.transform, references, out traitButtons, out unitButtons,
            out upgradeButtons, out exterminatusButtons);

        RectTransform assignment = Modal(canvas.transform, "AssignmentModal", new Vector2(840, 420), out card);
        Bind(references, "assignmentModal", assignment.gameObject);
        Bind(references, "assignmentTitleText", Label(card, "Heading", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(780, 46), 26, Cyan));
        Bind(references, "assignmentDescriptionText", Label(card, "Description", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -85), new Vector2(780, 64), 18, White));
        Bind(references, "extractorButton", Button(card, "ExtractorButton", "유닛 특화\n\n생산 시간 단축", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -164), new Vector2(232, 150)));
        Bind(references, "forgeButton", Button(card, "ForgeButton", "자원 특화\n\n광물 수급 강화", new Vector2(0, 1), new Vector2(0, 1), new Vector2(304, -164), new Vector2(232, 150)));
        Bind(references, "psionicButton", Button(card, "PsionicButton", "연구 특화\n\n신경 에너지 강화", new Vector2(0, 1), new Vector2(0, 1), new Vector2(572, -164), new Vector2(232, 150)));

        RectTransform pause = Modal(canvas.transform, "PauseModal", new Vector2(360, 370), out card);
        Bind(references, "pauseModal", pause.gameObject);
        Label(card, "Heading", "작전 일시정지", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(304, 42), 26, Cyan);
        Bind(references, "continueButton", Button(card, "ContinueButton", "계속하기", new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, -92), new Vector2(276, 46)));
        Bind(references, "pauseOptionsButton", Button(card, "OptionsButton", "설정", new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, -152), new Vector2(276, 46)));
        Bind(references, "pauseRestartButton", Button(card, "RestartButton", "작전 다시 시작", new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, -212), new Vector2(276, 46), true));
        Bind(references, "pauseMenuButton", Button(card, "MenuButton", "주 메뉴", new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, -272), new Vector2(276, 46), true));

        RectTransform ending = Modal(canvas.transform, "EndingModal", new Vector2(780, 420), out card);
        Bind(references, "endingModal", ending.gameObject);
        Bind(references, "endingTitleText", Label(card, "Heading", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -42), new Vector2(708, 70), 44, Cyan));
        Bind(references, "endingDescriptionText", Label(card, "Description", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -132), new Vector2(700, 90), 18, White));
        Bind(references, "endingRestartButton", Button(card, "RestartButton", "다시 플레이", new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -250), new Vector2(240, 56)));
        Bind(references, "endingMenuButton", Button(card, "MenuButton", "주 메뉴", new Vector2(0, 1), new Vector2(0, 1), new Vector2(420, -250), new Vector2(240, 56)));

        RectTransform options = Modal(canvas.transform, "OptionsModal", new Vector2(600, 500), out card);
        Bind(references, "optionsModal", options.gameObject);
        Label(card, "Heading", "설정", new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -28), new Vector2(530, 40), 29, Cyan);
        Label(card, "MusicLabel", "배경음", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -110), new Vector2(180, 26), 18, White);
        Label(card, "SfxLabel", "효과음", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -180), new Vector2(180, 26), 18, White);
        Label(card, "ScaleLabel", "화면 배율", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -250), new Vector2(180, 26), 18, White);
        Label(card, "CameraLabel", "카메라 속도", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -320), new Vector2(180, 26), 18, White);
        Bind(references, "musicSlider", SliderControl(card, "MusicSlider", new Vector2(230, -112), new Vector2(310, 20), 0, 1));
        Bind(references, "sfxSlider", SliderControl(card, "SfxSlider", new Vector2(230, -182), new Vector2(310, 20), 0, 1));
        Bind(references, "uiScaleSlider", SliderControl(card, "ScaleSlider", new Vector2(230, -252), new Vector2(310, 20), .8f, 1.25f));
        Bind(references, "cameraSlider", SliderControl(card, "CameraSlider", new Vector2(230, -322), new Vector2(310, 20), .55f, 1.8f));
        Bind(references, "optionsSaveButton", Button(card, "SaveButton", "저장하고 닫기", new Vector2(0, 1), new Vector2(0, 1), new Vector2(155, -410), new Vector2(290, 48)));

        RectTransform fade = Full(canvas.transform, "FadeOverlay");
        Bind(references, "fadeOverlay", Paint(fade, new Color(.005f, .008f, .018f, 0), false));
        fade.gameObject.SetActive(false);
    }

    static void DoctrineCard(Transform parent, string name, string title, string description,
        string cost, float x, out Button purchase)
    {
        RectTransform card = Fixed(parent, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -145), new Vector2(300, 300));
        Paint(card, new Color(.035f, .075f, .11f, 1f));
        Label(card, "Title", title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -20), new Vector2(264, 40), 22, Cyan);
        Label(card, "Description", description, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -76), new Vector2(264, 90), 16, White);
        Label(card, "Cost", cost, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -176), new Vector2(264, 28), 14, Muted);
        purchase = Button(card, "ResearchButton", "연구", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -232), new Vector2(264, 44));
    }
}
