using UnityEngine;
using UnityEngine.UI;

// Attach to each HP Slider in the HUD.
// Drag the corresponding HPSystem into the hpSystem field.
// Updates the slider value every frame to match current HP.
public class NPCHPBar : MonoBehaviour
{
    public HPSystem hpSystem;
    private Slider slider;

    void Start()
    {
        slider = GetComponent<Slider>();
        if (hpSystem != null)
        {
            slider.maxValue = hpSystem.maxHP;
            slider.value = hpSystem.currentHP;
        }
    }

    void Update()
    {
        if (hpSystem != null && slider != null)
            slider.value = hpSystem.currentHP;
    }
}