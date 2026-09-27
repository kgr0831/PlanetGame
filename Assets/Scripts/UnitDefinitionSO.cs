public sealed class UnitDefinitionSO : OblationDefinitionSO
{
    public string role;
    public OblationCost productionCost;
    public float productionSeconds;
    public float health;
    public float attack;
    public float movementSpeed;
    public string attackType;
    public string requiredResearchId;
    public bool largePlanetOnly;
    public int tier = 1;
}
