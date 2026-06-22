using UnityEngine;
using UnityEngine.UI;

public class BotTypeSelector : MonoBehaviour
{
    [Header("Bot Scripts on OpponentBot")]
    public AggressiveBot aggressiveBot;
    public EvasiveBot evasiveBot;
    public BalancedBot balancedBot;
    public BotController botController;

    [Header("UI Buttons")]
    public Button btnAggressive;
    public Button btnEvasive;
    public Button btnBalanced;
    public Button btnHuman;

    [Header("Human Mode")]
    public PlayerInputController humanController;
    public GameObject mobileControlsCanvas;

    void Start()
    {
        btnAggressive.onClick.AddListener(() => SelectBotType("Aggressive"));
        btnEvasive.onClick.AddListener(()    => SelectBotType("Evasive"));
        btnBalanced.onClick.AddListener(()   => SelectBotType("Balanced"));
        btnHuman.onClick.AddListener(()      => SelectBotType("Human"));

        SelectBotType("Aggressive");
    }

    public void SelectBotType(string type)
    {
        // Step 1 — disable all bot behaviours
        if (aggressiveBot != null) aggressiveBot.enabled = false;
        if (evasiveBot != null)    evasiveBot.enabled    = false;
        if (balancedBot != null)   balancedBot.enabled   = false;

        if (humanController != null) humanController.enabled = false;
        if (botController != null)   botController.enabled   = true;

        // Step 2 — enable the chosen one
        switch (type)
        {
            case "Aggressive":
                if (aggressiveBot != null) aggressiveBot.enabled = true;
                break;

            case "Evasive":
                if (evasiveBot != null) evasiveBot.enabled = true;
                break;

            case "Balanced":
                if (balancedBot != null) balancedBot.enabled = true;
                break;

            case "Human":
                if (botController != null)   botController.enabled   = false;
                if (humanController != null) humanController.enabled = true;
                break;
        }

        // Step 3 — CRITICAL: refresh AFTER enabling so activeStateMachine
        // picks up the newly enabled component, not the old one
        if (type != "Human" && botController != null)
            botController.RefreshActiveStateMachine();

        // Step 4 — update GameManager logging AFTER refresh
        // so GetCurrentBotType() now returns the correct value
        if (GameManager.instance != null)
            GameManager.instance.SetBotType(botController != null
                ? botController.GetCurrentBotType()
                : type);

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.SetActive(type == "Human");

        Debug.Log($"[BotTypeSelector] Switched to: {type} | "
                  + $"Confirmed active: {botController?.GetCurrentBotType()}");
    }
}