using UnityEngine;

public class CombatantStats : MonoBehaviour
{
    [SerializeField] private float maxHealth = 500f;
    [SerializeField] private float maxStamina = 200f;
    [SerializeField] private float health = 500f;
    [SerializeField] private float stamina = 200f;

    public float Health01 => maxHealth <= 0f ? 0f : Mathf.Clamp01(health / maxHealth);
    public float Stamina01 => maxStamina <= 0f ? 0f : Mathf.Clamp01(stamina / maxStamina);
    public float Health => health;
    public float Stamina => stamina;
    public float MaxHealth => maxHealth;
    public float MaxStamina => maxStamina;

    private void Awake()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }

    public void ResetToFull()
    {
        health = maxHealth;
        stamina = maxStamina;
    }

    public float SpendStamina(float amount)
    {
        if (amount <= 0f) return 0f;
        float spent = Mathf.Min(stamina, amount);
        stamina -= spent;
        return spent;
    }

    public float RecoverStamina(float amount)
    {
        if (amount <= 0f) return 0f;
        float previous = stamina;
        stamina = Mathf.Min(maxStamina, stamina + amount);
        return stamina - previous;
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f) return 0f;
        float dmg = Mathf.Min(health, amount);
        health -= dmg;
        return dmg;
    }
}
