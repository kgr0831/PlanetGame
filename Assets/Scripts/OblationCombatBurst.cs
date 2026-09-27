using UnityEngine;

public sealed class OblationCombatBurst : MonoBehaviour
{
    Light impactLight;
    float age;
    float peak;

    public static void Spawn(Transform parent, Vector3 position, Color color, Material material, int count, bool reduced)
    {
        var root = new GameObject("전투 충돌 효과");
        root.transform.SetParent(parent, false);
        root.transform.position = position + Vector3.up * 1.1f;
        var effect = root.AddComponent<OblationCombatBurst>();
        var particles = root.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false; main.playOnAwake = false; main.duration = .75f;
        main.startLifetime = .9f; main.startSpeed = 0; main.maxParticles = 128;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.enabled = false;
        var colorOverLife = particles.colorOverLifetime;
        colorOverLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(color, 1) },
            new[] { new GradientAlphaKey(.85f, 0), new GradientAlphaKey(0, 1) });
        colorOverLife.color = gradient;
        var size = particles.sizeOverLifetime;
        size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
        var renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.3f; renderer.velocityScale = .12f;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        particles.Play();
        for (int i = 0; i < Mathf.Min(128,reduced?count:count*2); i++)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position + Random.onUnitSphere * .8f,
                velocity = Random.onUnitSphere * Random.Range(reduced ? .3f : 1.5f, reduced ? 1f : 5.5f),
                startSize = Random.Range(.05f, .16f), startLifetime = Random.Range(.45f, 1.25f), startColor = Color.Lerp(color,Color.white,.25f)
            };
            particles.Emit(emit, 1);
        }
        effect.impactLight = root.AddComponent<Light>();
        effect.impactLight.type = LightType.Point; effect.impactLight.color = color;
        effect.impactLight.range = 8; effect.impactLight.shadows = LightShadows.None;
        effect.peak = reduced ? .3f : 5f;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        impactLight.intensity = peak * Mathf.Pow(Mathf.Clamp01(1 - age / .65f), 2);
        if (age >= 1.5f) Destroy(gameObject);
    }
}
