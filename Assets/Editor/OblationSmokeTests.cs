using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class OblationSmokeTests
{
    [Test]
    public void GalaxyTopology_IsFullyConnected()
    {
        Assert.That(OblationGame.TopologyIsConnected(), Is.True);
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
            Assert.That(root.GetComponentsInChildren<OblationPlanetView>(true).Length, Is.EqualTo(12));
            Assert.That(util.Find("World/Routes").childCount, Is.EqualTo(21));

            OblationGame game = util.Find("Runtime/GameManager").GetComponent<OblationGame>();
            Assert.That(game, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(game);
            Assert.That(serialized.FindProperty("planetViews").arraySize, Is.EqualTo(12));
            Assert.That(serialized.FindProperty("routeViews").arraySize, Is.EqualTo(21));
            Assert.That(serialized.FindProperty("startButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("gameCamera").objectReferenceValue, Is.Not.Null);
            Transform assignment = root.transform.Find("GameUI/GameCanvas/AssignmentModal/Card");
            Assert.That(assignment.Find("ExtractorButton/Label").GetComponent<Text>().text, Does.Contain("유닛 생산 행성"));
            Assert.That(assignment.Find("ForgeButton/Label").GetComponent<Text>().text, Does.Contain("제조 행성"));
            Assert.That(assignment.Find("PsionicButton/Label").GetComponent<Text>().text, Does.Contain("에너지 생산 행성"));
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
