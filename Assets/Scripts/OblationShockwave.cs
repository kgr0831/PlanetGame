using UnityEngine;

public sealed class OblationShockwave : MonoBehaviour
{
    float age;
    Material ownedMaterial;
    bool reduced;

    public void Initialize(Material material, bool reduce)
    { ownedMaterial = material; reduced = reduce; }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(age / .9f);
        transform.localScale = Vector3.one * Mathf.Lerp(reduced ? 1.2f : .5f, reduced ? 2 : 4.5f, 1 - Mathf.Pow(1 - progress, 2));
        if (ownedMaterial != null) ownedMaterial.SetFloat("_Opacity", (1 - progress) * (reduced ? .2f : .65f));
        if (progress >= 1) Destroy(gameObject);
    }

    void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); }
}
