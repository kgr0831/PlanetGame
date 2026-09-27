using System;
using UnityEngine;

public enum OblationResource { Biomass, Minerals, Neural, Offering }
public enum OblationTrait { Resource, Unit, Research }
public enum OblationResearchAxis { Hive, Industry, Combat }
public enum OblationExterminatus { Plague, Siege, Orbital }

[Serializable]
public struct OblationCost
{
    public float biomass;
    public float minerals;
    public float neural;
    public float offering;

    public OblationCost(float biomass, float minerals, float neural, float offering = 0)
    {
        this.biomass = biomass;
        this.minerals = minerals;
        this.neural = neural;
        this.offering = offering;
    }
}

public abstract class OblationDefinitionSO : ScriptableObject
{
    public string id;
    public string displayName;
}
