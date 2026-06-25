using UnityEngine;
using System.IO;
using System.Collections;

public class StrategyBridge : MonoBehaviour
{
    // All 5 NPCAgents read this static value every decision step
    public static int currentStrategyTag = 0;

    [Header("Mode")]
    [Tooltip("True during training — disables file read/write. " +
             "False during evaluation — enables LLM communication.")]
    public bool trainingMode = false;

    [Header("Timing")]
    public float pollIntervalSeconds = 5f;

    // File paths are set automatically from Application.persistentDataPath.
    // You do not need to set these manually in the Inspector.
    private string gameStatePath;
    private string strategyTagPath;

    void Start()
    {
        string basePath = Application.persistentDataPath;
        gameStatePath    = Path.Combine(basePath, "game_state.json");
        strategyTagPath  = Path.Combine(basePath, "strategy_tag.json");

        Debug.Log($"[StrategyBridge] Game state path:    {gameStatePath}");
        Debug.Log($"[StrategyBridge] Strategy tag path:  {strategyTagPath}");

        if (trainingMode)
        {
            Debug.Log("[StrategyBridge] Training mode ON — file I/O disabled. " +
                      "Tag will be randomised per episode by NPCAgent.OnEpisodeBegin().");
            return;
        }

        Debug.Log("[StrategyBridge] Evaluation mode ON — " +
                  "starting JSON bridge coroutine.");
        StartCoroutine(PollCoroutine());
    }

    IEnumerator PollCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollIntervalSeconds);

            WriteGameState();

            // Give Python a moment to respond before reading
            yield return new WaitForSeconds(0.5f);

            ReadStrategyTag();
        }
    }

    // ──────────────────────────────────────────────────
    // WRITE — sends current game state to Python
    // ──────────────────────────────────────────────────

    void WriteGameState()
    {
        GameManager gm = GameManager.instance;
        if (gm == null)
        {
            Debug.LogWarning("[StrategyBridge] WriteGameState: GameManager.instance is null.");
            return;
        }

        // Bot HP as a percentage 0–100
        float botHPPercent = 0f;
        if (gm.botHPSystem != null)
            botHPPercent = (gm.botHPSystem.currentHP / gm.botHPSystem.maxHP) * 100f;

        // Average NPC HP
        float avgNPCHP = 0f;
        int npcsAlive = 0;
        float npc1hp = 0f, npc2hp = 0f, npc3hp = 0f, npc4hp = 0f, npc5hp = 0f;

        if (gm.npcHPSystems != null)
        {
            float[] hps = new float[5];
            for (int i = 0; i < gm.npcHPSystems.Count && i < 5; i++)
            {
                if (gm.npcHPSystems[i] == null) continue;
                hps[i] = gm.npcHPSystems[i].currentHP;
                if (!gm.npcHPSystems[i].IsDead())
                {
                    avgNPCHP += hps[i];
                    npcsAlive++;
                }
            }
            if (npcsAlive > 0) avgNPCHP /= npcsAlive;
            if (hps.Length > 0) npc1hp = hps[0];
            if (hps.Length > 1) npc2hp = hps[1];
            if (hps.Length > 2) npc3hp = hps[2];
            if (hps.Length > 3) npc4hp = hps[3];
            if (hps.Length > 4) npc5hp = hps[4];
        }

        float timeElapsed = Time.time - gm.episodeStartTime;

        // Build JSON string manually — no external library needed
        string json =
            "{\n" +
            $"  \"player_hp\": {botHPPercent:F1},\n" +
            $"  \"avg_npc_hp\": {avgNPCHP:F1},\n" +
            $"  \"npcs_alive\": {npcsAlive},\n" +
            $"  \"bot_moving\": {(gm.botIsMoving ? "true" : "false")},\n" +
            $"  \"nearest_dist\": {GetNearestNPCDistToBot():F1},\n" +
            $"  \"npc1_hp\": {npc1hp:F0},\n" +
            $"  \"npc2_hp\": {npc2hp:F0},\n" +
            $"  \"npc3_hp\": {npc3hp:F0},\n" +
            $"  \"npc4_hp\": {npc4hp:F0},\n" +
            $"  \"npc5_hp\": {npc5hp:F0},\n" +
            $"  \"time_elapsed\": {timeElapsed:F1}\n" +
            "}";

        try
        {
            File.WriteAllText(gameStatePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[StrategyBridge] Failed to write game_state.json: {e.Message}");
        }
    }

    float GetNearestNPCDistToBot()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm.botObject == null) return 30f;

        float minDist = 30f;
        foreach (GameObject npcGO in gm.npcObjects)
        {
            if (npcGO == null) continue;
            HPSystem hp = npcGO.GetComponent<HPSystem>();
            if (hp != null && hp.IsDead()) continue;

            float d = Vector3.Distance(
                gm.botObject.transform.position,
                npcGO.transform.position);

            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    // ──────────────────────────────────────────────────
    // READ — receives strategy tag back from Python
    // ──────────────────────────────────────────────────

    void ReadStrategyTag()
    {
        if (!File.Exists(strategyTagPath))
        {
            Debug.Log("[StrategyBridge] strategy_tag.json does not exist yet. " +
                      "Waiting for Python sidecar to create it.");
            return;
        }

        try
        {
            string json = File.ReadAllText(strategyTagPath);

            // Parse manually — look for the "tag" value
            // Expected format: {"tag": 2}
            int tagValue = ParseTagFromJson(json);

            if (tagValue >= 0 && tagValue <= 3)
            {
                if (tagValue != currentStrategyTag)
                {
                    string[] names = { "SURROUND", "AGGRESSIVE", "FLANK", "RETREAT" };
                    Debug.Log($"[StrategyBridge] Tag changed: " +
                              $"{names[currentStrategyTag]} → {names[tagValue]}");
                    currentStrategyTag = tagValue;
                }
            }
            else
            {
                Debug.LogWarning($"[StrategyBridge] Read invalid tag value {tagValue} " +
                                 "from strategy_tag.json. Keeping previous tag.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[StrategyBridge] Failed to read strategy_tag.json: " +
                             $"{e.Message}. Keeping previous tag.");
        }
    }

    int ParseTagFromJson(string json)
    {
        // Looks for "tag": N in the JSON string
        // Works for the simple {"tag": N} format Python writes
        try
        {
            int colonIndex = json.IndexOf("\"tag\"");
            if (colonIndex < 0) return -1;

            int afterColon = json.IndexOf(':', colonIndex) + 1;

            // Skip whitespace
            while (afterColon < json.Length &&
                   (json[afterColon] == ' ' || json[afterColon] == '\n' ||
                    json[afterColon] == '\r' || json[afterColon] == '\t'))
                afterColon++;

            if (afterColon >= json.Length) return -1;

            char digit = json[afterColon];
            if (digit >= '0' && digit <= '3')
                return digit - '0';

            return -1;
        }
        catch
        {
            return -1;
        }
    }
}