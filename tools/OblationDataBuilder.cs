using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run with: unity command run_script --file tools/OblationDataBuilder.cs --entry OblationDataBuilder.Build
public static class OblationDataBuilder
{
    const string AssetPath = "Assets/Data/OblationCatalog.asset";

    static readonly string[] Names =
    {
        "VESPER", "NEMESIS", "KHEPRI", "TALOS", "MORROW", "EIDOLON",
        "ORISON", "CINDER", "HALCYON", "PERIHELION", "GOLGOTHA", "SERAPH"
    };

    static readonly string[] Descriptions =
    {
        "순례자의 요람: 균형 잡힌 공물", "기계의 묘지: 높은 방어력",
        "불안정 맨틀: 광물 풍부", "잃어버린 조선소: 생산 유리", "침묵의 합창단: 연구 유리",
        "중력 우물: 공격 도착 시간 단축", "야생 생물권: 높은 인구",
        "잿빛 위성: 낮은 방어력", "에너지 바다: 신경 에너지 풍부", "예언자의 금고: 연구 유리",
        "강철 대성당: 고유 유닛 생산", "왕관 행성: 인공지능 지휘 핵"
    };

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before creating data assets.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open Assets/Scenes/SampleScene.unity first.");
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        OblationCatalogSO catalog = AssetDatabase.LoadAssetAtPath<OblationCatalogSO>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<OblationCatalogSO>();
            catalog.name = "OblationCatalog";
            AssetDatabase.CreateAsset(catalog, AssetPath);
            Populate(catalog);
        }
        SetResearchLinks(catalog);
        SetAdditionalData(catalog);
        catalog.modifiers[1].stackingGroup = "industry_minerals";
        catalog.modifiers[2].stackingGroup = "industry_units";
        catalog.modifiers[3].stackingGroup = "industry_research";
        for (int i = 1; i < catalog.modifiers.Length; i++) EditorUtility.SetDirty(catalog.modifiers[i]);
        Transform manager = GameObject.Find("Root")?.transform.Find("Util/Runtime/GameManager");
        if (manager == null) throw new InvalidOperationException("GameManager is missing from the Inspector hierarchy.");
        SerializedObject serialized = new SerializedObject(manager.GetComponent<OblationGame>());
        serialized.FindProperty("catalog").objectReferenceValue = catalog;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Transform specialBody = GameObject.Find("Root")?.transform.Find("Util/World/Planets/GOLGOTHA/Body");
        if (specialBody != null) specialBody.localScale = Vector3.one * 1.45f;
        EditorUtility.SetDirty(catalog);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        return "Created the Notion data catalog and bound it to the saved game scene.";
    }

    static T Add<T>(OblationCatalogSO catalog, string id, string displayName) where T : OblationDefinitionSO
    {
        T item = ScriptableObject.CreateInstance<T>();
        item.name = id;
        item.id = id;
        item.displayName = displayName;
        AssetDatabase.AddObjectToAsset(item, catalog);
        return item;
    }

    static void Populate(OblationCatalogSO catalog)
    {
        catalog.resources = new[]
        {
            Resource(catalog, "biomass", "생체 물질", OblationResource.Biomass, 500, "인구·생물권"),
            Resource(catalog, "minerals", "광물 잔해", OblationResource.Minerals, 500, "산업 행성·전투 잔해"),
            Resource(catalog, "neural", "신경 에너지", OblationResource.Neural, 500, "연구·하이브 연결"),
            Resource(catalog, "offering", "공물", OblationResource.Offering, 12, "정복 목표·특수 조건")
        };
        catalog.traits = new[]
        {
            Trait(catalog, "resource_trait", "자원 특화", OblationTrait.Resource, new OblationCost(0, 50, 0), .4f, -.1f),
            Trait(catalog, "unit_trait", "유닛 특화", OblationTrait.Unit, new OblationCost(50, 20, 0), .2f, -.1f),
            Trait(catalog, "research_trait", "연구 특화", OblationTrait.Research, new OblationCost(0, 20, 50), .25f, -.1f)
        };
        catalog.encounters = new[]
        {
            Encounter(catalog, "frontier", "변방 조우", 1, "vaccine"),
            Encounter(catalog, "fortress", "요새 조우", 2, "fortification"),
            Encounter(catalog, "citadel", "대성당 조우", 3, "underground")
        };
        catalog.planets = new PlanetDefinitionSO[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            PlanetDefinitionSO planet = Add<PlanetDefinitionSO>(catalog, Names[i], Names[i]);
            planet.traitDescription = Descriptions[i];
            planet.maxPopulation = i == 0 || i == 1 ? 120 : 60 + (i * 17) % 61;
            planet.defense = i == 1 ? 62 : 12 + (i * 11) % 30;
            planet.biomassPerMinute = i == 0 ? 30 : 18 + i % 4 * 2;
            planet.mineralsPerMinute = 12 + i % 3 * 3;
            planet.neuralPerMinute = 10 + i % 2 * 3;
            planet.focusResource = (OblationResource)(i % 3);
            SetEnvironment(planet, i);
            planet.unitProductionMultiplier = i == 3 || i == 10 ? .85f : 1f;
            planet.researchSpeedMultiplier = i == 4 || i == 9 ? 1.2f : 1f;
            planet.traitSlots = i == 10 ? 3 : 2;
            planet.largeSpecialPlanet = i == 10;
            planet.uniqueUnitIds = i == 10 ? new[] { "leviathan", "oracle" } : Array.Empty<string>();
            planet.encounter = catalog.encounters[i == 1 || i == 10 ? 2 : i % 3];
            planet.interiorPreview = InteriorPreview(catalog, planet.id, i);
            catalog.planets[i] = planet;
        }
        catalog.units = new[]
        {
            Unit(catalog, "scout", "정찰체", "정찰", 1, 40, 0, 45, 40, 18, 8, "선제", null, false),
            Unit(catalog, "drone", "수확 드론", "지원", 1, 40, 0, 55, 50, 12, 6, "지원", null, false),
            Unit(catalog, "assault", "돌격체", "근접", 2, 65, 10, 75, 100, 30, 5, "무력", "combat1", false),
            Unit(catalog, "carrier", "전염 운반체", "질병", 2, 55, 0, 80, 55, 15, 7, "질병", "combat1", false),
            Unit(catalog, "siege", "공성체", "공성", 3, 90, 35, 110, 140, 42, 3, "무력", "combat2", false),
            Unit(catalog, "psion", "정신 지배체", "정신", 3, 75, 0, 120, 65, 27, 5, "정신", "hive2", false),
            Unit(catalog, "leviathan", "대성당 괴수", "고유 공성", 4, 140, 70, 180, 240, 65, 2, "무력", "combat3", true),
            Unit(catalog, "oracle", "예언체", "고유 정신", 4, 100, 15, 160, 90, 38, 4, "정신", "hive3", true)
        };
        catalog.unitUpgrades = new[]
        {
            Upgrade(catalog, "scout_senses", "정찰체 감각", "scout", 1, 1, 1.3f, "감지"),
            Upgrade(catalog, "scout_speed", "정찰체 기동", "scout", 1, 1, 1.15f, "이동"),
            Upgrade(catalog, "scout_carrier", "전염 운반 전환", "scout", .85f, .8f, 1, "질병"),
            Upgrade(catalog, "assault_health", "돌격체 갑각", "assault", 1.2f, 1, 1, "방어"),
            Upgrade(catalog, "assault_pierce", "돌격체 관통", "assault", 1, 1.1f, 1, "관통"),
            Upgrade(catalog, "assault_digger", "굴착형 전환", "assault", .85f, 1.25f, .8f, "건물")
        };
        catalog.modifiers = new[]
        {
            Modifier(catalog, "hive_combat", "집단 의지 보정", "attack", .1f, "hive"),
            Modifier(catalog, "mineral_yield", "정제 촉수 보정", "minerals", .15f, "industry_minerals"),
            Modifier(catalog, "unit_speed", "부화 가속 보정", "unitTime", -.15f, "industry_units"),
            Modifier(catalog, "research_speed", "사고 증폭 보정", "researchTime", -.15f, "industry_research")
        };
        catalog.research = new[]
        {
            Research(catalog, "hive1", "원격 감각", OblationResearchAxis.Hive, 1, null, "안테나 탐지 반경 +25%", "감지"),
            Research(catalog, "hive2", "집단 의지", OblationResearchAxis.Hive, 2, "hive1", "연결 행성 전투 보정 +10%", "요새화"),
            Research(catalog, "hive3", "초월 명령", OblationResearchAxis.Hive, 3, "hive2", "전술 능력 슬롯 +1 · 역병 절멸 해금", "질병"),
            Research(catalog, "industry1", "정제 촉수", OblationResearchAxis.Industry, 1, null, "광물 생산 +15%", "자원"),
            Research(catalog, "industry2", "부화 가속", OblationResearchAxis.Industry, 2, "industry1", "유닛 생산 시간 -15%", "생산"),
            Research(catalog, "industry3", "사고 증폭", OblationResearchAxis.Industry, 3, "industry2", "연구 시간 -15% · 공성 절멸 해금", "산업"),
            Research(catalog, "combat1", "침식 독성", OblationResearchAxis.Combat, 1, null, "역병 효과와 돌격체 해금", "백신"),
            Research(catalog, "combat2", "공성 갑각", OblationResearchAxis.Combat, 2, "combat1", "중장 유닛 해금", "요새화"),
            Research(catalog, "combat3", "궤도 표적화", OblationResearchAxis.Combat, 3, "combat2", "궤도 타격·궤도 절멸 해금", "지하화")
        };
        catalog.researchTrees = new ResearchTreeSO[3];
        for (int axis = 0; axis < 3; axis++)
        {
            ResearchTreeSO tree = Add<ResearchTreeSO>(catalog, "tree_" + axis, ((OblationResearchAxis)axis).ToString());
            tree.axis = (OblationResearchAxis)axis;
            tree.nodes = Array.FindAll(catalog.research, node => (int)node.axis == axis);
            catalog.researchTrees[axis] = tree;
        }
        catalog.antenna = Add<AntennaDefinitionSO>(catalog, "antenna", "하이브 안테나");
        catalog.antenna.installationCost = new OblationCost(0, 120, 0);
        catalog.antenna.installationSeconds = 30;
        catalog.antenna.controlHops = 1;
        catalog.antenna.detectionHops = 1;
        catalog.conquestRule = Add<ConquestRuleSO>(catalog, "three_stage_conquest", "교란·장악·동화");
        catalog.conquestRule.stageNames = new[] { "교란", "장악", "동화" };
        catalog.conquestRule.stageThresholds = new[] { .33f, .66f, 1f };
        catalog.conquestRule.requiresAntenna = true;
        catalog.conquestRule.retreatResult = "잔존 인구 유지";
        catalog.conquestRule.completionReward = new OblationCost(18, 12, 8, 1);
        catalog.exterminatus = new[]
        {
            Exterminatus(catalog, "plague_end", "역병 절멸", OblationExterminatus.Plague, "hive3", new OblationCost(80, 0, 80, 1), "감염 확산", "인접 생체 물질 감소"),
            Exterminatus(catalog, "siege_end", "공성 절멸", OblationExterminatus.Siege, "industry3", new OblationCost(90, 90, 0, 1), "방어 거점 붕괴", "잔해 광물 회수"),
            Exterminatus(catalog, "orbital_end", "궤도 절멸", OblationExterminatus.Orbital, "combat3", new OblationCost(0, 100, 100, 1), "궤도 표적 확보", "신경 에너지 추가 소모")
        };
    }

    static void SetResearchLinks(OblationCatalogSO catalog)
    {
        catalog.research[1].requiredAntennaCount = 2;
        catalog.research[2].requiredUnitId = "psion";
        catalog.research[7].requiredUnitId = "assault";
        catalog.research[1].modifiers = new[] { catalog.modifiers[0] };
        catalog.research[3].modifiers = new[] { catalog.modifiers[1] };
        catalog.research[4].modifiers = new[] { catalog.modifiers[2] };
        catalog.research[5].modifiers = new[] { catalog.modifiers[3] };
        catalog.research[2].unlockUnitIds = new[] { "oracle" };
        catalog.research[2].unlockExterminatusIds = new[] { "plague_end" };
        catalog.research[5].unlockExterminatusIds = new[] { "siege_end" };
        catalog.research[6].unlockUnitIds = new[] { "assault", "carrier" };
        catalog.research[7].unlockUnitIds = new[] { "siege" };
        catalog.research[8].unlockUnitIds = new[] { "leviathan" };
        catalog.research[8].unlockExterminatusIds = new[] { "orbital_end" };
        foreach (ResearchNodeSO node in catalog.research) EditorUtility.SetDirty(node);
    }

    static T Ensure<T>(OblationCatalogSO catalog, string id, string displayName) where T : OblationDefinitionSO
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
            if (asset is T existing && existing.id == id)
            {
                existing.displayName = displayName;
                EditorUtility.SetDirty(existing);
                return existing;
            }
        return Add<T>(catalog, id, displayName);
    }

    static UnitUpgradeSO EnsureUpgrade(OblationCatalogSO catalog, string id, string name, string unit,
        float health, float attack, float speed, string role)
    {
        UnitUpgradeSO item = Ensure<UnitUpgradeSO>(catalog, id, name);
        item.unitId = unit;
        item.cost = new OblationCost(55, 15, 20);
        item.healthMultiplier = health;
        item.attackMultiplier = attack;
        item.speedMultiplier = speed;
        item.detectionMultiplier = id == "scout_senses" ? 1.3f : 1f;
        item.armorPenetration = id == "assault_pierce" || id == "siege_breach" ? .1f : 0f;
        item.roleTag = role;
        EditorUtility.SetDirty(item);
        return item;
    }

    static void SetAdditionalData(OblationCatalogSO catalog)
    {
        float[] productionTimes = { 8, 10, 12, 14, 18, 20, 28, 26 };
        for (int i = 0; i < catalog.units.Length; i++)
        { catalog.units[i].productionSeconds = productionTimes[i]; EditorUtility.SetDirty(catalog.units[i]); }
        foreach (ResearchNodeSO node in catalog.research)
        {
            node.durationSeconds = node.tier == 1 ? 20 : node.tier == 2 ? 35 : 55;
            node.cost = new OblationCost(0, 0, node.tier == 1 ? 60 : node.tier == 2 ? 100 : 180);
            EditorUtility.SetDirty(node);
        }
        catalog.antenna.installationCost = new OblationCost(0, 80, 0);
        catalog.antenna.installationSeconds = 8;
        EditorUtility.SetDirty(catalog.antenna);
        catalog.traits[(int)OblationTrait.Unit].timeMultiplierPerLevel = .8f;
        catalog.traits[(int)OblationTrait.Research].timeMultiplierPerLevel = .85f;
        EditorUtility.SetDirty(catalog.traits[(int)OblationTrait.Unit]);
        EditorUtility.SetDirty(catalog.traits[(int)OblationTrait.Research]);
        for (int i = 0; i < catalog.planets.Length; i++)
        {
            catalog.planets[i].focusResource = (OblationResource)(i % 3);
            catalog.planets[i].biomassPerMinute = i == 0 ? 54 : 30 + i % 4 * 3;
            catalog.planets[i].mineralsPerMinute = i == 0 ? 36 : 24 + i % 3 * 3;
            catalog.planets[i].neuralPerMinute = i == 0 ? 30 : 18 + i % 2 * 4;
            SetEnvironment(catalog.planets[i], i);
            catalog.planets[i].interiorPreview = InteriorPreview(catalog, catalog.planets[i].id, i,
                catalog.planets[i].interiorPreview);
            if (i == 10) catalog.planets[i].defense = 52;
            EditorUtility.SetDirty(catalog.planets[i]);
        }
        catalog.unitUpgrades = new[]
        {
            EnsureUpgrade(catalog, "scout_senses", "정찰체 감각", "scout", 1, 1, 1, "감지"),
            EnsureUpgrade(catalog, "scout_speed", "정찰체 기동", "scout", 1, 1, 1.15f, "이동"),
            EnsureUpgrade(catalog, "scout_carrier", "전염 운반 전환", "scout", .85f, .8f, 1, "질병"),
            EnsureUpgrade(catalog, "drone_capacity", "드론 적재량", "drone", 1.2f, 1, 1, "지원"),
            EnsureUpgrade(catalog, "drone_harvest", "드론 수확기", "drone", 1, 1.1f, 1, "자원"),
            EnsureUpgrade(catalog, "drone_medic", "치유 드론 전환", "drone", 1, .9f, 1.15f, "방어"),
            EnsureUpgrade(catalog, "assault_health", "돌격체 갑각", "assault", 1.2f, 1, 1, "방어"),
            EnsureUpgrade(catalog, "assault_pierce", "돌격체 관통", "assault", 1, 1, 1, "관통"),
            EnsureUpgrade(catalog, "assault_digger", "굴착형 전환", "assault", .85f, 1.25f, .8f, "건물"),
            EnsureUpgrade(catalog, "carrier_spore", "운반체 포자", "carrier", 1, 1.15f, 1, "질병"),
            EnsureUpgrade(catalog, "carrier_reservoir", "운반체 저장낭", "carrier", 1.2f, 1, 1, "생존"),
            EnsureUpgrade(catalog, "carrier_psionic", "정신 포자 전환", "carrier", .9f, 1.1f, 1, "정신"),
            EnsureUpgrade(catalog, "siege_armor", "공성체 중갑", "siege", 1.2f, 1, 1, "방어"),
            EnsureUpgrade(catalog, "siege_breach", "공성체 관통탄", "siege", 1, 1, 1, "관통"),
            EnsureUpgrade(catalog, "siege_orbital", "궤도 포격 전환", "siege", .9f, 1.2f, .9f, "궤도"),
            EnsureUpgrade(catalog, "psion_focus", "정신체 집중", "psion", 1, 1.15f, 1, "정신"),
            EnsureUpgrade(catalog, "psion_range", "정신체 확장", "psion", 1, 1, 1.2f, "사거리"),
            EnsureUpgrade(catalog, "psion_dominator", "지배형 전환", "psion", .9f, 1.2f, 1, "지배"),
            EnsureUpgrade(catalog, "leviathan_hide", "괴수 외피", "leviathan", 1.2f, 1, 1, "방어"),
            EnsureUpgrade(catalog, "leviathan_maw", "괴수 포식", "leviathan", 1, 1.15f, 1, "무력"),
            EnsureUpgrade(catalog, "leviathan_fortress", "요새형 전환", "leviathan", 1.3f, 1, .8f, "공성"),
            EnsureUpgrade(catalog, "oracle_clarity", "예언체 통찰", "oracle", 1, 1.15f, 1, "정신"),
            EnsureUpgrade(catalog, "oracle_echo", "예언체 잔향", "oracle", 1, 1, 1.2f, "사거리"),
            EnsureUpgrade(catalog, "oracle_plague", "역병 예언 전환", "oracle", .9f, 1.2f, 1, "질병")
        };
        string[] names = { "순례 지식", "기계 해독", "맨틀 채굴", "조선소 부화", "합창 공명", "중력 항법",
            "야생 배양", "잿빛 재활용", "에너지 축전", "예언 연산", "대성당 공성", "왕관 지휘" };
        string[] stats = { "biomass", "attack", "minerals", "unitTime", "neural", "attack",
            "biomass", "minerals", "neural", "researchTime", "attack", "researchTime" };
        float[] values = { .05f, .04f, .07f, -.05f, .07f, .03f, .07f, .06f, .08f, -.05f, .05f, -.04f };
        var modifiers = new StatModifierDefinitionSO[4 + catalog.planets.Length];
        Array.Copy(catalog.modifiers, modifiers, 4);
        for (int i = 0; i < catalog.planets.Length; i++)
        {
            string id = "capture_" + catalog.planets[i].id.ToLowerInvariant();
            StatModifierDefinitionSO modifier = Ensure<StatModifierDefinitionSO>(catalog, id, names[i]);
            modifier.targetType = "player";
            modifier.statKey = stats[i];
            modifier.operation = "multiply";
            modifier.value = values[i];
            modifier.stackingGroup = id;
            catalog.planets[i].captureTechnology = modifier;
            modifiers[4 + i] = modifier;
            EditorUtility.SetDirty(modifier);
            EditorUtility.SetDirty(catalog.planets[i]);
        }
        catalog.modifiers = modifiers;
    }

    static void SetEnvironment(PlanetDefinitionSO planet, int index)
    {
        planet.biomassEnvironmentMultiplier = index == 6 ? 1.25f : 1f;
        planet.mineralsEnvironmentMultiplier = index == 2 ? 1.25f : index == 10 ? 1.2f : 1f;
        planet.neuralEnvironmentMultiplier = index == 8 ? 1.25f : 1f;
        planet.unitTimeEnvironmentMultiplier = index == 3 ? .8f : index == 2 ? .85f : 1f;
        planet.diseaseTimeEnvironmentMultiplier = index == 2 ? .75f : index == 3 ? .85f :
            index == 7 ? 1.15f : 1f;
        planet.attackArrivalMultiplier = index == 5 ? .78f : 1f;
    }

    static Texture2D InteriorPreview(OblationCatalogSO catalog, string planetId, int index, Texture2D current = null)
    {
        string name = "interior_" + planetId.ToLowerInvariant();
        Texture2D texture = current != null && current.name == name ? current : null;
        if (texture == null)
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
                if (asset is Texture2D existing && existing.name == name) { texture = existing; break; }
        if (texture == null)
        {
            texture = new Texture2D(128, 96, TextureFormat.RGBA32, false) { name = name };
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            AssetDatabase.AddObjectToAsset(texture, catalog);
        }
        Color[] palette =
        {
            new Color(.22f,.65f,.72f), new Color(.54f,.48f,.64f), new Color(.9f,.45f,.18f),
            new Color(.72f,.42f,.2f), new Color(.35f,.62f,.82f), new Color(.58f,.36f,.76f),
            new Color(.35f,.75f,.42f), new Color(.46f,.59f,.76f), new Color(.42f,.83f,.86f),
            new Color(.62f,.53f,.88f), new Color(.72f,.68f,.62f), new Color(.82f,.38f,.58f)
        };
        Color accent = palette[index];
        var pixels = new Color[128 * 96];
        for (int y = 0; y < 96; y++)
            for (int x = 0; x < 128; x++)
            {
                float horizon = 51 + Mathf.Sin(x * .09f + index) * 3 + Mathf.Sin(x * .21f) * 2;
                float grain = Mathf.Sin(x * .37f + y * .21f + index) * .06f;
                Color pixel;
                if (y > horizon)
                {
                    float haze = Mathf.Clamp01((y - horizon) / 45f);
                    pixel = Color.Lerp(accent * .5f, new Color(.015f, .035f, .075f), haze);
                }
                else
                {
                    float depth = Mathf.Clamp01((horizon - y) / 55f);
                    float layer = .65f + .12f * Mathf.Sin(y * .4f + x * .035f);
                    pixel = Color.Lerp(accent * layer, new Color(.025f, .035f, .06f), depth * .75f);
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(Mathf.Pow((x - 64f) / 67f, 2f) +
                        Mathf.Pow((y - 13f) / 30f, 2f)));
                    pixel = Color.Lerp(pixel, accent * 1.35f, core * .7f);
                }
                float tower = Mathf.Abs(x - (22 + index % 3 * 5)) < 3 && y > horizon && y < horizon + 16 ? .35f : 0f;
                pixels[y * 128 + x] = new Color(
                    Mathf.Clamp01(pixel.r + grain + tower * accent.r),
                    Mathf.Clamp01(pixel.g + grain + tower * accent.g),
                    Mathf.Clamp01(pixel.b + grain + tower * accent.b), 1f);
            }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    static ResourceDefinitionSO Resource(OblationCatalogSO catalog, string id, string name, OblationResource kind, float cap, string source)
    {
        ResourceDefinitionSO item = Add<ResourceDefinitionSO>(catalog, id, name);
        item.resource = kind; item.storageCap = cap; item.acquisitionCategory = source; item.iconKey = id;
        return item;
    }

    static PlanetTraitSO Trait(OblationCatalogSO catalog, string id, string name, OblationTrait kind, OblationCost cost, float bonus, float penalty)
    {
        PlanetTraitSO item = Add<PlanetTraitSO>(catalog, id, name);
        item.category = kind; item.firstUpgradeCost = cost; item.primaryBonus = bonus;
        item.secondaryPenalty = penalty; item.visualKeyword = id;
        item.timeMultiplierPerLevel = kind == OblationTrait.Unit ? .8f : kind == OblationTrait.Research ? .85f : 1f;
        return item;
    }

    static EncounterDefinitionSO Encounter(OblationCatalogSO catalog, string id, string name, int level, string counter)
    {
        EncounterDefinitionSO item = Add<EncounterDefinitionSO>(catalog, id, name);
        item.threatLevel = level; item.counterTags = new[] { counter };
        item.defenseGoals = new[] { "방어 전력 억제" }; item.conquestStages = 3;
        item.completionReward = new OblationCost(18, 12, 8, 1);
        return item;
    }

    static UnitDefinitionSO Unit(OblationCatalogSO catalog, string id, string name, string role, int tier,
        float biomass, float minerals, float seconds, float health, float attack, float speed,
        string attackType, string research, bool largeOnly)
    {
        UnitDefinitionSO item = Add<UnitDefinitionSO>(catalog, id, name);
        item.role = role; item.tier = tier; item.productionCost = new OblationCost(biomass, minerals, 0);
        item.productionSeconds = seconds; item.health = health; item.attack = attack;
        item.movementSpeed = speed; item.attackType = attackType;
        item.requiredResearchId = research; item.largePlanetOnly = largeOnly;
        return item;
    }

    static UnitUpgradeSO Upgrade(OblationCatalogSO catalog, string id, string name, string unit,
        float health, float attack, float speed, string role)
    {
        UnitUpgradeSO item = Add<UnitUpgradeSO>(catalog, id, name);
        item.unitId = unit; item.cost = new OblationCost(55, 15, 20);
        item.healthMultiplier = health; item.attackMultiplier = attack; item.speedMultiplier = speed; item.roleTag = role;
        return item;
    }

    static StatModifierDefinitionSO Modifier(OblationCatalogSO catalog, string id, string name, string stat, float value, string group)
    {
        StatModifierDefinitionSO item = Add<StatModifierDefinitionSO>(catalog, id, name);
        item.targetType = "player"; item.statKey = stat; item.operation = "multiply";
        item.value = value; item.stackingGroup = group;
        return item;
    }

    static ResearchNodeSO Research(OblationCatalogSO catalog, string id, string name, OblationResearchAxis axis,
        int tier, string prerequisite, string description, string counter)
    {
        ResearchNodeSO item = Add<ResearchNodeSO>(catalog, id, name);
        item.axis = axis; item.tier = tier;
        item.cost = new OblationCost(0, 0, tier == 1 ? 80 : tier == 2 ? 160 : 300);
        item.durationSeconds = tier == 1 ? 60 : tier == 2 ? 110 : 180;
        item.prerequisites = prerequisite == null ? Array.Empty<string>() : new[] { prerequisite };
        item.requiredPlanets = tier;
        item.description = description; item.counterTag = counter;
        return item;
    }

    static ExterminatusDefinitionSO Exterminatus(OblationCatalogSO catalog, string id, string name,
        OblationExterminatus kind, string research, OblationCost cost, string condition, string sideEffect)
    {
        ExterminatusDefinitionSO item = Add<ExterminatusDefinitionSO>(catalog, id, name);
        item.kind = kind; item.requiredResearchId = research; item.activationCost = cost;
        item.requiresScouting = true; item.requiresActiveAntenna = true;
        item.targetCondition = condition; item.sideEffect = sideEffect; item.presentationKey = id;
        return item;
    }
}
