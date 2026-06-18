using UnityEngine;

// Full implementation added Day 7.
// This placeholder exists so NPCAgent.cs compiles today.
public class StrategyBridge : MonoBehaviour
{
    public static int currentStrategyTag = 0;
    // Tag 0 = Surround, 1 = Aggressive, 2 = Flank, 3 = Retreat
    // In Combo A this stays 0 always.
    // In Combo B training it is randomised per episode.
    // In Combo B evaluation it is set by the LLM sidecar.
}