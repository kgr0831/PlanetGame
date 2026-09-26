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
        public LineRenderer beam;
    }

    static readonly Color PlayerColor = new Color(0.12f, 0.78f, 1f);
    static readonly Color EnemyColor = new Color(1f, 0.18f, 0.36f);
    static readonly Color NeutralColor = new Color(0.48f, 0.55f, 0.65f);
    static readonly string[] Names =
    {
        "VESPER", "NEMESIS", "KHEPRI", "TALOS", "MORROW", "EIDOLON",
        "ORISON", "CINDER", "HALCYON", "PERIHELION", "GOLGOTHA", "SERAPH"
    };
    static readonly string[] Traits =
    {
        "순례자의 요람: 균형 잡힌 공물", "기계의 묘지: 높은 방어력",
        "불안정 맨틀: +광물", "잃어버린 조선소: +산업력", "침묵의 합창단: +정신력",
        "중력 우물: 공격 도착 시간 단축", "야생 생물권: 높은 인구",
        "잿빛 위성: 낮은 방어력", "에너지 바다: +에너지", "예언자의 금고: +정신력",
        "강철 대성당: +산업력", "왕관 행성: 인공지능 지휘 핵"
    };
    static readonly Vector3[] Positions =
    {
        new Vector3(-9, 0, -5), new Vector3(9, 0, 5), new Vector3(-5, 0, -7),
        new Vector3(0, 0, -8), new Vector3(5, 0, -6), new Vector3(-8, 0, 0),
        new Vector3(-2, 0, -1), new Vector3(3, 0, 0), new Vector3(8, 0, 0),
        new Vector3(-5, 0, 5), new Vector3(0, 0, 7), new Vector3(5, 0, 7)
    };
    static readonly int[,] Edges =
    {
        {0,2},{0,5},{2,3},{2,6},{3,4},{3,6},{4,7},{4,8},{5,6},{5,9},
        {6,7},{6,9},{6,10},{7,8},{7,10},{7,11},{8,1},{8,11},{9,10},{10,11},{11,1}
    };

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
    float ore = 70;
    float industry = 50;
    float psi = 35;
    float fuel = 55;
    float combatUnits = 16;
    float laborUnits = 8;
    float aiStrength = 1;
    float economyClock;
    float aiClock;
    float eventClock;
    float fade;
    float mapDistance = 25;
    float musicVolume = .55f;
    float sfxVolume = .7f;
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
        gameCamera.farClipPlane = 250;
    }

    bool BindGalaxy()
    {
        if (gameCamera == null || musicSource == null || sfxSource == null || effectsRoot == null ||
            haloTemplate == null || lineMaterial == null || selectionRing == null || selectionLine == null ||
            planetViews == null || planetViews.Length != Positions.Length ||
            routeViews == null || routeViews.Length != Edges.GetLength(0))
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

            planets.Add(new Planet
            {
                name = Names[i],
                trait = Traits[i],
                position = view.transform.position,
                scale = view.bodyRenderer.transform.lossyScale.x,
                maxPopulation = i == 0 || i == 1 ? 120 : 60 + (i * 17) % 61,
                defense = i == 1 ? 62 : 12 + (i * 11) % 30,
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
        clickClip = CreateTone("화면 조작음", 580, .09f, .18f);
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
        ore = 70; industry = 50; psi = 35; fuel = 55;
        combatUnits = 16; laborUnits = 8;
        aiStrength = 1; economyClock = 0; aiClock = 2; eventClock = 0;
        sourcePlanet = 0; selectedPlanet = 0; targetPlanet = -1; pendingAssignment = -1; defendPlanet = -1;
        techViral = techFoundry = techOrbital = false;
        paused = factoryOpen = optionsOpen = false;
        zoomed = begin;
        cameraFocus = Vector3.zero;
        AddLog("은하가 약속되었습니다. 공물을 거두십시오.");
        state = begin ? ScreenState.Playing : ScreenState.Title;
        fade = begin ? 1f : 0f;
        UpdateAllVisuals();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        RotateWorld(dt);
        UpdateCamera(dt);
        if (musicSource != null) musicSource.volume = musicVolume * .34f;
        if (state != ScreenState.Playing) return;
        fade = Mathf.Max(0, fade - dt * .8f);
        HandleShortcuts();
        if (paused || optionsOpen || pendingAssignment >= 0 || state != ScreenState.Playing) return;
        HandleWorldInput();
        economyClock += dt;
        aiClock += dt;
        eventClock += dt;
        if (economyClock >= 1f) { economyClock -= 1f; EconomyTick(); }
        if (aiClock >= Mathf.Max(3.2f, 6.2f - aiStrength * .18f)) { aiClock = 0; EnemyTurn(); }
        if (eventClock >= 32f) { eventClock = 0; TriggerEvent(); }
        UpdateAttacks(dt);
        UpdateAllVisuals();
    }

    void RotateWorld(float dt)
    {
        for (int i = 0; i < planets.Count; i++)
            if (planets[i].body != null) planets[i].body.transform.Rotate(Vector3.up, dt * (7 + i % 5), Space.World);
    }

    void UpdateCamera(float dt)
    {
        if (gameCamera == null) return;
        Vector3 desiredFocus = zoomed && selectedPlanet >= 0 ? planets[selectedPlanet].position : cameraFocus;
        if (state == ScreenState.Title)
        {
            desiredFocus = Vector3.zero;
            mapDistance = 25 + Mathf.Sin(Time.unscaledTime * .15f) * 1.5f;
        }
        if (state == ScreenState.Playing && !zoomed && !paused && !optionsOpen)
        {
            Vector2 move = ReadMove();
            cameraFocus += new Vector3(move.x, 0, move.y) * dt * 7f * cameraSensitivity;
            cameraFocus.x = Mathf.Clamp(cameraFocus.x, -7f, 7f);
            cameraFocus.z = Mathf.Clamp(cameraFocus.z, -6f, 6f);
            float scroll = ReadScroll();
            if (Mathf.Abs(scroll) > .01f) mapDistance = Mathf.Clamp(mapDistance - scroll * .012f * cameraSensitivity, 15f, 34f);
        }
        float distance = zoomed ? 8.2f : mapDistance;
        Vector3 focus = Vector3.SmoothDamp(gameCamera.transform.forward == Vector3.zero ? desiredFocus : GetCameraLookPoint(), desiredFocus, ref cameraFocusVelocity, .22f, 100, dt);
        Vector3 desiredPosition = focus + new Vector3(0, distance * .72f, -distance * .72f);
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, desiredPosition, 1f - Mathf.Exp(-dt * 5f));
        gameCamera.transform.rotation = Quaternion.Slerp(gameCamera.transform.rotation, Quaternion.LookRotation(focus - gameCamera.transform.position, Vector3.up), 1f - Mathf.Exp(-dt * 7f));
    }

    Vector3 GetCameraLookPoint()
    {
        Ray ray = new Ray(gameCamera.transform.position, gameCamera.transform.forward);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : cameraFocus;
    }

    void HandleShortcuts()
    {
        if (EscapePressed())
        {
            if (optionsOpen) optionsOpen = false;
            else if (factoryOpen) factoryOpen = false;
            else if (zoomed) zoomed = false;
            else paused = !paused;
            PlayClick();
        }
        if (FactoryPressed() && !optionsOpen && pendingAssignment < 0) { factoryOpen = !factoryOpen; PlayClick(); }
        if (ZoomPressed() && selectedPlanet >= 0) { zoomed = !zoomed; PlayClick(); }
    }

    void HandleWorldInput()
    {
        if (!LeftPressed() || PointerOverUi()) return;
        Ray ray = gameCamera.ScreenPointToRay(PointerPosition());
        if (!Physics.Raycast(ray, out RaycastHit hit, 300)) return;
        PlanetMarker marker = hit.collider.GetComponent<PlanetMarker>();
        if (marker == null) return;
        int index = marker.index;
        selectedPlanet = index;
        if (planets[index].owner == Allegiance.Player)
        {
            sourcePlanet = index;
            targetPlanet = -1;
        }
        else if (sourcePlanet >= 0 && planets[sourcePlanet].links.Contains(index)) targetPlanet = index;
        if (lastClicked == index && Time.unscaledTime - lastClickTime < .38f) zoomed = true;
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
                if (besieged) continue;
                float traitBoost = p.trait.Contains("+광물") || p.trait.Contains("+산업력") || p.trait.Contains("+정신력") || p.trait.Contains("+에너지") ? 1.45f : 1f;
                if (i == 0) { ore += 1.2f; industry += .8f; psi += .35f; fuel += .7f; }
                switch (p.type)
                {
                    case WorldType.Unit:
                        combatUnits = Mathf.Min(80, combatUnits + .12f * traitBoost);
                        laborUnits = Mathf.Min(80, laborUnits + .08f * traitBoost);
                        break;
                    case WorldType.Manufacturing:
                        ore += 4.8f * traitBoost;
                        industry += 2.6f * traitBoost;
                        break;
                    case WorldType.Energy:
                        fuel += 2.25f * traitBoost;
                        psi += .35f * traitBoost;
                        break;
                }
            }
            else if (p.owner == Allegiance.Enemy)
            {
                enemyWorlds++;
            }
        }
        if (playerWorlds > 0) { ore += laborUnits * .015f; industry += laborUnits * .01f; }
        aiStrength += (.015f + enemyWorlds * .004f);
        if (playerWorlds == planets.Count || (planets[1].owner == Allegiance.Player && playerWorlds >= 7)) Win();
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
            if (planets[i].owner != Allegiance.Enemy) continue;
            foreach (int neighbor in planets[i].links)
            {
                Planet target = planets[neighbor];
                if (target.owner == Allegiance.Enemy || IsTargeted(neighbor)) continue;
                float score = 120 - target.population - target.defense * .35f + UnityEngine.Random.Range(0, 28f);
                if (target.owner == Allegiance.Player) score += 22;
                if (neighbor == 0) score += 38;
                if (score > bestScore) { bestScore = score; bestFrom = i; bestTo = neighbor; }
            }
        }
        if (bestFrom >= 0) BeginAttack(Allegiance.Enemy, bestFrom, bestTo, AttackMode.Assault);
    }

    bool BeginAttack(Allegiance attacker, int from, int to, AttackMode mode)
    {
        if (from < 0 || to < 0 || !planets[from].links.Contains(to) || IsTargeted(to)) return false;
        if (attacker == Allegiance.Player)
        {
            float unitsCost = mode == AttackMode.Orbital ? 4 : mode == AttackMode.Surprise ? 6 : 8;
            float oreCost = mode == AttackMode.Assault ? 10 : mode == AttackMode.Orbital ? 20 : 0;
            float industryCost = mode == AttackMode.Assault ? 16 : mode == AttackMode.Orbital ? 55 : 8;
            float psiCost = mode == AttackMode.Surprise ? 18 : mode == AttackMode.Orbital ? 35 : 0;
            float fuelCost = mode == AttackMode.Orbital ? 22 : 12;
            if (ore < oreCost || industry < industryCost || psi < psiCost || fuel < fuelCost || combatUnits < unitsCost)
            {
                AddLog("작전에 필요한 자원 또는 전투 유닛이 부족합니다.");
                return false;
            }
            if (mode == AttackMode.Orbital && !techOrbital) { AddLog("먼저 궤도 칙령을 연구해야 합니다."); return false; }
            ore -= oreCost; industry -= industryCost; psi -= psiCost; fuel -= fuelCost; combatUnits -= unitsCost;
        }
        Planet source = planets[from], target = planets[to];
        float rawDamage;
        float duration;
        if (attacker == Allegiance.Player)
        {
            rawDamage = mode == AttackMode.Surprise ? 64 + (techViral ? 38 : 0) : mode == AttackMode.Orbital ? 175 : 88 + (techFoundry ? 34 : 0);
            rawDamage += source.population * .12f;
            duration = mode == AttackMode.Surprise ? 3.8f : mode == AttackMode.Orbital ? 4.2f : 5.4f;
        }
        else
        {
            rawDamage = 63 + aiStrength * 13 + source.population * .09f;
            duration = 5.3f;
        }
        float armor = mode == AttackMode.Surprise ? target.defense * .18f : mode == AttackMode.Orbital ? target.defense * .05f : target.defense * .48f;
        var attack = new Attack
        {
            attacker = attacker, from = from, to = to, mode = mode, startPopulation = target.population,
            startClaim = attacker == Allegiance.Player ? target.playerClaim : target.enemyClaim,
            damage = Mathf.Max(24, rawDamage - armor), duration = target.trait.Contains("도착 시간 단축") ? duration * .78f : duration
        };
        attack.beam = CreateAttackBeam(source.position, target.position, attacker == Allegiance.Player ? PlayerColor : EnemyColor);
        attacks.Add(attack);
        if (attacker == Allegiance.Enemy && target.owner == Allegiance.Player) { defendPlanet = to; AddLog("적 함대 접근 중: " + target.name); }
        else AddLog((mode == AttackMode.Surprise ? "은밀한 역병이 향하는 곳: " : mode == AttackMode.Orbital ? "궤도 심판의 목표: " : "함대가 진군하는 곳: ") + target.name);
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
            attack.elapsed += dt;
            float progress = Mathf.Clamp01(attack.elapsed / attack.duration);
            Planet target = planets[attack.to];
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
            if (attack.beam != null)
            {
                Color color = attack.attacker == Allegiance.Player ? PlayerColor : EnemyColor;
                color.a = .35f + Mathf.Sin(Time.unscaledTime * 12f) * .25f;
                attack.beam.startColor = attack.beam.endColor = color;
                attack.beam.widthMultiplier = .75f + Mathf.Sin(Time.unscaledTime * 9f) * .22f;
            }
            if (progress < 1) continue;
            if (attack.beam != null) Destroy(attack.beam.gameObject);
            if (target.population <= .6f)
            {
                Allegiance winner = target.playerClaim > target.enemyClaim ? Allegiance.Player :
                    target.enemyClaim > target.playerClaim ? Allegiance.Enemy : attack.attacker;
                Capture(attack.to, winner);
            }
            else AddLog(target.name + " 작전 실패. 잔존 인구 " + Mathf.CeilToInt(target.population) + "억.");
            if (defendPlanet == attack.to) defendPlanet = -1;
            attacks.RemoveAt(i);
        }
    }

    static float Smooth(float value) => value * value * (3f - 2f * value);

    void Capture(int index, Allegiance owner)
    {
        Planet p = planets[index];
        Allegiance previous = p.owner;
        p.owner = owner;
        p.population = 0;
        p.playerClaim = owner == Allegiance.Player ? 1 : 0;
        p.enemyClaim = owner == Allegiance.Enemy ? 1 : 0;
        if (owner == Allegiance.Player)
        {
            p.type = index == 0 ? WorldType.Unit : WorldType.Unassigned;
            if (index != 0) { pendingAssignment = index; paused = true; }
            AddLog(p.name + " 점령 완료. 그 특이점이 공물에 합류합니다.");
        }
        else
        {
            p.type = (WorldType)(1 + index % 3);
            AddLog(p.name + "이(가) 적 대사제에게 함락되었습니다.");
        }
        if (previous != owner) SpawnShockwave(p.position, owner == Allegiance.Player ? PlayerColor : EnemyColor);
        sfxSource.PlayOneShot(captureClip, sfxVolume);
        UpdateAllVisuals();
        if (index == 0 && owner == Allegiance.Enemy) Lose();
        if (index == 1 && owner == Allegiance.Player && CountOwned(Allegiance.Player) >= 7) Win();
    }

    void SpawnShockwave(Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "정복 충격파";
        go.transform.SetParent(effectsRoot);
        go.transform.position = position;
        Destroy(go.GetComponent<Collider>());
        var material = new Material(haloTemplate);
        material.SetColor("_OwnerColor", color);
        material.SetFloat("_Progress", 1f);
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.AddComponent<OblationShockwave>();
    }

    void TriggerEvent()
    {
        switch (UnityEngine.Random.Range(0, 4))
        {
            case 0: ore += 28; AddLog("운석 공물: 광물 +28."); break;
            case 1: psi += 22; AddLog("신의 꿈: 정신력 +22."); break;
            case 2: fuel = Mathf.Max(0, fuel - 14); AddLog("보급로 파열: 에너지 -14."); break;
            default: industry += 18; aiStrength += .25f; AddLog("군비 경쟁: 산업력 +18, 적 위협 상승."); break;
        }
    }

    void Defend()
    {
        if (defendPlanet < 0 || industry < 15) return;
        industry -= 15;
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
        AddLog(planets[pendingAssignment].name + "의 역할: " + WorldTypeText(type) + ".");
        pendingAssignment = -1;
        paused = false;
        PlayClick();
    }

    void Win()
    {
        if (state != ScreenState.Playing) return;
        state = ScreenState.Victory;
        paused = true;
        AddLog("적이 굴복했습니다. 은하가 제물을 바칠 준비를 마쳤습니다.");
    }

    void Lose()
    {
        if (state != ScreenState.Playing) return;
        state = ScreenState.Defeat;
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
            Material halo = p.haloRenderer.material;
            halo.SetColor("_OwnerColor", PlayerColor);
            halo.SetColor("_ContenderColor", EnemyColor);
            halo.SetFloat("_Progress", p.playerClaim);
            halo.SetFloat("_ContestedProgress", p.enemyClaim);
        }
        if (selectionRing != null)
        {
            bool show = selectedPlanet >= 0 && state == ScreenState.Playing;
            selectionRing.SetActive(show);
            if (show)
            {
                Planet p = planets[selectedPlanet];
                float radius = p.scale * (1.55f + Mathf.Sin(Time.unscaledTime * 3f) * .06f);
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
            routes[i].startColor = routes[i].endColor = color;
        }
    }

    static Color OwnerColor(Allegiance owner) => owner == Allegiance.Player ? PlayerColor : owner == Allegiance.Enemy ? EnemyColor : NeutralColor;

    void AddLog(string message)
    {
        eventLog.Insert(0, message);
        if (eventLog.Count > 7) eventLog.RemoveAt(eventLog.Count - 1);
    }

    static string AllegianceText(Allegiance value) => value == Allegiance.Player ? "플레이어" : value == Allegiance.Enemy ? "적" : "중립";

    static string WorldTypeText(WorldType value) => value == WorldType.Unit ? "유닛 생산 행성" : value == WorldType.Manufacturing ? "제조 행성" : value == WorldType.Energy ? "에너지 생산 행성" : "미지정";

    static string ProductionText(Planet p)
    {
        return p.type == WorldType.Unit ? "생산: 전투 유닛 +0.12 / 노동 유닛 +0.08 (초당)" :
            p.type == WorldType.Manufacturing ? "생산: 광물 +4.8 (채굴 +50%) / 산업력 +2.6 (초당)" :
            p.type == WorldType.Energy ? "생산: 에너지 +2.25 / 정신력 +0.35 (초당)" : "개발 방향 선택을 기다리는 중입니다.";
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
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0;
#else
        return Input.mouseScrollDelta.y * 120f;
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
