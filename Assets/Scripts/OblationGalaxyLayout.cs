using System.Collections.Generic;
using UnityEngine;

// Shared by the saved-scene authoring pass, runtime topology and validation.
public static class OblationGalaxyLayout
{
    public const float MapNear = 4, MapFar = 210, FocusNear = 3, FocusFar = 90;
    public const float StartingDistance = 32, OverviewDistance = 170, PanLimit = 78;
    public static readonly string[] Names = {
        "VESPER","NEMESIS","KHEPRI","TALOS","MORROW","EIDOLON","ORISON","CINDER","HALCYON","PERIHELION","GOLGOTHA","SERAPH",
        "NYX","EREBUS","AURORA","CALDERA","ITHACA","NACRE","UMBRA","PYRE","LYRA","NOCTIS","OSIRIS","VEIL",
        "APHELION","SOLACE","ACHERON","LACUNA","MIRAGE","OBSIDIAN","THRENODY","VIGIL","CAIRN","SABLE","HORIZON","ELYSIUM"
    };
    public static readonly Vector3[] Positions = BuildPositions();
    public static readonly int[,] Edges = BuildEdges();
    static Vector3[] BuildPositions()
    {
        var positions = new Vector3[Names.Length];
        Vector3[] core = {new Vector3(0,0,0),new Vector3(9,0,9),new Vector3(0,0,-4.5f),new Vector3(-1,0,-9),
            new Vector3(4.5f,0,0),new Vector3(9,0,-1),new Vector3(0,0,4.5f),new Vector3(-1,0,9),
            new Vector3(-4.5f,0,0),new Vector3(-9,0,1),new Vector3(-5,0,-5),new Vector3(5,0,5)};
        for(int i=0;i<12;i++)positions[i]=core[i]*2.5f;
        for(int ring=0;ring<2;ring++)for(int i=0;i<12;i++)
        {
            float angle=(i*30+ring*12)*Mathf.Deg2Rad;
            float radius=(ring==0?40:64)+(i%3-1)*2;
            positions[12+ring*12+i]=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
        }
        return positions;
    }
    static int[,] BuildEdges()
    {
        var pairs=new List<Vector2Int>();
        int[,] core={{0,2},{0,4},{0,6},{0,8},{2,3},{2,10},{3,10},{4,5},{4,11},{5,1},{5,11},{6,7},{6,11},{7,11},{7,9},{8,9},{8,10},{9,10},{1,11},{2,4},{6,8}};
        for(int i=0;i<core.GetLength(0);i++)pairs.Add(new Vector2Int(core[i,0],core[i,1]));
        for(int i=0;i<12;i++)
        {
            pairs.Add(new Vector2Int(12+i,12+(i+1)%12));
            pairs.Add(new Vector2Int(24+i,24+(i+1)%12));
            pairs.Add(new Vector2Int(12+i,24+i));
            int nearest=1;
            for(int j=2;j<12;j++)if((Positions[j]-Positions[12+i]).sqrMagnitude<(Positions[nearest]-Positions[12+i]).sqrMagnitude)nearest=j;
            pairs.Add(new Vector2Int(nearest,12+i));
        }
        var edges=new int[pairs.Count,2];
        for(int i=0;i<pairs.Count;i++){edges[i,0]=pairs[i].x;edges[i,1]=pairs[i].y;}
        return edges;
    }
}
