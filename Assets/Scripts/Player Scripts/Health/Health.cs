using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Health : MonoBehaviour
{
    public int maxHealth = 250;
    public int currentHealth;

    public HealthBar healthBar;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealth;
        healthBar.SetMaxHealth(maxHealth);
    }

    void TakeDamage(int damage)
    {
        currentHealth -= damage;

        healthBar.SetMaxHealth(currentHealth);

        if (currentHealth <= 0)
        {
            SceneManager.LoadScene(1);
        }
    }
}
