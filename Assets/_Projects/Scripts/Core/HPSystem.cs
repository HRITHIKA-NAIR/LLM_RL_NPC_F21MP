using UnityEngine;
using UnityEngine.Events;

public class HPSystem : MonoBehaviour
{
    [Header("HP Settings")]
    public float maxHP = 60f;

    [HideInInspector]
    public float currentHP;

    public UnityEvent OnDeath;

    private bool isDead = false;

    void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHP -= amount;
        currentHP = Mathf.Max(0f, currentHP);

        if (currentHP <= 0f && !isDead)
        {
            isDead = true;
            OnDeath.Invoke();
        }
    }

    public void ResetHP()
    {
        isDead = false;
        currentHP = maxHP;
    }

    public bool IsDead()
    {
        return isDead;
    }
}