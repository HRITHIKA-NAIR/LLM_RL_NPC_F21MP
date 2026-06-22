using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("References")]
    public List<GameObject> npcObjects;
    public GameObject botObject;
    public ArenaSetup arenaSetup;
    public BotController botController;

    [Header("State")]
    public string currentBotType = "Aggressive";
    public string currentCombo = "A";
    public bool trainingMode = true;
    public float maxEpisodeSeconds = 120f;

    [Header("Evaluation (set in Inspector before eval runs)")]
    public bool evaluationMode = false;
    public int episodesPerBotType = 100;

    // Public so StrategyBridge can read them
    public int episodeCount = 0;
    public float episodeStartTime;
    public HPSystem botHPSystem;
    public bool botIsMoving = false;
    public List<HPSystem> npcHPSystems = new List<HPSystem>();

    private bool episodeEnding = false;
    private int currentEvalEpisode = 0;
    private string[] evalBotOrder = { "Aggressive", "Evasive", "Balanced" };
    private int currentBotTypeIndex = 0;
    private bool evaluationComplete = false;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // Get bot HP system
        botHPSystem = botObject.GetComponent<HPSystem>();

        // Collect NPC HP systems

        npcHPSystems.Clear();

        foreach (var npc in npcObjects)
            npcHPSystems.Add(npc.GetComponent<HPSystem>());

        // Set time scale
        Time.timeScale = trainingMode ? 5f : 1f;

        episodeStartTime = Time.time;
        episodeCount = 0;
    }

    void Update()
    {
        if (episodeEnding) return;
        if (botHPSystem == null) return;
        if (evaluationComplete) return;

        // Track bot movement for StrategyBridge
        if (botObject != null)
        {
            CharacterController botCC =
                botObject.GetComponent<CharacterController>();

            if (botCC != null)
                botIsMoving = botCC.velocity.magnitude > 0.1f;
        }

        // Check win/loss conditions
        bool allNPCsDead = true;

        foreach (var hp in npcHPSystems)
        {
            if (!hp.IsDead())
            {
                allNPCsDead = false;
                break;
            }
        }

        if (botHPSystem.IsDead())
            StartCoroutine(EndEpisodeRoutine("NPC_SQUAD"));

        else if (allNPCsDead)
            StartCoroutine(EndEpisodeRoutine("BOT"));

        else if (Time.time - episodeStartTime >= maxEpisodeSeconds)
            StartCoroutine(EndEpisodeRoutine("TIMEOUT"));
    }

    IEnumerator EndEpisodeRoutine(string winner)
    {
        if (episodeEnding) yield break;
        episodeEnding = true;

        float duration = Time.time - episodeStartTime;

        float[] npcHPs = new float[npcHPSystems.Count];
        for (int i = 0; i < npcHPSystems.Count; i++)
            npcHPs[i] = npcHPSystems[i].currentHP;

        int npcsAlive = 0;
        foreach (var hp in npcHPSystems)
            if (!hp.IsDead()) npcsAlive++;

        // ── REMOVED: currentBotType = botController.GetCurrentBotType(); ──
        // currentBotType is already correct from BotTypeSelector.SelectBotType()

        MetricsLogger.instance.LogEpisode(
            episodeCount, currentCombo, currentBotType,
            duration, winner, npcsAlive,
            botHPSystem.currentHP, npcHPs
        );

        Debug.Log(
            $"[Episode {episodeCount}] Combo {currentCombo} | Bot: {currentBotType}" +
            $" | Time: {duration:F1}s | Winner: {winner}" +
            $" | NPCs Alive: {npcsAlive}/5 | Bot HP: {botHPSystem.currentHP:F0}" +
            $"\nNPC HP: [NPC1:{npcHPs[0]:F0}] [NPC2:{npcHPs[1]:F0}]" +
            $" [NPC3:{npcHPs[2]:F0}] [NPC4:{npcHPs[3]:F0}] [NPC5:{npcHPs[4]:F0}]"
        );

        // Evaluation mode
        if (evaluationMode && !evaluationComplete)
        {
            currentEvalEpisode++;

            if (currentEvalEpisode >= episodesPerBotType)
            {
                currentEvalEpisode = 0;
                currentBotTypeIndex++;

                if (currentBotTypeIndex >= evalBotOrder.Length)
                {
                    evaluationComplete = true;

                    Debug.Log("[Eval] EVALUATION COMPLETE. Check CSV file.");

                    Time.timeScale = 1f;

                    episodeEnding = false;
                    yield break;
                }

                string nextBot = evalBotOrder[currentBotTypeIndex];

                currentBotType = nextBot;

                Debug.Log($"[Eval] Switching to bot type: {nextBot}");

                BotTypeSelector selector =
                    FindObjectOfType<BotTypeSelector>();

                if (selector != null)
                    selector.SelectBotType(nextBot);
            }
        }

        // Allow rewards to be processed
        yield return null;

        // Let death animations finish
        yield return new WaitForSeconds(6f);

        // End ML-Agent episodes
        foreach (GameObject npc in npcObjects)
        {
            NPCAgent agent = npc.GetComponent<NPCAgent>();

            if (agent != null)
                agent.EndEpisode();
        }

        // Reset bot
        if (botController != null)
            botController.ResetForNewEpisode();

        // Reset arena positions and HP
        if (arenaSetup != null)
            arenaSetup.ResetAll();

        // Start next episode
        episodeCount++;
        episodeStartTime = Time.time;

        episodeEnding = false;
    }

    // Called by BotTypeSelector
    public void SetBotType(string type)
    {
        currentBotType = type;
    }
}