public sealed class EncounterDefinitionSO : OblationDefinitionSO
{
    public int threatLevel;
    public string[] publicTraitIds;
    public string[] defenseGoals;
    public string[] counterTags;
    public string[] eventIds;
    public int conquestStages = 3;
    public OblationCost completionReward;
}
