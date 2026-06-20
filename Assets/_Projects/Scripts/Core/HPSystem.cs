using UnityEngine;
using UnityEngine.Events;
using System.Collections;

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

         if (!gameObject.activeInHierarchy) return;

        if (isDead) return;

        currentHP -= amount;
        currentHP = Mathf.Max(0f, currentHP);

        if (currentHP <= 0f && !isDead)
        {
            isDead = true;

            OnDeath.Invoke();

            // Stay lying down for 5 seconds, then disappear
            StartCoroutine(DisableAfterDelay());
        }
    }

    IEnumerator DisableAfterDelay()
    {
        yield return new WaitForSeconds(5f);

        gameObject.SetActive(false);
    }

    public void ResetHP()
    {
        // Stop any old "disable after death" coroutine
        StopAllCoroutines();

        isDead = false;
        currentHP = maxHP;

        // Bring character back next episode
        gameObject.SetActive(true);
    }

    public bool IsDead()
    {
        return isDead;
    }
}