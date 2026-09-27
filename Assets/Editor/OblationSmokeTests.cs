using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class OblationSmokeTests
{
    [Test]
    public void Project_UsesUrpAtEveryQualityLevel()
    {
        Assert.That(GraphicsSettings.defaultRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
        Assert.That(GraphicsSettings.currentRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            RenderPipelineAsset configured = QualitySettings.GetRenderPipelineAssetAt(level) ??
                GraphicsSettings.defaultRenderPipeline;
            Assert.That(configured, Is.InstanceOf<UniversalRenderPipelineAsset>(), QualitySettings.names[level]);
        }
    }

    [Test]
    public void GalaxyTopology_IsFullyConnected()
    {
        Assert.That(OblationGame.TopologyIsConnected(), Is.True);
        Assert.That(OblationGame.StartingRoutesSpanFourDirections(), Is.True);
    }

    [Test]
    public void FirstActions_DoNotRequireLongIdleWaits()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<OblationCatalogSO>("Assets/Data/OblationCatalog.asset");
        Assert.That(catalog.units[0].productionSeconds, Is.InRange(1,10));
        Assert.That(catalog.research[0].durationSeconds, Is.InRange(1,30));
        Assert.That(catalog.antenna.installationSeconds, Is.InRange(1,10));
        Assert.That(catalog.antenna.installationCost.minerals, Is.LessThanOrEqualTo(80));
    }

    [Test]
    public void ExperienceShaders_HaveNoCompilerErrors()
    {
        foreach (string name in new[] { "OblationPlanet.shader", "OblationClouds.shader", "OblationPlanetRing.shader", "OblationHalo.shader", "OblationLine.shader", "OblationParticle.shader", "OblationStarfield.shader", "OblationHologram.shader", "OblationEnergyPulse.shader", "OblationActionFx.shader", "OblationAtmosphere.shadergraph" })
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/" + name);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True, name);
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Assert.That(message.severity.ToString(), Is.Not.EqualTo("Error"), name + ": " + message.message);
        }
    }

    [Test]
    public void NotionCatalog_HasValidIdsAndReferences()
    {
        OblationCatalogSO catalog = AssetDatabase.LoadAssetAtPath<OblationCatalogSO>("Assets/Data/OblationCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.planets, Has.Length.EqualTo(OblationGalaxyLayout.Names.Length));
        Assert.That(catalog.resources, Has.Length.EqualTo(4));
        Assert.That(catalog.units, Has.Length.EqualTo(8));
        Assert.That(catalog.unitUpgrades, Has.Length.EqualTo(24));
        Assert.That(catalog.research, Has.Length.EqualTo(9));
        Assert.That(catalog.exterminatus, Has.Length.EqualTo(3));
        Assert.That(catalog.traits[(int)OblationTrait.Unit].timeMultiplierPerLevel, Is.InRange(.1f, 1f));
        Assert.That(catalog.traits[(int)OblationTrait.Research].timeMultiplierPerLevel, Is.InRange(.1f, 1f));
        var ids = new HashSet<string>();
        foreach (OblationDefinitionSO[] group in new OblationDefinitionSO[][]
        {
            catalog.planets, catalog.traits, catalog.resources, catalog.units, catalog.unitUpgrades,
            catalog.research, catalog.researchTrees, catalog.exterminatus, catalog.encounters, catalog.modifiers
        })
            foreach (OblationDefinitionSO item in group)
            {
                Assert.That(item, Is.Not.Null);
                Assert.That(item.id, Is.Not.Empty);
                Assert.That(ids.Add(item.id), Is.True, item.id + " ID is duplicated.");
            }
        Assert.That(catalog.antenna, Is.Not.Null);
        Assert.That(catalog.conquestRule, Is.Not.Null);
        Assert.That(catalog.conquestRule.stageNames, Has.Length.EqualTo(3));
        Assert.That(catalog.conquestRule.stageThresholds, Has.Length.EqualTo(3));
        Assert.That(catalog.conquestRule.stageThresholds[0], Is.LessThan(catalog.conquestRule.stageThresholds[1]));
        Assert.That(catalog.conquestRule.stageThresholds[1], Is.LessThan(catalog.conquestRule.stageThresholds[2]));
        Assert.That(ids.Add(catalog.antenna.id), Is.True);
        Assert.That(ids.Add(catalog.conquestRule.id), Is.True);
        foreach (ResearchNodeSO node in catalog.research)
        {
            Assert.That(node.durationSeconds, Is.GreaterThan(0));
            foreach (string prerequisite in node.prerequisites)
                Assert.That(ids.Contains(prerequisite), Is.True, node.id + " prerequisite is missing.");
            if (!string.IsNullOrEmpty(node.requiredUnitId)) Assert.That(ids.Contains(node.requiredUnitId), Is.True);
            if (node.unlockUnitIds != null)
                foreach (string unitId in node.unlockUnitIds) Assert.That(ids.Contains(unitId), Is.True);
            if (node.unlockExterminatusIds != null)
                foreach (string endingId in node.unlockExterminatusIds) Assert.That(ids.Contains(endingId), Is.True);
        }
        var researchById = new Dictionary<string, ResearchNodeSO>();
        foreach (ResearchNodeSO node in catalog.research) researchById.Add(node.id, node);
        var completed = new HashSet<string>();
        var visiting = new HashSet<string>();
        foreach (ResearchNodeSO node in catalog.research)
            Assert.That(HasAcyclicPrerequisites(node, researchById, visiting, completed), Is.True,
                node.id + " has a missing or cyclic research prerequisite.");
        foreach (UnitDefinitionSO unit in catalog.units)
            if (!string.IsNullOrEmpty(unit.requiredResearchId)) Assert.That(ids.Contains(unit.requiredResearchId), Is.True);
        foreach (UnitUpgradeSO upgrade in catalog.unitUpgrades)
            Assert.That(ids.Contains(upgrade.unitId), Is.True);
        foreach (UnitDefinitionSO unit in catalog.units)
        {
            int count = 0;
            foreach (UnitUpgradeSO upgrade in catalog.unitUpgrades)
                if (upgrade.unitId == unit.id) count++;
            Assert.That(count, Is.EqualTo(3), unit.id + " must have two upgrades and one conversion.");
        }
        foreach (ExterminatusDefinitionSO ending in catalog.exterminatus)
        {
            Assert.That(ids.Contains(ending.requiredResearchId), Is.True);
            Assert.That(ending.activationCost.offering, Is.GreaterThan(0));
        }
        foreach (PlanetDefinitionSO planet in catalog.planets)
        {
            Assert.That(planet.encounter, Is.Not.Null);
            Assert.That(planet.captureTechnology, Is.Not.Null);
            Assert.That(planet.interiorPreview, Is.Not.Null);
            Assert.That(ids.Contains(planet.captureTechnology.id), Is.True);
            Assert.That((int)planet.focusResource, Is.InRange(0, 2));
            Assert.That(planet.biomassEnvironmentMultiplier, Is.GreaterThan(0));
            Assert.That(planet.mineralsEnvironmentMultiplier, Is.GreaterThan(0));
            Assert.That(planet.neuralEnvironmentMultiplier, Is.GreaterThan(0));
            Assert.That(planet.unitTimeEnvironmentMultiplier, Is.GreaterThan(0));
            Assert.That(planet.diseaseTimeEnvironmentMultiplier, Is.GreaterThan(0));
            foreach (string uniqueUnitId in planet.uniqueUnitIds)
            {
                Assert.That(ids.Contains(uniqueUnitId), Is.True);
                Assert.That(planet.largeSpecialPlanet, Is.True);
            }
        }
    }

    static bool HasAcyclicPrerequisites(ResearchNodeSO node, Dictionary<string, ResearchNodeSO> byId,
        HashSet<string> visiting, HashSet<string> completed)
    {
        if (completed.Contains(node.id)) return true;
        if (!visiting.Add(node.id)) return false;
        foreach (string prerequisite in node.prerequisites)
            if (!byId.TryGetValue(prerequisite, out ResearchNodeSO dependency) ||
                !HasAcyclicPrerequisites(dependency, byId, visiting, completed)) return false;
        visiting.Remove(node.id);
        completed.Add(node.id);
        return true;
    }

    [Test]
    public void SampleScene_UsesInspectorHierarchy()
    {
        const string path = "Assets/Scenes/SampleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest) scene = EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            GameObject root = null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.name == "Root") root = candidate;
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.Find("GameUI"), Is.Not.Null);
            Assert.That(root.transform.Find("HudUI"), Is.Not.Null);
            Transform util = root.transform.Find("Util");
            Assert.That(util, Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<OblationPlanetView>(true).Length, Is.EqualTo(OblationGalaxyLayout.Names.Length));
            Assert.That(util.Find("World/Routes").childCount, Is.EqualTo(OblationGalaxyLayout.Edges.GetLength(0)));
            Assert.That(util.Find("World/Background/Hungry Star"), Is.Null);
            Transform planets = util.Find("World/Planets");
            Assert.That(planets.Find("VESPER").localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(planets.Find("KHEPRI").localPosition.z, Is.LessThan(0));
            Assert.That(planets.Find("MORROW").localPosition.x, Is.GreaterThan(0));
            Assert.That(planets.Find("ORISON").localPosition.z, Is.GreaterThan(0));
            Assert.That(planets.Find("HALCYON").localPosition.x, Is.LessThan(0));

            OblationGame game = util.Find("Runtime/GameManager").GetComponent<OblationGame>();
            Assert.That(game, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(game);
            Assert.That(serialized.FindProperty("planetViews").arraySize, Is.EqualTo(OblationGalaxyLayout.Names.Length));
            Assert.That(serialized.FindProperty("routeViews").arraySize, Is.EqualTo(OblationGalaxyLayout.Edges.GetLength(0)));
            Assert.That(serialized.FindProperty("startButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("gameCamera").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("focusUi").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("planetInteriorImage").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("resourceValues").arraySize, Is.EqualTo(5));
            Assert.That(serialized.FindProperty("tutorialRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("cinematicCanvas").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("cinematicTargetButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("cinematicReticle").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("operationTabs").arraySize, Is.EqualTo(3));
            Assert.That(serialized.FindProperty("resourceDetailsButton").objectReferenceValue, Is.Not.Null);
            foreach (Text text in ((GameObject)serialized.FindProperty("tutorialRoot").objectReferenceValue).GetComponentsInChildren<Text>(true))
                Assert.That(text.raycastTarget, Is.False);
            Assert.That(serialized.FindProperty("quickProduceButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("reducedMotionButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("impactMaterial").objectReferenceValue, Is.Not.Null);
            var director=(OblationVisualDirector)serialized.FindProperty("visualDirector").objectReferenceValue;
            Assert.That(director, Is.Not.Null);
            Assert.That(director.pulseMaterial, Is.Not.Null);
            Assert.That(director.actionMaterial, Is.Not.Null);
            Assert.That(serialized.FindProperty("storyCanvas").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("mapOverviewButton").objectReferenceValue, Is.Not.Null);
            for (int i=0;i<OblationGalaxyLayout.Names.Length;i++) Assert.That(planets.Find(OblationGalaxyLayout.Names[i]).position, Is.EqualTo(OblationGalaxyLayout.Positions[i]));
            Font silver=AssetDatabase.LoadAssetAtPath<Font>("Assets/Silver.ttf");
            Assert.That(serialized.FindProperty("interfaceFont").objectReferenceValue, Is.EqualTo(silver));
            foreach(Text label in root.GetComponentsInChildren<Text>(true))Assert.That(label.font, Is.EqualTo(silver),label.name);
            Assert.That(game.GetComponent<OblationUiAudio>().source, Is.Not.Null);
            var surfaceFamilies=new HashSet<int>();int ringCount=0;
            foreach(var view in root.GetComponentsInChildren<OblationPlanetView>(true))
            {
                surfaceFamilies.Add(Mathf.RoundToInt(view.bodyRenderer.sharedMaterial.GetFloat("_SurfaceType")));
                if(view.ringRenderer!=null){ringCount++;Assert.That(view.ringRenderer.transform.IsChildOf(view.transform), Is.True);}
                Assert.That(view.atmosphereRenderer, Is.Not.Null);
                Assert.That(view.atmosphereRenderer.sharedMaterial.HasProperty("_RimPower"), Is.True);
                Assert.That(view.atmosphereRenderer.transform.IsChildOf(view.transform), Is.True);
                Assert.That(view.cloudRenderer, Is.Not.Null);
                Assert.That(view.cloudRenderer.transform.IsChildOf(view.bodyRenderer.transform), Is.True);
                Assert.That(view.cloudRenderer.sharedMaterial.shader.name, Is.EqualTo("Oblation/Planet Clouds"));
            }
            Assert.That(surfaceFamilies.Count, Is.EqualTo(8));Assert.That(ringCount, Is.GreaterThanOrEqualTo(6));
            var volume = (Volume)serialized.FindProperty("gameplayVolume").objectReferenceValue;
            Assert.That(volume.sharedProfile.TryGet(out Bloom bloom), Is.True);
            Assert.That(bloom.intensity.value, Is.GreaterThan(.4f));
            var camera = (Camera)serialized.FindProperty("gameCamera").objectReferenceValue;
            Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);
            Assert.That(camera.GetUniversalAdditionalCameraData().antialiasing, Is.Not.EqualTo(AntialiasingMode.None));
            Assert.That(serialized.FindProperty("catalog").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("operationsModal").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("unitButtons").arraySize, Is.EqualTo(8));
            Assert.That(serialized.FindProperty("upgradeButtons").arraySize, Is.EqualTo(6));
            Assert.That(serialized.FindProperty("traitButtons").arraySize, Is.EqualTo(3));
            Assert.That(serialized.FindProperty("exterminatusButtons").arraySize, Is.EqualTo(3));
            Transform assignment = root.transform.Find("GameUI/GameCanvas/AssignmentModal/Card");
            Assert.That(assignment.Find("ExtractorButton/Label").GetComponent<Text>().text, Does.Contain("유닛 특화"));
            Assert.That(assignment.Find("ForgeButton/Label").GetComponent<Text>().text, Does.Contain("자원 특화"));
            Assert.That(assignment.Find("PsionicButton/Label").GetComponent<Text>().text, Does.Contain("연구 특화"));
            Material halo = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Oblation/HaloTemplate.mat");
            Assert.That(halo, Is.Not.Null);
            Assert.That(halo.HasProperty("_ContestedProgress"), Is.True);
        }
        finally
        {
            if (openedForTest) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
