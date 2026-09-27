public sealed class PlanetTraitSO : OblationDefinitionSO
{
    public OblationTrait category;
    public int maximumLevel = 10;
    public OblationCost firstUpgradeCost;
    public float primaryBonus;
    public float secondaryPenalty;
    public float timeMultiplierPerLevel = 1f;
    public string visualKeyword;
}
