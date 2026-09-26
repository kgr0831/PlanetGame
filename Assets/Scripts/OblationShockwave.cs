using UnityEngine;

public sealed class OblationShockwave : MonoBehaviour
{
    float age;

    void Update()
    {
        age += Time.deltaTime;
        transform.localScale = Vector3.one * Mathf.Lerp(.4f, 5f, age / 1.2f);
        if (age >= 1.2f) Destroy(gameObject);
    }
}
