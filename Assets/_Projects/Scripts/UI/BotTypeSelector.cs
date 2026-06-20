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

        // Start with Aggressive active
        SelectBotType("Aggressive");
    }

    public void SelectBotType(string type)
    {
        // Disable all bot behaviours first
        aggressiveBot.enabled = false;
        evasiveBot.enabled = false;
        balancedBot.enabled = false;

        if (humanController != null)
            humanController.enabled = false;

        botController.enabled = true;

        switch (type)
        {
            case "Aggressive":
                aggressiveBot.enabled = true;
                break;
            case "Evasive":
                evasiveBot.enabled = true;
                break;
            case "Balanced":
                balancedBot.enabled = true;
                break;
            case "Human":
                botController.enabled = false;
                if (humanController != null)
                    humanController.enabled = true;
                break;
        }

        // Tell BotController which state machine is now active
        if (type != "Human")
            botController.RefreshActiveStateMachine();

        // Update GameManager logging
        if (GameManager.instance != null)
            GameManager.instance.SetBotType(type);

        Debug.Log($"[BotTypeSelector] Switched to: {type}");
    }
}