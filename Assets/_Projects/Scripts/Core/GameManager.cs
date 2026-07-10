using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;

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

    [Header("Evaluation Settings")]
    public bool evaluationMode = false;
    public int episodesPerBotType = 100;

    [Header("Training Bot Rotation (step-based, automatic)")]
    [Tooltip("Total max_steps in your YAML. Must match exactly.")]
    public int totalTrainingSteps = 3000000;
    [Tooltip("How many bot types to rotate through. Normally 3.")]
    public int numberOfBotTypes = 3;
    // Rotation threshold = totalTrainingSteps / numberOfBotTypes
    // At threshold steps: switch from Aggressive → Evasive
    // At 2× threshold:    switch from Evasive   → Balanced
    // Calculated automatically — do not set manually

    // Public fields read by StrategyBridge
    public int episodeCount = 0;
    public float episodeStartTime;
    public HPSystem botHPSystem;
    public bool botIsMoving = false;
    public List<HPSystem> npcHPSystems = new List<HPSystem>();

    // Private state
    private bool episodeEnding = false;
    private int currentEvalEpisode = 0;
    private string[] botOrder = { "Aggressive", "Evasive", "Balanced" };
    private int currentBotTypeIndex = 0;
    private bool evaluationComplete = false;

    // Training rotation state
    private int trainingRotationThreshold = 0;
    private int lastBotTypeIndex = -1;  // tracks which bot is active in training

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        botHPSystem = botObject.GetComponent<HPSystem>();

        npcHPSystems.Clear();
        foreach (var npc in npcObjects)
            npcHPSystems.Add(npc.GetComponent<HPSystem>());

        Time.timeScale = trainingMode ? 6f : 1f;

        episodeStartTime = Time.time;
        episodeCount = 0;

        // Calculate the step threshold for each bot type rotation
        // e.g. 3000000 / 3 = 1000000 steps per bot type
        trainingRotationThreshold = totalTrainingSteps / numberOfBotTypes;

        if (trainingMode)
        {
            // Set the starting bot type and refresh the state machine
            currentBotTypeIndex = 0;
            lastBotTypeIndex = 0;
            currentBotType = botOrder[0];
            ApplyBotTypeSwitch(botOrder[0]);

            Debug.Log($"[Training] Starting with {botOrder[0]}. " +
                      $"Rotation every {trainingRotationThreshold:N0} steps. " +
                      $"Bot types: Aggressive (0-{trainingRotationThreshold:N0}) → " +
                      $"Evasive ({trainingRotationThreshold:N0}-{trainingRotationThreshold*2:N0}) → " +
                      $"Balanced ({trainingRotationThreshold*2:N0}-{totalTrainingSteps:N0})");
        }
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

        // ── TRAINING BOT ROTATION (step-based, automatic) ──
        if (trainingMode)
        {
            // Academy.Instance.StepCount is the actual environment step count
            // from ML-Agents. It is reliable and does not depend on episode length.
            int currentSteps = 0;
            try
            {
                currentSteps = (int)Academy.Instance.StepCount;
            }
            catch
            {
                // Academy not initialised yet on first frame — safe to ignore
            }

            // Calculate which bot type should be active right now
            // based on current step count
            int targetBotIndex = Mathf.Min(
                currentSteps / trainingRotationThreshold,
                numberOfBotTypes - 1
            );

            // Only switch if the target is different from what is currently active
            if (targetBotIndex != lastBotTypeIndex)
            {
                lastBotTypeIndex = targetBotIndex;
                currentBotTypeIndex = targetBotIndex;
                string newBotType = botOrder[targetBotIndex];
                currentBotType = newBotType;

                Debug.Log($"[Training] Step {currentSteps:N0}: " +
                          $"Switching to {newBotType} bot. " +
                          $"(threshold {trainingRotationThreshold * targetBotIndex:N0})");

                ApplyBotTypeSwitch(newBotType);
            }
        }

        // ── WIN / LOSS / TIMEOUT CHECK ──
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

    // ── SWITCH BOT TYPE ──
    // Called by training rotation and by evaluation runner
    void ApplyBotTypeSwitch(string type)
    {
        BotTypeSelector selector = FindObjectOfType<BotTypeSelector>();
        if (selector != null)
        {
            selector.SelectBotType(type);
        }
        else
        {
            // No BotTypeSelector in scene (e.g. HUD disabled during training)
            // Manually enable/disable the bot state machines
            if (botController == null) return;

            AggressiveBot agg = botController.GetComponent<AggressiveBot>();
            EvasiveBot ev    = botController.GetComponent<EvasiveBot>();
            BalancedBot bal  = botController.GetComponent<BalancedBot>();

            if (agg != null) agg.enabled = (type == "Aggressive");
            if (ev  != null) ev.enabled  = (type == "Evasive");
            if (bal != null) bal.enabled  = (type == "Balanced");

            botController.RefreshActiveStateMachine();
        }

        currentBotType = type;
        Debug.Log($"[GameManager] Bot type applied: {type}");
    }

    // ── EPISODE END ──
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

        // Log to CSV
        MetricsLogger.instance.LogEpisode(
            episodeCount, currentCombo, currentBotType,
            duration, winner, npcsAlive,
            botHPSystem.currentHP, npcHPs
        );

        Debug.Log(
            $"[Episode {episodeCount}] Combo {currentCombo}" +
            $" | Bot: {currentBotType}" +
            $" | Time: {duration:F1}s" +
            $" | Winner: {winner}" +
            $" | NPCs Alive: {npcsAlive}/5" +
            $" | Bot HP: {botHPSystem.currentHP:F0}" +
            $"\nNPC HP: [NPC1:{npcHPs[0]:F0}]" +
            $" [NPC2:{npcHPs[1]:F0}]" +
            $" [NPC3:{npcHPs[2]:F0}]" +
            $" [NPC4:{npcHPs[3]:F0}]" +
            $" [NPC5:{npcHPs[4]:F0}]"
        );

        // ── EVALUATION MODE: count episodes per bot type ──
        if (evaluationMode && !evaluationComplete)
        {
            currentEvalEpisode++;

            if (currentEvalEpisode >= episodesPerBotType)
            {
                currentEvalEpisode = 0;
                currentBotTypeIndex++;

                if (currentBotTypeIndex >= botOrder.Length)
                {
                    evaluationComplete = true;
                    Debug.Log("[Eval] EVALUATION COMPLETE. Check CSV file.");
                    Time.timeScale = 1f;
                    episodeEnding = false;
                    yield break;
                }

                string nextBot = botOrder[currentBotTypeIndex];
                currentBotType = nextBot;
                Debug.Log($"[Eval] Switching to bot type: {nextBot}");
                ApplyBotTypeSwitch(nextBot);
            }
        }

        // Wait one frame for ML-Agents to process rewards
        yield return null;

        // Wait for death animations
        yield return new WaitForSeconds(trainingMode ? 6f : 6f);

        // Tell each NPCAgent their episode is ending
        foreach (GameObject npc in npcObjects)
        {
            NPCAgent agent = npc.GetComponent<NPCAgent>();
            if (agent != null) agent.EndEpisode();
        }

        // Reset bot
        if (botController != null)
            botController.ResetForNewEpisode();

        // Reset arena
        if (arenaSetup != null)
            arenaSetup.ResetAll();

        // Increment episode counter
        episodeCount++;
        episodeStartTime = Time.time;

        episodeEnding = false;
    }

    public void SetBotType(string type)
    {
        currentBotType = type;
    }
}