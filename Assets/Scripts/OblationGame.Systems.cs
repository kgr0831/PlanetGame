using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed partial class OblationGame
{
    readonly HashSet<string> completedResearch = new HashSet<string>();
    readonly HashSet<string> completedUpgrades = new HashSet<string>();
    readonly HashSet<string> capturedTechnologies = new HashSet<string>();
    readonly Dictionary<string, int> unitInventory = new Dictionary<string, int>();
    ResearchNodeSO activeResearch;
    float researchRemaining;
    OblationResearchAxis? lastResearchAxis;
    int plagueOperations;
    int siegeOperations;
    int orbitalOperations;
    [Serializable] sealed class ResearchMetric
    {
        public string researchId;
        public float selectedSeconds;
        public float completedSeconds = -1;
        public string nextAction;
        public float nextActionSeconds = -1;
    }

    [Serializable] sealed class CampaignMetrics
    {
        public string campaignId;
        public string outcome;
        public float durationSeconds;
        public float startingBiomass;
        public float startingMinerals;
        public float startingNeural;
        public float firstAntennaSeconds = -1;
        public float firstConquestSeconds = -1;
        public float firstTierTwoResearchSeconds = -1;
        public float firstOfferingSeconds = -1;
        public float firstExterminatusReadySeconds = -1;
        public float firstFailedAdjacentAttackSeconds = -1;
        public float firstRetrySeconds = -1;
        public string failedTargetId;
        public List<ResearchMetric> research = new List<ResearchMetric>();
    }

    CampaignMetrics campaignMetrics;
    float campaignStartTime;
    bool metricsWritten;

    void StartCampaignMetrics()
    {
        campaignMetrics = new CampaignMetrics
        {
            campaignId = Guid.NewGuid().ToString("N"),
            startingBiomass = biomass,
            startingMinerals = minerals,
            startingNeural = neural
        };
        campaignStartTime = Time.unscaledTime;
        metricsWritten = false;
    }

    void RecordResearchAction(string action)
    {
        if (campaignMetrics == null || metricsWritten) return;
        for (int i = campaignMetrics.research.Count - 1; i >= 0; i--)
        {
            ResearchMetric metric = campaignMetrics.research[i];
            if (metric.completedSeconds < 0 || !string.IsNullOrEmpty(metric.nextAction)) continue;
            metric.nextAction = action;
            metric.nextActionSeconds = Time.unscaledTime - campaignStartTime;
            break;
        }
    }

    void FinishCampaignMetrics(string outcome)
    {
        if (campaignMetrics == null || metricsWritten) return;
        metricsWritten = true;
        campaignMetrics.outcome = outcome;
        campaignMetrics.durationSeconds = Time.unscaledTime - campaignStartTime;
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "oblation_research_metrics.jsonl");
            File.AppendAllText(path, JsonUtility.ToJson(campaignMetrics) + Environment.NewLine);
        }
        catch (IOException) { Debug.LogWarning("OBLATION: 연구 지표를 저장하지 못했습니다.", this); }
        catch (UnauthorizedAccessException) { Debug.LogWarning("OBLATION: 연구 지표를 저장하지 못했습니다.", this); }
    }

    void OnApplicationQuit() => FinishCampaignMetrics("abandoned");

    void ResetSystems()
    {
        biomass = 200;
        minerals = 160;
        neural = 100;
        offering = 0;
        combatUnits = 6;
        completedResearch.Clear();
        completedUpgrades.Clear();
        capturedTechnologies.Clear();
        unitInventory.Clear();
        unitInventory["scout"] = 6;
        activeResearch = null;
        researchRemaining = 0;
        lastResearchAxis = null;
        plagueOperations = siegeOperations = orbitalOperations = 0;
        for (int i = 0; i < planets.Count; i++)
        {
            Planet p = planets[i];
            p.antennaActive = i == 0 || i == 1;
            p.antennaHealth = p.antennaActive ? catalog.antenna.durability : 0;
            p.antennaRemaining = 0;
            p.scouted = i == 0 || i == 1;
            p.destroyed = false;
            p.queuedUnitId = null;
            p.unitRemaining = 0;
            p.unitTotalTime = 0;
            p.enemyUnitId = null;
            p.enemyProductionClock = 0;
            Array.Clear(p.traitLevels, 0, p.traitLevels.Length);
        }
    }

    bool HasResearch(string id) => completedResearch.Contains(id);

    float ModifierMultiplier(string statKey)
    {
        float result = 1f;
        var groups = new HashSet<string>();
        foreach (ResearchNodeSO node in catalog.research)
        {
            if (node == null || !HasResearch(node.id) || node.modifiers == null) continue;
            foreach (StatModifierDefinitionSO modifier in node.modifiers)
            {
                if (modifier == null || modifier.statKey != statKey || modifier.operation != "multiply" ||
                    !groups.Add(modifier.stackingGroup)) continue;
                result *= Mathf.Max(.1f, 1f + modifier.value);
            }
        }
        foreach (PlanetDefinitionSO planet in catalog.planets)
        {
            StatModifierDefinitionSO modifier = planet.captureTechnology;
            if (modifier == null || !capturedTechnologies.Contains(modifier.id) || modifier.statKey != statKey ||
                modifier.operation != "multiply" || !groups.Add(modifier.stackingGroup)) continue;
            result *= Mathf.Max(.1f, 1f + modifier.value);
        }
        return result;
    }

    bool CanAfford(OblationCost cost) => biomass >= cost.biomass && minerals >= cost.minerals &&
        neural >= cost.neural && offering >= cost.offering;

    bool TrySpend(OblationCost cost)
    {
        if (!CanAfford(cost)) return false;
        biomass -= cost.biomass;
        minerals -= cost.minerals;
        neural -= cost.neural;
        offering -= cost.offering;
        return true;
    }

    void AddResources(OblationCost reward)
    {
        float previousOffering = offering;
        biomass = Mathf.Min(ResourceCap(OblationResource.Biomass), biomass + reward.biomass);
        minerals = Mathf.Min(ResourceCap(OblationResource.Minerals), minerals + reward.minerals);
        neural = Mathf.Min(ResourceCap(OblationResource.Neural), neural + reward.neural);
        offering = Mathf.Min(ResourceCap(OblationResource.Offering), offering + reward.offering);
        if (campaignMetrics != null && campaignMetrics.firstOfferingSeconds < 0 && previousOffering <= 0 && offering > 0)
            campaignMetrics.firstOfferingSeconds = Time.unscaledTime - campaignStartTime;
    }

    float ResourceCap(OblationResource kind)
    {
        if (catalog.resources != null)
            foreach (ResourceDefinitionSO resource in catalog.resources)
                if (resource != null && resource.resource == kind) return resource.storageCap;
        return 999;
    }

    bool IsConnected(int index, Allegiance owner)
    {
        if (index < 0 || index >= planets.Count || planets[index].owner != owner || planets[index].destroyed) return false;
        int home = owner == Allegiance.Player ? 0 : 1;
        if (planets[home].owner != owner || !planets[home].antennaActive) return false;
        var reached = new bool[planets.Count];
        var queue = new Queue<int>();
        reached[home] = true;
        queue.Enqueue(home);
        while (queue.Count > 0)
        {
            int from = queue.Dequeue();
            if (!planets[from].antennaActive) continue;
            foreach (int next in planets[from].links)
            {
                if (reached[next] || planets[next].owner != owner || planets[next].destroyed) continue;
                reached[next] = true;
                queue.Enqueue(next);
            }
        }
        return reached[index];
    }

    bool HasAntennaInReach(int index, Allegiance owner)
    {
        foreach (int neighbor in planets[index].links)
            if (planets[neighbor].owner == owner && planets[neighbor].antennaActive && IsConnected(neighbor, owner)) return true;
        return false;
    }

    bool CanScout(int index)
    {
        float radius = 6.5f * (HasResearch("hive1") ? 1.25f : 1f);
        foreach (UnitUpgradeSO upgrade in catalog.unitUpgrades)
            if (upgrade != null && completedUpgrades.Contains(upgrade.id)) radius *= upgrade.detectionMultiplier;
        for (int i = 0; i < planets.Count; i++)
            if (planets[i].owner == Allegiance.Player && planets[i].antennaActive &&
                IsConnected(i, Allegiance.Player) &&
                Vector3.Distance(planets[i].position, planets[index].position) <= radius) return true;
        return false;
    }

    void TickSystems(float dt)
    {
        if (activeResearch != null)
        {
            researchRemaining -= dt * ResearchSpeed();
            if (researchRemaining <= 0) CompleteResearch();
        }
        for (int i = 0; i < planets.Count; i++)
        {
            Planet p = planets[i];
            if (p.antennaRemaining > 0)
            {
                p.antennaRemaining -= dt;
                if (p.antennaRemaining <= 0)
                {
                    p.antennaRemaining = 0;
                    p.antennaActive = true;
                    p.antennaHealth = catalog.antenna.durability;
                    if(p.owner==Allegiance.Player){signalPlanet=i;QueueStory(StoryChapter.Signal);}
                    AddLog(p.name + " 안테나가 연결되었습니다.");
                    if(visualDirector!=null)visualDirector.Event(p.position,OwnerColor(p.owner),OblationEffectKind.Antenna,7);
                }
            }
            if (string.IsNullOrEmpty(p.queuedUnitId) || p.owner != Allegiance.Player ||
                !IsConnected(i, Allegiance.Player) || IsTargeted(i)) continue;
            p.unitRemaining -= dt;
            if (p.unitRemaining > 0) continue;
            string unitId = p.queuedUnitId;
            p.queuedUnitId = null;
            p.unitRemaining = 0;
            unitInventory[unitId] = unitInventory.TryGetValue(unitId, out int count) ? count + 1 : 1;
            combatUnits = Mathf.Min(120, combatUnits + 1);
            AddLog(p.name + "에서 " + FindUnit(unitId).displayName + " 생산 완료.");
            if(visualDirector!=null)visualDirector.Event(p.position,PlayerColor,OblationEffectKind.Production,4);
        }
        if (campaignMetrics != null && campaignMetrics.firstExterminatusReadySeconds < 0)
            foreach (Planet p in planets)
                if (!p.destroyed && p.owner != Allegiance.Player)
                    foreach (ExterminatusDefinitionSO definition in catalog.exterminatus)
                        if (CanExterminatusAt(Array.IndexOf(catalog.planets, p.definition), definition))
                        {
                            campaignMetrics.firstExterminatusReadySeconds = Time.unscaledTime - campaignStartTime;
                            return;
                        }
    }

    void TickPlanetProduction(Planet p, int index)
    {
        if (!IsConnected(index, Allegiance.Player) || IsTargeted(index)) return;
        OblationCost rate = ProductionPerMinute(p);
        AddResources(new OblationCost(rate.biomass / 60f, rate.minerals / 60f, rate.neural / 60f));
    }

    void TickEnemyProduction(Planet p, int index)
    {
        if (!IsConnected(index, Allegiance.Enemy)) return;
        p.enemyProductionClock += 1f;
        if (p.enemyProductionClock >= 45f && string.IsNullOrEmpty(p.enemyUnitId))
        {
            p.enemyUnitId = catalog.units[2 + index % 2].id;
            AddLog(p.name + "에서 적 전용 병종 생산 시작.");
        }
        if (string.IsNullOrEmpty(p.enemyUnitId) || UnityEngine.Random.value > .01f) return;
        foreach (int neighbor in p.links)
        {
            Planet next = planets[neighbor];
            if (next.owner != Allegiance.Enemy || !string.IsNullOrEmpty(next.enemyUnitId)) continue;
            next.enemyUnitId = p.enemyUnitId;
            AddLog(next.name + "에 적 병종 보급 성공.");
            break;
        }
    }

    OblationCost ProductionPerMinute(Planet p)
    {
        float resourceLevel = p.traitLevels[(int)OblationTrait.Resource];
        float unitLevel = p.traitLevels[(int)OblationTrait.Unit];
        float researchLevel = p.traitLevels[(int)OblationTrait.Research];
        float resourceBoost = 1f + catalog.traits[(int)OblationTrait.Resource].primaryBonus * resourceLevel;
        float otherResourceMultiplier = Mathf.Pow(1f + catalog.traits[(int)OblationTrait.Resource].secondaryPenalty, resourceLevel);
        float mineralBoost = p.type == WorldType.Manufacturing ? 1.5f : 1f;
        float neuralBoost = p.type == WorldType.Energy ? 1.5f : 1f;
        mineralBoost *= ModifierMultiplier("minerals");
        return new OblationCost(
            p.definition.biomassPerMinute * p.definition.biomassEnvironmentMultiplier *
                (p.definition.focusResource == OblationResource.Biomass ? resourceBoost : otherResourceMultiplier) *
                Mathf.Pow(1f + catalog.traits[(int)OblationTrait.Unit].secondaryPenalty, unitLevel) * ModifierMultiplier("biomass"),
            p.definition.mineralsPerMinute * p.definition.mineralsEnvironmentMultiplier * mineralBoost *
                (p.definition.focusResource == OblationResource.Minerals ? resourceBoost : otherResourceMultiplier),
            p.definition.neuralPerMinute * p.definition.neuralEnvironmentMultiplier * neuralBoost *
                (p.definition.focusResource == OblationResource.Neural ? resourceBoost : otherResourceMultiplier) *
                (1f + catalog.traits[(int)OblationTrait.Research].primaryBonus * researchLevel) * ModifierMultiplier("neural"));
    }

    float ResearchSpeed()
    {
        float speed = 1f / ModifierMultiplier("researchTime");
        float traitTimeMultiplier = 1f;
        for (int i = 0; i < planets.Count; i++)
        {
            if (planets[i].owner == Allegiance.Player && planets[i].type == WorldType.Energy &&
                IsConnected(i, Allegiance.Player) && !IsTargeted(i))
                speed += .08f * planets[i].definition.researchSpeedMultiplier;
            if (planets[i].owner == Allegiance.Player && IsConnected(i, Allegiance.Player) && !IsTargeted(i))
                traitTimeMultiplier *= Mathf.Pow(catalog.traits[(int)OblationTrait.Research].timeMultiplierPerLevel,
                    planets[i].traitLevels[(int)OblationTrait.Research]);
        }
        return speed / Mathf.Max(.1f, traitTimeMultiplier);
    }

    ResearchNodeSO NextResearch(OblationResearchAxis axis)
    {
        if (catalog.research == null) return null;
        ResearchNodeSO next = null;
        foreach (ResearchNodeSO node in catalog.research)
            if (node != null && node.axis == axis && !HasResearch(node.id) &&
                (next == null || node.tier < next.tier)) next = node;
        return next;
    }

    ResearchNodeSO FindResearch(string id)
    {
        if (catalog.research != null)
            foreach (ResearchNodeSO node in catalog.research)
                if (node != null && node.id == id) return node;
        return null;
    }

    bool CanStartResearch(ResearchNodeSO node)
    {
        if (node == null || activeResearch != null || !CanAfford(ResearchCost(node)) || CountOwned(Allegiance.Player) < node.requiredPlanets)
            return false;
        if (!string.IsNullOrEmpty(node.requiredUnitId) && !unitInventory.ContainsKey(node.requiredUnitId)) return false;
        if (node.requiredAntennaCount > 0)
        {
            int antennaCount = 0;
            for (int i = 0; i < planets.Count; i++)
                if (planets[i].owner == Allegiance.Player && planets[i].antennaActive &&
                    IsConnected(i, Allegiance.Player)) antennaCount++;
            if (antennaCount < node.requiredAntennaCount) return false;
        }
        if (node.prerequisites != null)
            foreach (string prerequisite in node.prerequisites)
                if (!HasResearch(prerequisite)) return false;
        return true;
    }

    void TryStartResearch(OblationResearchAxis axis)
    {
        ResearchNodeSO node = NextResearch(axis);
        if (!CanStartResearch(node)) { AddLog("연구 선행 조건이나 자원이 부족합니다."); return; }
        if (!TrySpend(ResearchCost(node))) return;
        RecordResearchAction("research:" + node.id);
        activeResearch = node;
        researchRemaining = node.durationSeconds;
        if (campaignMetrics != null)
            campaignMetrics.research.Add(new ResearchMetric
            {
                researchId = node.id,
                selectedSeconds = Time.unscaledTime - campaignStartTime
            });
        AddLog(node.displayName + " 연구 시작.");
        PlayClick();
    }

    void CompleteResearch()
    {
        ResearchNodeSO finished = activeResearch;
        activeResearch = null;
        researchRemaining = 0;
        if (finished == null) return;
        completedResearch.Add(finished.id);
        if (campaignMetrics != null && finished.tier == 2 && campaignMetrics.firstTierTwoResearchSeconds < 0)
            campaignMetrics.firstTierTwoResearchSeconds = Time.unscaledTime - campaignStartTime;
        if (campaignMetrics != null)
            for (int i = campaignMetrics.research.Count - 1; i >= 0; i--)
                if (campaignMetrics.research[i].researchId == finished.id && campaignMetrics.research[i].completedSeconds < 0)
                {
                    campaignMetrics.research[i].completedSeconds = Time.unscaledTime - campaignStartTime;
                    break;
                }
        lastResearchAxis = finished.axis;
        techViral = HasResearch("combat1");
        techFoundry = HasResearch("combat2");
        techOrbital = HasResearch("combat3");
        AddLog(finished.displayName + " 연구 완료.");
        if(visualDirector!=null)visualDirector.Event(planets[0].position,new Color(.65f,.35f,1),OblationEffectKind.Research,7);
        sfxSource.PlayOneShot(captureClip, sfxVolume);
    }

    UnitDefinitionSO FindUnit(string id)
    {
        if (catalog.units != null)
            foreach (UnitDefinitionSO unit in catalog.units)
                if (unit != null && unit.id == id) return unit;
        return null;
    }

    OblationCost ResearchCost(ResearchNodeSO node)
    {
        float multiplier = lastResearchAxis == node.axis ? 1.15f : 1f;
        return new OblationCost(node.cost.biomass * multiplier, node.cost.minerals * multiplier,
            node.cost.neural * multiplier, node.cost.offering * multiplier);
    }

    void TryUpgradeUnit(int upgradeIndex)
    {
        if (catalog.unitUpgrades == null || upgradeIndex < 0 || upgradeIndex >= catalog.unitUpgrades.Length) return;
        UnitUpgradeSO upgrade = catalog.unitUpgrades[upgradeIndex];
        if (upgrade == null || completedUpgrades.Contains(upgrade.id) ||
            !unitInventory.TryGetValue(upgrade.unitId, out int count) || count < 1 || !TrySpend(upgrade.cost))
        {
            AddLog("유닛 강화 조건이나 자원이 부족합니다.");
            return;
        }
        completedUpgrades.Add(upgrade.id);
        RecordResearchAction("upgrade:" + upgrade.id);
        AddLog(upgrade.displayName + " 적용 완료.");
        PlayClick();
    }

    bool CanUpgradeUnit(int upgradeIndex)
    {
        if (catalog.unitUpgrades == null || upgradeIndex < 0 || upgradeIndex >= catalog.unitUpgrades.Length) return false;
        UnitUpgradeSO upgrade = catalog.unitUpgrades[upgradeIndex];
        return upgrade != null && !completedUpgrades.Contains(upgrade.id) &&
            unitInventory.TryGetValue(upgrade.unitId, out int count) && count > 0 && CanAfford(upgrade.cost);
    }

    float UnitAttackBonus(AttackMode mode)
    {
        float bonus = 1f;
        float roleBonus = 0;
        foreach (KeyValuePair<string, int> entry in unitInventory)
        {
            UnitDefinitionSO unit = FindUnit(entry.Key);
            if (unit == null || entry.Value < 1) continue;
            bool matched = UnitMatchesMode(unit, mode);
            float effectiveAttack = unit.attack;
            foreach (UnitUpgradeSO upgrade in catalog.unitUpgrades)
                if (upgrade != null && upgrade.unitId == unit.id && completedUpgrades.Contains(upgrade.id))
                    effectiveAttack *= upgrade.attackMultiplier;
            if (matched) roleBonus += effectiveAttack * entry.Value / 1000f;
        }
        bonus += Mathf.Min(.4f, roleBonus);
        return bonus;
    }

    bool UnitMatchesMode(UnitDefinitionSO unit, AttackMode mode)
    {
        bool matched = mode == AttackMode.Surprise ? unit.attackType == "질병" || unit.attackType == "정신" :
            mode == AttackMode.Assault ? unit.attackType == "무력" || unit.attackType == "선제" :
            unit.role.Contains("공성") || unit.largePlanetOnly;
        for (int i = 2; i < catalog.unitUpgrades.Length; i += 3)
        {
            UnitUpgradeSO conversion = catalog.unitUpgrades[i];
            if (conversion == null || conversion.unitId != unit.id || !completedUpgrades.Contains(conversion.id)) continue;
            string tag = conversion.roleTag;
            matched |= mode == AttackMode.Surprise ? tag == "질병" || tag == "정신" || tag == "지배" :
                mode == AttackMode.Assault ? tag == "방어" || tag == "건물" :
                tag == "공성" || tag == "건물" || tag == "궤도";
        }
        return matched;
    }

    float UnitTravelMultiplier(AttackMode mode)
    {
        float multiplier = 1f;
        foreach (UnitUpgradeSO upgrade in catalog.unitUpgrades)
        {
            if (upgrade == null || !completedUpgrades.Contains(upgrade.id) ||
                !unitInventory.TryGetValue(upgrade.unitId, out int count) || count < 1 ||
                !UnitMatchesMode(FindUnit(upgrade.unitId), mode)) continue;
            multiplier *= 1f / Mathf.Max(.5f, upgrade.speedMultiplier);
        }
        return Mathf.Clamp(multiplier, .7f, 1.3f);
    }

    float UnitArmorPenetration(AttackMode mode)
    {
        float penetration = 0;
        foreach (UnitUpgradeSO upgrade in catalog.unitUpgrades)
            if (upgrade != null && completedUpgrades.Contains(upgrade.id) &&
                unitInventory.TryGetValue(upgrade.unitId, out int count) && count > 0 &&
                UnitMatchesMode(FindUnit(upgrade.unitId), mode)) penetration += upgrade.armorPenetration;
        return Mathf.Min(.4f, penetration);
    }

    float UnitSurvivalRefund(AttackMode mode)
    {
        float refund = 0;
        foreach (UnitUpgradeSO upgrade in catalog.unitUpgrades)
            if (upgrade != null && completedUpgrades.Contains(upgrade.id) &&
                unitInventory.TryGetValue(upgrade.unitId, out int count) && count > 0 &&
                UnitMatchesMode(FindUnit(upgrade.unitId), mode))
                refund += Mathf.Max(0, upgrade.healthMultiplier - 1f) * .5f;
        return Mathf.Min(.35f, refund);
    }

    bool CanProduceUnit(UnitDefinitionSO unit, Planet p, int index)
    {
        return unit != null && p.owner == Allegiance.Player && !p.destroyed &&
            p.type == WorldType.Unit && IsConnected(index, Allegiance.Player) && !IsTargeted(index) &&
            string.IsNullOrEmpty(p.queuedUnitId) && (!unit.largePlanetOnly || p.definition.largeSpecialPlanet) &&
            (string.IsNullOrEmpty(unit.requiredResearchId) || HasResearch(unit.requiredResearchId)) && CanAfford(unit.productionCost);
    }

    void TryQueueUnit(int unitIndex)
    {
        if (selectedPlanet < 0 || catalog.units == null || unitIndex < 0 || unitIndex >= catalog.units.Length) return;
        Planet p = planets[selectedPlanet];
        UnitDefinitionSO unit = catalog.units[unitIndex];
        if (!CanProduceUnit(unit, p, selectedPlanet)) { AddLog("유닛 생산 조건이 충족되지 않았습니다."); return; }
        if (!TrySpend(unit.productionCost)) return;
        p.queuedUnitId = unit.id;
        RecordResearchAction("unit:" + unit.id);
        p.unitRemaining = unit.productionSeconds * p.definition.unitProductionMultiplier *
            p.definition.unitTimeEnvironmentMultiplier *
            ModifierMultiplier("unitTime") * Mathf.Pow(catalog.traits[(int)OblationTrait.Unit].timeMultiplierPerLevel,
                p.traitLevels[(int)OblationTrait.Unit]);
        p.unitTotalTime = p.unitRemaining;
        AddLog(p.name + "에서 " + unit.displayName + " 생산 시작.");
        PlayClick();
    }

    void TryInstallAntenna()
    {
        if (selectedPlanet < 0 || catalog.antenna == null) return;
        Planet p = planets[selectedPlanet];
        if (p.owner != Allegiance.Player || p.destroyed || p.antennaActive || p.antennaRemaining > 0 ||
            (selectedPlanet != 0 && !IsConnected(selectedPlanet, Allegiance.Player)) ||
            !TrySpend(catalog.antenna.installationCost))
        {
            AddLog("안테나 설치 조건이나 광물이 부족합니다.");
            return;
        }
        p.antennaRemaining = catalog.antenna.installationSeconds;
        if (campaignMetrics != null && campaignMetrics.firstAntennaSeconds < 0)
            campaignMetrics.firstAntennaSeconds = Time.unscaledTime - campaignStartTime;
        RecordResearchAction("antenna:" + p.definition.id);
        AddLog(p.name + " 안테나 설치 시작.");
        PlayClick();
    }

    bool CanInstallAntenna()
    {
        if (selectedPlanet < 0 || catalog.antenna == null) return false;
        Planet p = planets[selectedPlanet];
        return p.owner == Allegiance.Player && !p.destroyed && !p.antennaActive && p.antennaRemaining <= 0 &&
            (selectedPlanet == 0 || IsConnected(selectedPlanet, Allegiance.Player)) &&
            CanAfford(catalog.antenna.installationCost);
    }

    void TryUpgradeTrait(OblationTrait kind)
    {
        if (selectedPlanet < 0 || catalog.traits == null || (int)kind >= catalog.traits.Length) return;
        Planet p = planets[selectedPlanet];
        PlanetTraitSO trait = catalog.traits[(int)kind];
        int level = p.traitLevels[(int)kind];
        int activeCategories = 0;
        foreach (int value in p.traitLevels) if (value > 0) activeCategories++;
        if (p.owner != Allegiance.Player || p.destroyed || trait == null || level >= trait.maximumLevel ||
            (level == 0 && activeCategories >= p.definition.traitSlots)) return;
        float multiplier = Mathf.Pow(1.25f, level);
        OblationCost cost = new OblationCost(trait.firstUpgradeCost.biomass * multiplier,
            trait.firstUpgradeCost.minerals * multiplier, trait.firstUpgradeCost.neural * multiplier);
        if (!TrySpend(cost)) { AddLog("특화에 필요한 자원이 부족합니다."); return; }
        p.traitLevels[(int)kind]++;
        RecordResearchAction("trait:" + kind);
        AddLog(p.name + " " + trait.displayName + " " + p.traitLevels[(int)kind] + "단계.");
        PlayClick();
    }

    ExterminatusDefinitionSO FindExterminatus(OblationExterminatus kind)
    {
        if (catalog.exterminatus != null)
            foreach (ExterminatusDefinitionSO definition in catalog.exterminatus)
                if (definition != null && definition.kind == kind) return definition;
        return null;
    }

    void TryExterminatus(OblationExterminatus kind)
    {
        if (selectedPlanet < 0) return;
        Planet p = planets[selectedPlanet];
        ExterminatusDefinitionSO definition = FindExterminatus(kind);
        if (!CanExterminatus(kind))
        {
            AddLog("익스터미나투스의 연구·공물·정찰·안테나 조건이 부족합니다.");
            return;
        }
        TrySpend(definition.activationCost);
        RecordResearchAction("exterminatus:" + definition.id);
        p.population = 0;
        p.owner = Allegiance.Neutral;
        p.destroyed = true;
        p.playerClaim = p.enemyClaim = 0;
        p.antennaActive = false;
        p.queuedUnitId = null;
        SpawnShockwave(p.position, PlayerColor);
        AddLog(p.name + " 절멸 완료: " + definition.displayName + ". " + definition.sideEffect);
        if (kind == OblationExterminatus.Plague) biomass = Mathf.Max(0, biomass - 20);
        if (kind == OblationExterminatus.Siege) minerals = Mathf.Min(ResourceCap(OblationResource.Minerals), minerals + 15);
        if (kind == OblationExterminatus.Orbital) neural = Mathf.Max(0, neural - 15);
        UpdateAllVisuals();
        PlayClick();
    }

    bool CanExterminatus(OblationExterminatus kind)
    {
        if (selectedPlanet < 0) return false;
        return CanExterminatusAt(selectedPlanet, FindExterminatus(kind));
    }

    bool CanExterminatusAt(int index, ExterminatusDefinitionSO definition)
    {
        if (index < 0 || index >= planets.Count || definition == null) return false;
        Planet p = planets[index];
        return p.owner != Allegiance.Player && !p.destroyed && !IsTargeted(index) &&
            HasResearch(definition.requiredResearchId) && (!definition.requiresScouting || p.scouted) &&
            (!definition.requiresActiveAntenna || HasAntennaInReach(index, Allegiance.Player)) &&
            CanAfford(definition.activationCost);
    }

    void RecordEnemyAdaptation(AttackMode mode)
    {
        if (mode == AttackMode.Surprise && ++plagueOperations == 2) AddLog("적이 역병 격리망을 개발했습니다.");
        if (mode == AttackMode.Assault && ++siegeOperations == 2) AddLog("적이 요새화를 시작했습니다.");
        if (mode == AttackMode.Orbital && ++orbitalOperations == 2) AddLog("적이 지하 방호시설을 구축했습니다.");
    }

    float CounterMultiplier(AttackMode mode)
    {
        return mode == AttackMode.Surprise && plagueOperations >= 2 ? .8f :
            mode == AttackMode.Assault && siegeOperations >= 2 ? .85f :
            mode == AttackMode.Orbital && orbitalOperations >= 2 ? .82f : 1f;
    }

    float EncounterMultiplier(Planet target, AttackMode mode)
    {
        if (target.definition.encounter == null || target.definition.encounter.counterTags == null) return 1f;
        foreach (string tag in target.definition.encounter.counterTags)
        {
            if (mode == AttackMode.Surprise && tag == "vaccine") return .85f;
            if (mode == AttackMode.Assault && tag == "fortification") return .85f;
            if (mode == AttackMode.Orbital && tag == "underground") return .85f;
        }
        return 1f;
    }

    static string CounterText(string tag) => tag == "vaccine" ? "백신 격리망" :
        tag == "fortification" ? "요새화" : tag == "underground" ? "지하화" : tag;
}
