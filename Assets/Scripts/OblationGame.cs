using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed partial class OblationGame : MonoBehaviour
{
    public enum Allegiance { Neutral, Player, Enemy }
    public enum WorldType { Unassigned, Unit, Manufacturing, Energy }
    enum ScreenState { Title, Playing, Victory, Defeat }
    enum AttackMode { Surprise, Assault, Orbital }

    sealed class Planet
    {
        public PlanetDefinitionSO definition;
        public string name;
        public string trait;
        public Vector3 position;
        public readonly List<int> links = new List<int>();
        public Allegiance owner;
        public WorldType type;
        public float population;
        public float maxPopulation;
        public float playerClaim;
        public float enemyClaim;
        public float defense;
        public float scale;
        public float seed;
        public bool antennaActive;
        public float antennaHealth;
        public float antennaRemaining;
        public bool scouted;
        public bool destroyed;
        public readonly int[] traitLevels = new int[3];
        public string queuedUnitId;
        public float unitRemaining;
        public float unitTotalTime;
        public float hitFlash;
        public string enemyUnitId;
        public float enemyProductionClock;
        public GameObject body;
        public Renderer bodyRenderer;
        public Renderer haloRenderer;
    }

    sealed class Attack
    {
        public Allegiance attacker;
        public AttackMode mode;
        public int from;
        public int to;
        public float elapsed;
        public float duration;
        public float startPopulation;
        public float startClaim;
        public float damage;
        public bool defended;
        public int stage;
        public float startAntennaHealth;
        public float committedUnits;
        public LineRenderer beam;
        public OblationAttackVisual visual;
    }

    static readonly Color PlayerColor = new Color(0.12f, 0.78f, 1f);
    static readonly Color EnemyColor = new Color(1f, 0.42f, 0.12f);
    static readonly Color NeutralColor = new Color(0.48f, 0.55f, 0.65f);
    static readonly string[] Names = OblationGalaxyLayout.Names;
    static readonly Vector3[] Positions = OblationGalaxyLayout.Positions;
    static readonly int[,] Edges = OblationGalaxyLayout.Edges;

    readonly List<Planet> planets = new List<Planet>();
    readonly List<Attack> attacks = new List<Attack>();
    readonly List<string> eventLog = new List<string>();
    readonly List<LineRenderer> routes = new List<LineRenderer>();

    [Header("Inspector scene references")]
    [SerializeField] Camera gameCamera;
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource sfxSource;
    [SerializeField] Transform effectsRoot;
    [SerializeField] OblationPlanetView[] planetViews;
    [SerializeField] LineRenderer[] routeViews;
    [SerializeField] Material haloTemplate;
    [SerializeField] Material lineMaterial;
    [SerializeField] OblationCatalogSO catalog;
    [SerializeField] GameObject selectionRing;
    [SerializeField] LineRenderer selectionLine;

    AudioClip clickClip;
    AudioClip attackClip;
    AudioClip captureClip;

    ScreenState state = ScreenState.Title;
    int sourcePlanet = -1;
    int targetPlanet = -1;
    int selectedPlanet = -1;
    int pendingAssignment = -1;
    int defendPlanet = -1;
    int lastClicked = -1;
    float lastClickTime;
    float biomass = 200;
    float minerals = 160;
    float neural = 100;
    float offering;
    float combatUnits;
    float aiStrength = 1;
    float economyClock;
    float aiClock;
    float eventClock;
    float campaignElapsed;
    float fade;
    float mapDistance = OblationGalaxyLayout.StartingDistance;
    float focusDistance = 18;
    float musicVolume = .55f;
    float sfxVolume = .7f;
    public float UiSfxVolume => sfxVolume;
    public int AppearanceSeed { get; private set; }
    float uiScaleSetting = 1;
    float cameraSensitivity = 1;
    bool optionsOpen;
    bool paused;
    bool factoryOpen;
    bool zoomed;
    bool techViral;
    bool techFoundry;
    bool techOrbital;
    Vector3 cameraFocus;
    Vector3 cameraLookFocus;
    Vector3 cameraFocusVelocity;
    public static bool TopologyIsConnected()
    {
        var seen = new bool[Positions.Length];
        var queue = new Queue<int>();
        seen[0] = true;
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            for (int i = 0; i < Edges.GetLength(0); i++)
            {
                int next = Edges[i, 0] == node ? Edges[i, 1] : Edges[i, 1] == node ? Edges[i, 0] : -1;
                if (next >= 0 && !seen[next]) { seen[next] = true; queue.Enqueue(next); }
            }
        }
        for (int i = 0; i < seen.Length; i++) if (!seen[i]) return false;
        return true;
    }

    public static bool StartingRoutesSpanFourDirections()
    {
        bool north = false, south = false, east = false, west = false;
        for (int i = 0; i < Edges.GetLength(0); i++)
        {
            int next = Edges[i, 0] == 0 ? Edges[i, 1] : Edges[i, 1] == 0 ? Edges[i, 0] : -1;
            if (next < 0) continue;
            Vector3 delta = Positions[next] - Positions[0];
            north |= delta.z > 0 && Mathf.Abs(delta.z) >= Mathf.Abs(delta.x);
            south |= delta.z < 0 && Mathf.Abs(delta.z) >= Mathf.Abs(delta.x);
            east |= delta.x > 0 && Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);
            west |= delta.x < 0 && Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);
        }
        return north && south && east && west;
    }

    void Awake()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 120;
        musicVolume = PlayerPrefs.GetFloat("Oblation.Music", .55f);
        sfxVolume = PlayerPrefs.GetFloat("Oblation.Sfx", .7f);
        uiScaleSetting = PlayerPrefs.GetFloat("Oblation.UiScale", 1f);
        cameraSensitivity = PlayerPrefs.GetFloat("Oblation.Sensitivity", 1f);
        if (!BindGalaxy()) { enabled = false; return; }
        SetupCamera();
        SetupAudio();
        BindUiEvents();
        ResetCampaign(false);
    }

    void SetupCamera()
    {
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(.002f, .004f, .012f);
        gameCamera.fieldOfView = 50;
        gameCamera.nearClipPlane = .05f;
        gameCamera.farClipPlane = 1200;
    }

    bool BindGalaxy()
    {
        if (gameCamera == null || musicSource == null || sfxSource == null || effectsRoot == null ||
            haloTemplate == null || lineMaterial == null || catalog == null || selectionRing == null || selectionLine == null ||
            planetViews == null || planetViews.Length != Positions.Length ||
            routeViews == null || routeViews.Length != Edges.GetLength(0) ||
            catalog.planets == null || catalog.planets.Length != Positions.Length ||
            catalog.resources == null || catalog.resources.Length != 4 ||
            catalog.traits == null || catalog.traits.Length != 3 ||
            catalog.units == null || catalog.units.Length != 8 ||
            catalog.unitUpgrades == null || catalog.unitUpgrades.Length != 24 ||
            catalog.research == null || catalog.research.Length != 9 ||
            catalog.exterminatus == null || catalog.exterminatus.Length != 3 ||
            catalog.antenna == null || catalog.conquestRule == null)
        {
            Debug.LogError("OBLATION: Inspector 참조가 누락되었거나 행성/보급로 개수가 맞지 않습니다.", this);
            return false;
        }

        for (int i = 0; i < planetViews.Length; i++)
        {
            OblationPlanetView view = planetViews[i];
            if (view == null || view.index != i || view.bodyRenderer == null || view.haloRenderer == null)
            {
                Debug.LogError("OBLATION: 행성 Inspector 참조가 잘못되었습니다: " + i, this);
                return false;
            }
            PlanetDefinitionSO definition = catalog.planets[i];
            if (definition == null || definition.id != Names[i])
            {
                Debug.LogError("OBLATION: 행성 데이터가 씬 순서와 맞지 않습니다: " + i, this);
                return false;
            }

            planets.Add(new Planet
            {
                definition = definition,
                name = definition.displayName,
                trait = definition.traitDescription,
                position = view.transform.position,
                scale = view.bodyRenderer.transform.lossyScale.x,
                maxPopulation = definition.maxPopulation,
                defense = definition.defense,
                body = view.bodyRenderer.gameObject,
                bodyRenderer = view.bodyRenderer,
                haloRenderer = view.haloRenderer
            });
        }

        for (int i = 0; i < Edges.GetLength(0); i++)
        {
            int a = Edges[i, 0], b = Edges[i, 1];
            planets[a].links.Add(b);
            planets[b].links.Add(a);
            if (routeViews[i] == null)
            {
                Debug.LogError("OBLATION: 보급로 참조가 누락되었습니다: " + i, this);
                return false;
            }
            routes.Add(routeViews[i]);
        }

        selectionRing.SetActive(false);
        return true;
    }

    void ConfigureLine(LineRenderer line, Color color, float width, bool loop)
    {
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.loop = loop;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 3;
        line.numCornerVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    void SetupAudio()
    {
        musicSource.loop = true;
        musicSource.clip = CreateAmbientClip();
        musicSource.volume = musicVolume * .34f;
        musicSource.Play();
        clickClip = CreateTone("화면 조작음", 1120, .055f, .12f);
        attackClip = CreateTone("공격음", 110, .34f, .26f);
        captureClip = CreateTone("정복음", 260, .65f, .3f);
    }

    static AudioClip CreateAmbientClip()
    {
        const int rate = 22050, seconds = 12;
        float[] samples = new float[rate * seconds];
        var random = new System.Random(9031);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float breath = .5f + .5f * Mathf.Sin(t * .19f);
            float drone = Mathf.Sin(t * 2 * Mathf.PI * 43f) * .06f + Mathf.Sin(t * 2 * Mathf.PI * 64.5f) * .035f;
            float dust = ((float)random.NextDouble() * 2 - 1) * .006f * breath;
            samples[i] = drone * (.55f + breath * .35f) + dust;
        }
        AudioClip clip = AudioClip.Create("절차적 공허 합창", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip CreateTone(string name, float frequency, float duration, float volume)
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(rate * duration);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Pow(1f - i / (float)count, 2f);
            samples[i] = (Mathf.Sin(t * frequency * Mathf.PI * 2) + Mathf.Sin(t * frequency * .503f * Mathf.PI * 2) * .35f) * envelope * volume;
        }
        AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void ResetCampaign(bool begin)
    {
        FinishCampaignMetrics("abandoned");
        AppearanceSeed=Guid.NewGuid().GetHashCode()&int.MaxValue;
        var visualRandom=new System.Random(AppearanceSeed);
        foreach(var view in planetViews)view.ApplyVisualSeed(visualRandom.Next());
        foreach (Attack attack in attacks) if (attack.beam != null) Destroy(attack.beam.gameObject);
        attacks.Clear();
        eventLog.Clear();
        for (int i = 0; i < planets.Count; i++)
        {
            Planet p = planets[i];
            p.owner = Allegiance.Neutral;
            p.type = WorldType.Unassigned;
            p.population = p.maxPopulation;
            p.playerClaim = 0;
            p.enemyClaim = 0;
        }
        planets[0].owner = Allegiance.Player;
        planets[0].playerClaim = 1;
        planets[0].type = WorldType.Unit;
        planets[1].owner = Allegiance.Enemy;
        planets[1].enemyClaim = 1;
        planets[1].type = WorldType.Manufacturing;
        ResetActionEffects();
        ResetSystems();
        ResetPresentation(begin);
        if (begin) StartCampaignMetrics();
        if (planets[0].definition.captureTechnology != null)
            capturedTechnologies.Add(planets[0].definition.captureTechnology.id);
        aiStrength = 1; economyClock = 0; aiClock = 0; eventClock = 0; campaignElapsed = 0;
        sourcePlanet = 0; selectedPlanet = -1; targetPlanet = -1; pendingAssignment = -1; defendPlanet = -1;
        techViral = techFoundry = techOrbital = false;
        paused = factoryOpen = operationsOpen = optionsOpen = false;
        zoomed = false;
        mapDistance = OblationGalaxyLayout.StartingDistance; focusDistance = 18; mapDragging = false;
        cameraFocus = Vector3.zero;
        cameraLookFocus = Vector3.zero;
        cameraFocusVelocity = Vector3.zero;
        AddLog("은하가 약속되었습니다. 공물을 거두십시오.");
        if (begin) AddLog("시작 병력 6기 배치 완료. 행성을 선택해 작전을 시작하세요.");
        state = begin ? ScreenState.Playing : ScreenState.Title;
        fade = 0;
        ResetStory(begin);
        UpdateAllVisuals();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        RotateWorld(dt);
        UpdateStory(dt);
        UpdateCamera(dt);
        if (musicSource != null) musicSource.volume = musicVolume * .34f;
        if (state != ScreenState.Playing) return;
        fade = Mathf.Max(0, fade - dt * .8f);
        if (storyConsumedInput) return;
        HandleShortcuts();
        HandleAccessibilityShortcuts();
        if (paused || optionsOpen || pendingAssignment >= 0 || state != ScreenState.Playing) return;
        HandleWorldInput();
        if (TutorialHoldsSimulation) return;
        dt *= gameSpeed;
        campaignElapsed += dt;
        TickSystems(dt);
        economyClock += dt;
        aiClock += dt;
        eventClock += dt;
        while (economyClock >= 1f) { economyClock -= 1f; EconomyTick(); }
        if (aiClock >= Mathf.Max(15f, 24f - aiStrength * .5f)) { aiClock = 0; EnemyTurn(); }
        if (eventClock >= 32f) { eventClock = 0; TriggerEvent(); }
        UpdateAttacks(dt);
    }

    void RotateWorld(float dt)
    {
        if (ReducedMotion) return;
        for (int i = 0; i < planets.Count; i++)
            if (planets[i].body != null) planets[i].body.transform.Rotate(Vector3.up, dt * (7 + i % 5), Space.World);
    }

    void UpdateCamera(float dt)
    {
        if (gameCamera == null) return;
        if (UpdateStoryCamera(dt)) return;
        HandleCameraZoom();
        HandleMapNavigation(dt);
        if (UpdateCinematicCamera(dt)) return;
        Vector3 desiredFocus = zoomed && selectedPlanet >= 0 ? planets[selectedPlanet].position : cameraFocus;
        if (state == ScreenState.Title)
        {
            desiredFocus = Vector3.zero;
            mapDistance = 25;
        }
        cameraLookFocus = ReducedMotion ? desiredFocus : Vector3.SmoothDamp(cameraLookFocus, desiredFocus, ref cameraFocusVelocity, .32f, 300f, dt);
        float distance = zoomed ? focusDistance : mapDistance;
        Vector3 desiredPosition = cameraLookFocus + new Vector3(0, distance * .72f, -distance * .72f);
        float motion = ReducedMotion ? 1f : 1f - Mathf.Exp(-dt * 6f);
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, desiredPosition, motion);
        gameCamera.transform.rotation = Quaternion.Slerp(gameCamera.transform.rotation,
            Quaternion.LookRotation(cameraLookFocus - gameCamera.transform.position, Vector3.up), motion);
        gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView, zoomed ? 34f : 50f, ReducedMotion ? 1 : 1f - Mathf.Exp(-dt * 6f));
    }

    void HandleCameraZoom()
    {
        if (!CameraControlsAvailable || PointerOverUi()) return;
        float scroll = ReadScroll();
        if (Mathf.Abs(scroll) < .001f) return;
        float factor = Mathf.Exp(-scroll * .08f * cameraSensitivity);
        if (NavigationLesson) tutorialZoomTravel += Mathf.Abs(scroll);
        if (tutorialActive && !NavigationLesson) cinematicZoom = Mathf.Clamp(cinematicZoom * factor, .55f, 2.2f);
        else if (zoomed) focusDistance = Mathf.Clamp(focusDistance * factor, OblationGalaxyLayout.FocusNear, OblationGalaxyLayout.FocusFar);
        else mapDistance = Mathf.Clamp(mapDistance * factor, OblationGalaxyLayout.MapNear, OblationGalaxyLayout.MapFar);
    }

    void HandleShortcuts()
    {
        if (EscapePressed())
        {
            if (tutorialActive && !optionsOpen) { FinishTutorial(); return; }
            if (optionsOpen) optionsOpen = false;
            else if (operationsOpen) { operationsOpen = false; paused = false; }
            else if (factoryOpen) factoryOpen = false;
            else if (zoomed) zoomed = false;
            else paused = !paused;
            PlayClick();
        }
        if (FactoryPressed() && !optionsOpen && !paused && pendingAssignment < 0 && (!tutorialActive || Beat == TutorialBeat.Research)) { operationsOpen = false; factoryOpen = !factoryOpen; PlayClick(); }
        if (!tutorialActive && ZoomPressed() && selectedPlanet >= 0) { zoomed = !zoomed; PlayClick(); }
    }

    void HandleWorldInput()
    {
        hoveredPlanet = -1;
        if (PointerOverUi() || mapDragging || operationsOpen || factoryOpen) return;
        int index = PlanetAtPointer();
        if (index < 0) return;
        hoveredPlanet = index;
        if (!LeftPressed() || !TutorialAllowsWorldTarget(index)) return;
        SelectPlanet(index);
        if (!tutorialActive && lastClicked == index && Time.unscaledTime - lastClickTime < .38f) zoomed = true;
        lastClicked = index;
        lastClickTime = Time.unscaledTime;
        PlayClick();
    }

    void EconomyTick()
    {
        int playerWorlds = 0, enemyWorlds = 0;
        for (int i = 0; i < planets.Count; i++)
        {
            Planet p = planets[i];
            if (p.destroyed) continue;
            bool besieged = IsTargeted(i);
            if (!besieged)
            {
                float recovery = p.maxPopulation / 600f;
                p.population = Mathf.Min(p.maxPopulation, p.population + recovery);
                float claimRecovery = recovery / p.maxPopulation;
                if (p.owner == Allegiance.Player)
                {
                    p.enemyClaim = Mathf.Max(0, p.enemyClaim - claimRecovery);
                    p.playerClaim = 1 - p.enemyClaim;
                }
                else if (p.owner == Allegiance.Enemy)
                {
                    p.playerClaim = Mathf.Max(0, p.playerClaim - claimRecovery);
                    p.enemyClaim = 1 - p.playerClaim;
                }
                else
                {
                    p.playerClaim = Mathf.Max(0, p.playerClaim - claimRecovery);
                    p.enemyClaim = Mathf.Max(0, p.enemyClaim - claimRecovery);
                }
            }
            if (p.owner == Allegiance.Player)
            {
                playerWorlds++;
                if (!besieged) TickPlanetProduction(p, i);
            }
            else if (p.owner == Allegiance.Enemy)
            {
                enemyWorlds++;
                if (!besieged) TickEnemyProduction(p, i);
            }
        }
        aiStrength += (.001f + enemyWorlds * .0004f);
        int resolved = playerWorlds;
        for (int i = 0; i < planets.Count; i++) if (planets[i].destroyed) resolved++;
        if (resolved == planets.Count && enemyWorlds == 0) Win();
        if (planets[0].owner != Allegiance.Player || playerWorlds == 0) Lose();
    }

    bool IsTargeted(int index)
    {
        for (int i = 0; i < attacks.Count; i++) if (attacks[i].to == index) return true;
        return false;
    }

    void EnemyTurn()
    {
        for (int i = 0; i < attacks.Count; i++) if (attacks[i].attacker == Allegiance.Enemy) return;
        int bestFrom = -1, bestTo = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < planets.Count; i++)
        {
            if (planets[i].owner != Allegiance.Enemy || !IsConnected(i, Allegiance.Enemy)) continue;
            foreach (int neighbor in planets[i].links)
            {
                Planet target = planets[neighbor];
                if (target.owner == Allegiance.Enemy || target.destroyed || IsTargeted(neighbor)) continue;
                if (target.owner == Allegiance.Player && (campaignElapsed < 90 || tutorialActive)) continue;
                if (tutorialActive && neighbor == tutorialTarget) continue;
                float score = 120 - target.population - target.defense * .35f + UnityEngine.Random.Range(0, 28f);
                if (target.owner == Allegiance.Neutral) score += 1000;
                if (target.owner == Allegiance.Player) score += target.links.Count * 5f;
                if (neighbor == 0) score += 38;
                if (score > bestScore) { bestScore = score; bestFrom = i; bestTo = neighbor; }
            }
        }
        if (bestFrom >= 0) BeginAttack(Allegiance.Enemy, bestFrom, bestTo, AttackMode.Assault);
    }

    bool BeginAttack(Allegiance attacker, int from, int to, AttackMode mode)
    {
        if (from < 0 || to < 0 || !planets[from].links.Contains(to) || IsTargeted(to) ||
            planets[to].destroyed || !IsConnected(from, attacker)) return false;
        float committedUnits = 0;
        if (attacker == Allegiance.Player)
        {
            if (mode == AttackMode.Surprise && !techViral)
            {
                AddLog("먼저 침식 독성을 연구해야 합니다.");
                return false;
            }
            int activeOperations = 0;
            foreach (Attack existing in attacks) if (existing.attacker == Allegiance.Player) activeOperations++;
            if (activeOperations >= (HasResearch("hive3") ? 2 : 1))
            {
                AddLog("전술 능력 슬롯이 모두 사용 중입니다.");
                return false;
            }
            float unitsCost = mode == AttackMode.Orbital ? 2 : mode == AttackMode.Surprise ? 3 : 4;
            OblationCost cost = mode == AttackMode.Surprise ? new OblationCost(25, 0, 18) :
                mode == AttackMode.Orbital ? new OblationCost(0, 25, 25) : new OblationCost(30, 12, 0);
            if (combatUnits < unitsCost || !CanAfford(cost))
            {
                AddLog("작전에 필요한 자원 또는 전투 유닛이 부족합니다.");
                return false;
            }
            if (mode == AttackMode.Orbital && !techOrbital) { AddLog("먼저 궤도 표적화를 연구해야 합니다."); return false; }
            TrySpend(cost);
            combatUnits -= unitsCost;
            committedUnits = unitsCost;
            if (campaignMetrics != null && campaignMetrics.firstRetrySeconds < 0 &&
                campaignMetrics.firstFailedAdjacentAttackSeconds >= 0 && campaignMetrics.failedTargetId == planets[to].definition.id)
                campaignMetrics.firstRetrySeconds = Time.unscaledTime - campaignStartTime;
        }
        Planet source = planets[from], target = planets[to];
        float rawDamage;
        float duration;
        if (attacker == Allegiance.Player)
        {
            rawDamage = mode == AttackMode.Surprise ? 64 + (techViral ? 38 : 0) : mode == AttackMode.Orbital ? 175 : 88 + (techFoundry ? 34 : 0);
            rawDamage += source.population * .12f;
            rawDamage *= UnitAttackBonus(mode) * ModifierMultiplier("attack");
            rawDamage *= CounterMultiplier(mode);
            rawDamage *= EncounterMultiplier(target, mode);
            duration = mode == AttackMode.Surprise ? 3.8f : mode == AttackMode.Orbital ? 4.2f : 5.4f;
            duration *= UnitTravelMultiplier(mode);
            if (mode == AttackMode.Surprise) duration *= target.definition.diseaseTimeEnvironmentMultiplier;
        }
        else
        {
            rawDamage = 63 + aiStrength * 13 + source.population * .09f +
                (string.IsNullOrEmpty(source.enemyUnitId) ? 0 : FindUnit(source.enemyUnitId).attack * .3f);
            duration = 5.3f;
        }
        float armor = mode == AttackMode.Surprise ? target.defense * .18f : mode == AttackMode.Orbital ? target.defense * .05f : target.defense * .48f;
        if (attacker == Allegiance.Player) armor *= 1f - UnitArmorPenetration(mode);
        var attack = new Attack
        {
            attacker = attacker, from = from, to = to, mode = mode, startPopulation = target.population,
            startClaim = attacker == Allegiance.Player ? target.playerClaim : target.enemyClaim,
            damage = Mathf.Max(24, rawDamage - armor), duration = duration * source.definition.attackArrivalMultiplier,
            startAntennaHealth = target.antennaHealth, committedUnits = committedUnits
        };
        Color attackColor = AttackColor(attacker, mode);
        attack.beam = CreateAttackBeam(source.position, target.position, attackColor);
        attack.visual = attack.beam.gameObject.AddComponent<OblationAttackVisual>();
        attack.visual.Initialize(source.position, target.position, attackColor, lineMaterial, AttackEffect(mode), reducedEffects);
        if(visualDirector!=null)visualDirector.Event(source.position,attackColor,AttackEffect(mode),4);
        attacks.Add(attack);
        if(attacker==Allegiance.Enemy)QueueStory(StoryChapter.Rival);
        if (attacker == Allegiance.Player) RecordResearchAction("attack:" + mode);
        if (attacker == Allegiance.Player) RecordEnemyAdaptation(mode);
        if (attacker == Allegiance.Enemy && target.owner == Allegiance.Player) { defendPlanet = to; AddLog("적 함대 접근 중: " + target.name); }
        else AddLog((mode == AttackMode.Surprise ? "역병 함대가 향하는 곳: " : mode == AttackMode.Orbital ? "궤도 심판의 목표: " : "함대가 진군하는 곳: ") + target.name);
        sfxSource.PlayOneShot(attackClip, sfxVolume);
        return true;
    }

    LineRenderer CreateAttackBeam(Vector3 from, Vector3 to, Color color)
    {
        var beamObject = new GameObject("공격 경로");
        beamObject.transform.SetParent(effectsRoot);
        var beam = beamObject.AddComponent<LineRenderer>();
        ConfigureLine(beam, color, .12f, false);
        beam.positionCount = 9;
        for (int i = 0; i < 9; i++)
        {
            float t = i / 8f;
            Vector3 point = Vector3.Lerp(from, to, t);
            point.y += Mathf.Sin(t * Mathf.PI) * 1.25f;
            beam.SetPosition(i, point);
        }
        return beam;
    }

    void UpdateAttacks(float dt)
    {
        for (int i = attacks.Count - 1; i >= 0; i--)
        {
            Attack attack = attacks[i];
            if (!IsConnected(attack.from, attack.attacker))
            {
                if (attack.beam != null) Destroy(attack.beam.gameObject);
                AddLog(planets[attack.to].name + " 작전 중단: 안테나 연결이 끊겼습니다.");
                attacks.RemoveAt(i);
                continue;
            }
            attack.elapsed += dt;
            float progress = Mathf.Clamp01(attack.elapsed / attack.duration);
            if (attack.visual != null) attack.visual.SetProgress(progress);
            ConquestRuleSO rule = catalog.conquestRule;
            if (rule != null && rule.stageThresholds != null && rule.stageNames != null)
                while (attack.stage < rule.stageThresholds.Length && attack.stage < rule.stageNames.Length &&
                    progress >= rule.stageThresholds[attack.stage])
                {
                    AddLog(planets[attack.to].name + " // " + rule.stageNames[attack.stage]);
                    attack.stage++;
                }
            Planet target = planets[attack.to];
            if (attack.attacker == Allegiance.Enemy && target.antennaActive)
            {
                target.antennaHealth = Mathf.Max(0, attack.startAntennaHealth - attack.damage * progress);
                if (target.antennaHealth <= 0)
                {
                    target.antennaActive = false;
                    if (target.owner == Allegiance.Player)
                        AddResources(new OblationCost(0, catalog.antenna.installationCost.minerals * .25f, 0));
                    AddLog(target.name + " 안테나 파괴. 연결이 끊겼습니다.");
                }
            }
            target.population = Mathf.Lerp(attack.startPopulation, Mathf.Max(0, attack.startPopulation - attack.damage), Smooth(progress));
            float claim = Mathf.Clamp01(attack.startClaim + (attack.startPopulation - target.population) / target.maxPopulation);
            if (attack.attacker == Allegiance.Player)
            {
                target.playerClaim = claim;
                target.enemyClaim = target.owner == Allegiance.Enemy ? 1 - claim : Mathf.Min(target.enemyClaim, 1 - claim);
            }
            else
            {
                target.enemyClaim = claim;
                target.playerClaim = target.owner == Allegiance.Player ? 1 - claim : Mathf.Min(target.playerClaim, 1 - claim);
            }
            if (progress < 1) continue;
            if (attack.beam != null) Destroy(attack.beam.gameObject);
            target.hitFlash = 1;
            if(visualDirector!=null&&target.population>.6f)visualDirector.Event(target.position,AttackColor(attack.attacker,attack.mode),AttackEffect(attack.mode),7);
            if(attack.mode==AttackMode.Assault) OblationCombatBurst.Spawn(effectsRoot, target.position, AttackColor(attack.attacker, attack.mode),
                impactMaterial, reducedEffects ? 8 : 28, reducedEffects);
            if (attack.attacker == Allegiance.Player)
                combatUnits = Mathf.Min(120, combatUnits + attack.committedUnits * UnitSurvivalRefund(attack.mode));
            if (target.population <= .6f)
            {
                Allegiance winner = target.playerClaim > target.enemyClaim ? Allegiance.Player :
                    target.enemyClaim > target.playerClaim ? Allegiance.Enemy : attack.attacker;
                Capture(attack.to, winner);
            }
            else
            {
                AddLog(target.name + " 작전 실패. 잔존 인구 " + Mathf.CeilToInt(target.population) + "억.");
                if (attack.attacker == Allegiance.Player && campaignMetrics != null &&
                    campaignMetrics.firstFailedAdjacentAttackSeconds < 0 && attack.from == 0)
                {
                    campaignMetrics.firstFailedAdjacentAttackSeconds = Time.unscaledTime - campaignStartTime;
                    campaignMetrics.failedTargetId = target.definition.id;
                }
            }
            if (defendPlanet == attack.to) defendPlanet = -1;
            attacks.RemoveAt(i);
        }
    }

    static float Smooth(float value) => value * value * (3f - 2f * value);

    void Capture(int index, Allegiance owner)
    {
        Planet p = planets[index];
        Allegiance previous = p.owner;
        bool hadAntenna = p.antennaActive;
        p.owner = owner;
        p.antennaActive = index == (owner == Allegiance.Player ? 0 : 1);
        p.antennaHealth = p.antennaActive ? catalog.antenna.durability : 0;
        p.antennaRemaining = 0;
        p.enemyUnitId = null;
        p.enemyProductionClock = 0;
        p.population = 0;
        p.playerClaim = owner == Allegiance.Player ? 1 : 0;
        p.enemyClaim = owner == Allegiance.Enemy ? 1 : 0;
        if (owner == Allegiance.Player)
        {
            p.type = index == 0 ? WorldType.Unit : WorldType.Unassigned;
            if (index != 0) { pendingAssignment = index; paused = true; }
            if (previous != Allegiance.Player && catalog.conquestRule != null)
                AddResources(catalog.conquestRule.completionReward);
            if (previous != Allegiance.Player && campaignMetrics != null && campaignMetrics.firstConquestSeconds < 0)
                campaignMetrics.firstConquestSeconds = Time.unscaledTime - campaignStartTime;
            if (previous != Allegiance.Player && p.definition.captureTechnology != null &&
                capturedTechnologies.Add(p.definition.captureTechnology.id))
                AddLog(p.name + " 고유 기술 획득: " + p.definition.captureTechnology.displayName + ".");
            AddLog(p.name + " 점령 완료. 그 특이점이 공물에 합류합니다.");
        }
        else
        {
            if (previous == Allegiance.Player && hadAntenna && catalog.antenna != null)
                AddResources(new OblationCost(0, catalog.antenna.installationCost.minerals * .25f, 0));
            p.type = (WorldType)(1 + index % 3);
            p.antennaRemaining = catalog.antenna != null ? catalog.antenna.installationSeconds : 30;
            AddLog(p.name + "이(가) 적 대사제에게 함락되었습니다.");
        }
        if (previous != owner) SpawnShockwave(p.position, owner == Allegiance.Player ? PlayerColor : EnemyColor);
        sfxSource.PlayOneShot(captureClip, sfxVolume);
        UpdateAllVisuals();
        if (index == 0 && owner == Allegiance.Enemy) Lose();
    }

    void SpawnShockwave(Vector3 position, Color color)
    {
        if(visualDirector!=null)visualDirector.Event(position,color,OblationEffectKind.Capture,8);
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "정복 충격파";
        go.transform.SetParent(effectsRoot);
        go.transform.position = position;
        Destroy(go.GetComponent<Collider>());
        var material = new Material(haloTemplate);
        material.SetColor("_OwnerColor", color);
        material.SetFloat("_Progress", 1f);
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.AddComponent<OblationShockwave>().Initialize(material, reducedEffects || ReducedMotion);
        OblationCombatBurst.Spawn(effectsRoot, position, color, impactMaterial, reducedEffects ? 10 : 40, reducedEffects);
    }

    void TriggerEvent()
    {
        switch (UnityEngine.Random.Range(0, 4))
        {
            case 0: AddResources(new OblationCost(0, 28, 0)); AddLog("운석 공물: 광물 잔해 +28."); break;
            case 1: AddResources(new OblationCost(0, 0, 22)); AddLog("신의 꿈: 신경 에너지 +22."); break;
            case 2: biomass = Mathf.Max(0, biomass - 14); AddLog("생물권 고갈: 생체 물질 -14."); break;
            default: AddResources(new OblationCost(18, 0, 0)); aiStrength += .25f; AddLog("군비 경쟁: 생체 물질 +18, 적 위협 상승."); break;
        }
    }

    void Defend()
    {
        if (defendPlanet < 0 || !TrySpend(new OblationCost(0, 15, 0))) return;
        for (int i = 0; i < attacks.Count; i++)
        {
            Attack attack = attacks[i];
            if (attack.to != defendPlanet || attack.attacker != Allegiance.Enemy || attack.defended) continue;
            attack.damage *= .44f;
            attack.duration += 1.2f;
            attack.defended = true;
            AddLog(planets[defendPlanet].name + "에 방어망을 전개했습니다.");
            PlayClick();
            return;
        }
    }

    void AssignType(WorldType type)
    {
        if (pendingAssignment < 0) return;
        planets[pendingAssignment].type = type;
        AddLog(planets[pendingAssignment].name + "의 특화: " + WorldTypeText(type) + ".");
        pendingAssignment = -1;
        paused = false;
        PlayClick();
    }

    void Win()
    {
        if (state != ScreenState.Playing) return;
        state = ScreenState.Victory;
        FinishCampaignMetrics("victory");
        paused = true;
        AddLog("적이 굴복했습니다. 은하가 제물을 바칠 준비를 마쳤습니다.");
    }

    void Lose()
    {
        if (state != ScreenState.Playing) return;
        state = ScreenState.Defeat;
        FinishCampaignMetrics("defeat");
        paused = true;
        AddLog("당신의 제단 행성이 침묵했습니다.");
    }

    int CountOwned(Allegiance owner)
    {
        int count = 0;
        for (int i = 0; i < planets.Count; i++) if (planets[i].owner == owner) count++;
        return count;
    }

    void UpdateAllVisuals()
    {
        for (int i = 0; i < planets.Count; i++)
        {
            Planet p = planets[i];
            bool visible = !zoomed || i == selectedPlanet || state == ScreenState.Title;
            planetViews[i].gameObject.SetActive(visible);
            if (!visible) continue;
            Color ownerColor = OwnerColor(p.owner);
            Material body = p.bodyRenderer.material;
            body.SetColor("_OwnerColor", ownerColor);
            body.SetFloat("_Selected", i == selectedPlanet ? 1f : 0f);
            body.SetFloat("_SceneFocus", tutorialActive && i != 0 && i != tutorialTarget ? .22f : 1f);
            planetViews[i].SetAtmosphere(Color.Lerp(body.GetColor("_AccentColor"),ownerColor,.4f),
                (reducedEffects?.3f:1)*(tutorialActive&&i!=0&&i!=tutorialTarget?.18f:1),
                tutorialActive&&i!=0&&i!=tutorialTarget?.22f:1);
            p.hitFlash = Mathf.MoveTowards(p.hitFlash, 0, Time.unscaledDeltaTime * 2.8f);
            body.SetFloat("_HitFlash", reducedEffects ? p.hitFlash * .2f : p.hitFlash);
            Material halo = p.haloRenderer.material;
            halo.SetColor("_OwnerColor", PlayerColor);
            halo.SetColor("_ContenderColor", EnemyColor);
            halo.SetFloat("_Progress", p.playerClaim);
            halo.SetFloat("_ContestedProgress", p.enemyClaim);
            halo.SetFloat("_Opacity",.075f);
        }
        if (selectionRing != null)
        {
            bool show = selectedPlanet >= 0 && state == ScreenState.Playing && !tutorialActive;
            selectionRing.SetActive(show);
            if (show)
            {
                Planet p = planets[selectedPlanet];
                float radius = p.scale * (1.55f + (ReducedMotion ? 0 : Mathf.Sin(Time.unscaledTime * 1.8f) * .025f));
                Color color = OwnerColor(p.owner); color.a = .82f;
                selectionLine.startColor = selectionLine.endColor = color;
                for (int i = 0; i <= 64; i++)
                {
                    float angle = i / 64f * Mathf.PI * 2;
                    selectionLine.SetPosition(i, p.position + new Vector3(Mathf.Cos(angle) * radius, -.02f, Mathf.Sin(angle) * radius));
                }
            }
        }
        for (int i = 0; i < routes.Count; i++)
        {
            routes[i].enabled = !zoomed || state == ScreenState.Title;
            int a = Edges[i, 0], b = Edges[i, 1];
            Color color = planets[a].owner != Allegiance.Neutral && planets[a].owner == planets[b].owner ? OwnerColor(planets[a].owner) : new Color(.18f, .4f, .65f, .28f);
            color.a = planets[a].owner == planets[b].owner ? .5f : .22f;
            if (tutorialActive) color.a = a == 0 && b == tutorialTarget || b == 0 && a == tutorialTarget ? .7f : .035f;
            routes[i].startColor = routes[i].endColor = color;
            float routeWidth=Mathf.Clamp(Vector3.Distance(gameCamera.transform.position,cameraLookFocus)*.0016f,.045f,.34f);
            routes[i].startWidth=routes[i].endWidth=routeWidth;
        }
    }

    static Color OwnerColor(Allegiance owner) => owner == Allegiance.Player ? PlayerColor : owner == Allegiance.Enemy ? EnemyColor : NeutralColor;

    static Color AttackColor(Allegiance owner, AttackMode mode) => owner == Allegiance.Enemy ? EnemyColor :
        mode == AttackMode.Surprise ? new Color(.4f, 1, .5f) : mode == AttackMode.Orbital ? new Color(.8f, .5f, 1) : PlayerColor;

    void AddLog(string message)
    {
        if (feedbackText != null && !message.Contains(" // ")) { feedbackText.text = message; feedbackUntil = Time.unscaledTime + 3f; }
        eventLog.Insert(0, message);
        if (eventLog.Count > 5) eventLog.RemoveAt(eventLog.Count - 1);
    }

    static string AllegianceText(Allegiance value) => value == Allegiance.Player ? "플레이어" : value == Allegiance.Enemy ? "적" : "중립";

    static string WorldTypeText(WorldType value) => value == WorldType.Unit ? "유닛 특화" : value == WorldType.Manufacturing ? "자원 특화" : value == WorldType.Energy ? "연구 특화" : "미지정";

    static string ResourceText(OblationResource value) => value == OblationResource.Biomass ? "생체" :
        value == OblationResource.Minerals ? "광물" : value == OblationResource.Neural ? "신경" : "공물";

    string ProductionText(Planet p)
    {
        if (p.destroyed) return "행성이 파괴되어 생산할 수 없습니다.";
        if (p.owner == Allegiance.Player && !IsConnected(Array.IndexOf(catalog.planets, p.definition), Allegiance.Player))
            return "안테나 연결 끊김: 생체·광물·신경 생산 정지";
        OblationCost rate = ProductionPerMinute(p);
        OblationCost reward = catalog.conquestRule.completionReward;
        string queue = p.owner != Allegiance.Player ? "점령 후 안테나 연결 필요" :
            string.IsNullOrEmpty(p.queuedUnitId) ? "대기열 없음" : FindUnit(p.queuedUnitId).displayName + " " + p.unitRemaining.ToString("0") + "초";
        return $"분당 생체/광물/신경 {rate.biomass:0}/{rate.minerals:0}/{rate.neural:0} · 단절 시 0\n점령 보상 {reward.biomass:0}/{reward.minerals:0}/{reward.neural:0} + 공물{reward.offering:0}  |  {queue}";
    }

    void PlayClick()
    {
        if (sfxSource != null && clickClip != null) sfxSource.PlayOneShot(clickClip, sfxVolume);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    bool PointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    static Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    static bool LeftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static float ReadScroll()
    {
#if ENABLE_INPUT_SYSTEM
        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange) scroll /= 120f;
#endif
        return scroll;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    static Vector2 ReadMove()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return Vector2.zero;
        return new Vector2((Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0), (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)).normalized;
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
#endif
    }

    static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    static bool FactoryPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }

    static bool ZoomPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Return);
#endif
    }
}
