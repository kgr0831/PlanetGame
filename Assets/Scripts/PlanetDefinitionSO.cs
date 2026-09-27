using UnityEngine;

public sealed class PlanetDefinitionSO : OblationDefinitionSO
{
    public string traitDescription;
    public float maxPopulation = 80;
    public float defense = 20;
    public float biomassPerMinute = 6;
    public float mineralsPerMinute = 4;
    public float neuralPerMinute = 2;
    public OblationResource focusResource;
    public float biomassEnvironmentMultiplier = 1;
    public float mineralsEnvironmentMultiplier = 1;
    public float neuralEnvironmentMultiplier = 1;
    public float unitTimeEnvironmentMultiplier = 1;
    public float diseaseTimeEnvironmentMultiplier = 1;
    public float attackArrivalMultiplier = 1;
    public float unitProductionMultiplier = 1;
    public float researchSpeedMultiplier = 1;
    public int traitSlots = 2;
    public bool largeSpecialPlanet;
    public string[] uniqueUnitIds;
    public EncounterDefinitionSO encounter;
    public StatModifierDefinitionSO captureTechnology;
    public Texture2D interiorPreview;
}
