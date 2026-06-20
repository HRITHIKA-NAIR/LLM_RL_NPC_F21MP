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

    private bool episodeEnding = false;   // guard flag — prevents double-firing
    private int currentEvalEpisode = 0;
    private string[] evalBotOrder = { "Aggressive", "Evasive", "Balanced" };
    private int currentBotTypeIndex = 0;
    private bool evaluationComplete = false;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Get bot HP system
        botHPSystem = botObject.GetComponent<HPSystem>();

        // Collect NPC HP systems
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
            CharacterController botCC = botObject.GetComponent<CharacterController>();
            if (botCC != null)
                botIsMoving = botCC.velocity.magnitude > 0.1f;
        }

        // Check win/loss/timeout conditions
        bool allNPCsDead = true;
        foreach (var hp in npcHPSystems)
            if (!hp.IsDead()) { allNPCsDead = false; break; }

        if (botHPSystem.IsDead())
            StartCoroutine(EndEpisodeRoutine("NPC_SQUAD"));
        else if (allNPCsDead)
            StartCoroutine(EndEpisodeRoutine("BOT"));
        else if (Time.time - episodeStartTime >= maxEpisodeSeconds)
            StartCoroutine(EndEpisodeRoutine("TIMEOUT"));
    }

    IEnumerator EndEpisodeRoutine(string winner)
    {
        // Guard — if already ending, do nothing
        if (episodeEnding) yield break;
        episodeEnding = true;

        // Collect stats
        float duration = Time.time - episodeStartTime;
        float[] npcHPs = new float[npcHPSystems.Count];
        for (int i = 0; i < npcHPSystems.Count; i++)
            npcHPs[i] = npcHPSystems[i].currentHP;

        int npcsAlive = 0;
        foreach (var hp in npcHPSystems)
            if (!hp.IsDead()) npcsAlive++;

        // Log to CSV
        MetricsLogger.instance.LogEpisode(
            episodeCount, currentCombo, currentBotType,
            duration, winner, npcsAlive,
            botHPSystem.currentHP, npcHPs
        );

        // Log to Console with HP detail
        Debug.Log(
            $"[Episode {episodeCount}] Combo {currentCombo} | Bot: {currentBotType}" +
            $" | Time: {duration:F1}s | Winner: {winner}" +
            $" | NPCs Alive: {npcsAlive}/5 | Bot HP: {botHPSystem.currentHP:F0}" +
            $"\nNPC HP: [NPC1:{npcHPs[0]:F0}] [NPC2:{npcHPs[1]:F0}]" +
            $" [NPC3:{npcHPs[2]:F0}] [NPC4:{npcHPs[3]:F0}] [NPC5:{npcHPs[4]:F0}]"
        );

        // Handle evaluation mode episode counting
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
                FindObjectOfType<BotTypeSelector>()?.SelectBotType(nextBot);
            }
        }

        // Wait one frame so ML-Agents processes rewards before reset
        yield return null;

        // Allow death animation and disappearance to finish
        yield return new WaitForSeconds(6f);

        // Tell each NPCAgent their episode is ending
        // ML-Agents needs this to process the final reward and reset the policy
        foreach (GameObject npc in npcObjects)
        {
            NPCAgent agent = npc.GetComponent<NPCAgent>();
            if (agent != null) agent.EndEpisode();
        }

        // Reset bot
        if (botController != null)
            botController.ResetForNewEpisode();

        // Reset arena positions and HP
        if (arenaSetup != null)
            arenaSetup.ResetAll();

        // Increment episode counter and reset timer
        episodeCount++;
        episodeStartTime = Time.time;

        // Release guard — allow next episode to run
        episodeEnding = false;
    }

    // Called by BotTypeSelector when a button is pressed
    public void SetBotType(string type)
    {
        currentBotType = type;
    }
}