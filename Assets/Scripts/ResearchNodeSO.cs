public sealed class ResearchNodeSO : OblationDefinitionSO
{
    public OblationResearchAxis axis;
    public int tier;
    public OblationCost cost;
    public float durationSeconds;
    public string[] prerequisites;
    public string requiredUnitId;
    public int requiredAntennaCount;
    public int requiredPlanets;
    public string[] unlockUnitIds;
    public string[] unlockExterminatusIds;
    public StatModifierDefinitionSO[] modifiers;
    public string counterTag;
    public string description;
}
