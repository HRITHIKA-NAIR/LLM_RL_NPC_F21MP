using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("References")]
    public List<GameObject> npcObjects;
    public GameObject botObject;

    [Header("State")]
    public string currentBotType = "Aggressive";
    public string currentCombo = "A";
    public bool trainingMode = true;
    public int episodeCount = 0;
    public float episodeStartTime;

    private HPSystem botHP;
    private List<HPSystem> npcHPSystems = new List<HPSystem>();

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        botHP = botObject.GetComponent<HPSystem>();
        foreach (var npc in npcObjects)
            npcHPSystems.Add(npc.GetComponent<HPSystem>());

        StartEpisode();
    }

    public void StartEpisode()
    {
        episodeStartTime = Time.time;
        episodeCount++;
    }

    void Update()
    {
        if (botHP == null) return;

        bool allNPCsDead = true;
        foreach (var hp in npcHPSystems)
            if (!hp.IsDead()) { allNPCsDead = false; break; }

        if (botHP.IsDead())
            EndEpisode("NPC_SQUAD");
        else if (allNPCsDead)
            EndEpisode("BOT");
    }

    public void EndEpisode(string winner)
    {
        float duration = Time.time - episodeStartTime;

        float[] npcHPs = new float[npcHPSystems.Count];
        for (int i = 0; i < npcHPSystems.Count; i++)
            npcHPs[i] = npcHPSystems[i].currentHP;

        int npcsAlive = 0;
        foreach (var hp in npcHPSystems)
            if (!hp.IsDead()) npcsAlive++;

        MetricsLogger.instance.LogEpisode(
            episodeCount, currentCombo, currentBotType,
            duration, winner, npcsAlive, botHP.currentHP, npcHPs
        );

        Debug.Log($"[Episode {episodeCount}] Combo {currentCombo} | Bot: {currentBotType} " +
                  $"| Time: {duration:F1}s | Winner: {winner} | NPCs Alive: {npcsAlive}/5 " +
                  $"| Bot HP: {botHP.currentHP:F0}" +
                  $"\nNPC HP: [NPC1:{npcHPs[0]:F0}] [NPC2:{npcHPs[1]:F0}] " +
                  $"[NPC3:{npcHPs[2]:F0}] [NPC4:{npcHPs[3]:F0}] [NPC5:{npcHPs[4]:F0}]");

        // Reset for next episode — NPCAgent.OnEpisodeBegin() handles NPC reset
        // Bot and NPC HP reset handled by ArenaSetup
    }
}