using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("References")]
    public List<GameObject> npcObjects;
    public GameObject botObject;
    public ArenaSetup arenaSetup;

    [Header("State")]
    public string currentBotType = "Aggressive";
    public string currentCombo = "A";
    public bool trainingMode = true;

    public int episodeCount = 0;
    public float episodeStartTime;

    private HPSystem botHP;
    private List<HPSystem> npcHPSystems = new List<HPSystem>();

    // Prevent multiple EndEpisode() calls
    private bool episodeEnding = false;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
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
        episodeEnding = false;

        episodeCount++;
        episodeStartTime = Time.time;

        if (arenaSetup != null)
            arenaSetup.ResetAll();

        foreach (GameObject npc in npcObjects)
        {
            NPCAgent agent = npc.GetComponent<NPCAgent>();

            if (agent != null)
                agent.EndEpisode();
        }

        Debug.Log($"Episode {episodeCount} started.");
    }

    void Update()
    {
        if (botHP == null || episodeEnding)
            return;

        bool allNPCsDead = true;

        foreach (var hp in npcHPSystems)
        {
            if (!hp.IsDead())
            {
                allNPCsDead = false;
                break;
            }
        }

        if (episodeEnding)
            return;

        if (botHP.IsDead())
        {
            EndEpisode("NPC_SQUAD");
        }
        else if (allNPCsDead)
        {
            EndEpisode("BOT");
        }
        else if (Time.time - episodeStartTime >= 120f)
        {
            EndEpisode("TIMEOUT");
        }
    }

    public void EndEpisode(string winner)
    {
        // Prevent duplicate calls
        if (episodeEnding)
            return;

        episodeEnding = true;

        float duration = Time.time - episodeStartTime;

        float[] npcHPs = new float[npcHPSystems.Count];

        for (int i = 0; i < npcHPSystems.Count; i++)
            npcHPs[i] = npcHPSystems[i].currentHP;

        int npcsAlive = 0;

        foreach (var hp in npcHPSystems)
            if (!hp.IsDead())
                npcsAlive++;

        MetricsLogger.instance.LogEpisode(
            episodeCount,
            currentCombo,
            currentBotType,
            duration,
            winner,
            npcsAlive,
            botHP.currentHP,
            npcHPs
        );

        Debug.Log(
            $"[Episode {episodeCount}] Combo {currentCombo}" +
            $" | Bot: {currentBotType}" +
            $" | Time: {duration:F1}s" +
            $" | Winner: {winner}" +
            $" | NPCs Alive: {npcsAlive}/5" +
            $" | Bot HP: {botHP.currentHP:F0}"
        );

        StartEpisode();
    }
}