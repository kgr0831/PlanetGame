using UnityEngine;

public sealed class OblationCatalogSO : ScriptableObject
{
    public PlanetDefinitionSO[] planets;
    public PlanetTraitSO[] traits;
    public ResourceDefinitionSO[] resources;
    public UnitDefinitionSO[] units;
    public UnitUpgradeSO[] unitUpgrades;
    public ResearchNodeSO[] research;
    public ResearchTreeSO[] researchTrees;
    public AntennaDefinitionSO antenna;
    public ExterminatusDefinitionSO[] exterminatus;
    public EncounterDefinitionSO[] encounters;
    public ConquestRuleSO conquestRule;
    public StatModifierDefinitionSO[] modifiers;
}
