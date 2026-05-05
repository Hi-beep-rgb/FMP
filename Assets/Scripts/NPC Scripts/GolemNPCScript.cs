using UnityEngine;
using UnityEngine.AI;

public class GolemNPCScript : MonoBehaviour
{
    public Transform player;
    public bool playerInSightRange;
    public float sightRange;
    public LayerMask whatIsPlayer;

    private void Awake()
    {
        player = GameObject.Find("Player").transform;
    }

    // Update is called once per frame
    void Update()
    {
        playerInSightRange = Physics.CheckSphere(transform.position, sightRange, whatIsPlayer);

        if (playerInSightRange) AttackPlayer();
    }

    private void AttackPlayer()
    {
        transform.LookAt(player);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}
