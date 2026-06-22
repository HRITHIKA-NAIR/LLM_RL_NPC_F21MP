using UnityEngine;
using TMPro;

public class HUDControlller : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI episodeTimerText;
    public TextMeshProUGUI strategyLabel;

    private string[] tagNames = { "SURROUND", "AGGRESSIVE", "FLANK", "RETREAT" };

    void Update()
    {
        // Episode timer
        if (episodeTimerText != null && GameManager.instance != null)
        {
            float elapsed = Time.time - GameManager.instance.episodeStartTime;
            episodeTimerText.text = $"Time: {elapsed:F1}s  |  Episode: {GameManager.instance.episodeCount}";
        }

        // Strategy tag label — only visible in Combo B
        if (strategyLabel != null)
        {
            bool isComboB = GameManager.instance != null &&
                            GameManager.instance.currentCombo == "B";
            strategyLabel.gameObject.SetActive(isComboB);

            if (isComboB)
            {
                int tag = StrategyBridge.currentStrategyTag;
                strategyLabel.text = $"Strategy: {tagNames[Mathf.Clamp(tag, 0, 3)]}";
            }
        }
    }
}