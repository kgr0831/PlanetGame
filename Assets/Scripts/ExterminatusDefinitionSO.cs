public sealed class ExterminatusDefinitionSO : OblationDefinitionSO
{
    public OblationExterminatus kind;
    public string requiredResearchId;
    public OblationCost activationCost;
    public bool requiresScouting = true;
    public bool requiresActiveAntenna = true;
    public string targetCondition;
    public string sideEffect;
    public string presentationKey;
}
