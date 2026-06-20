using UnityEngine;

public class ArenaSetup : MonoBehaviour
{
    [Header("NPC Spawn Zone (centre and half-size)")]
    public Vector3 npcSpawnCentre = new Vector3(-7f, 0f, 0f);
    public Vector3 npcSpawnHalfExtents = new Vector3(4f, 0f, 8f);

    [Header("Bot Spawn Zone")]
    public Vector3 botSpawnCentre = new Vector3(7f, 0f, 0f);
    public Vector3 botSpawnHalfExtents = new Vector3(4f, 0f, 8f);

    [Header("References")]
    public Transform[] npcTransforms;
    public Transform botTransform;
    public HPSystem[] npcHPSystems;
    public HPSystem botHPSystem;

    // Remember the bot's original position and rotation
    private Vector3 initialBotPosition;
    private Quaternion initialBotRotation;

    void Awake()
    {
        if (botTransform != null)
        {
            initialBotPosition = botTransform.position;
            initialBotRotation = botTransform.rotation;
        }
    }

    public void ResetAll()
    {
        // Reset positions
        foreach (Transform npc in npcTransforms)
        {
            Vector3 pos = RandomInZone(npcSpawnCentre, npcSpawnHalfExtents);
            npc.position = pos;
            npc.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }

        // Reset bot to its original position and rotation
        if (botTransform != null)
        {
            CharacterController cc = botTransform.GetComponent<CharacterController>();

            if (cc != null)
                cc.enabled = false;

            botTransform.position = initialBotPosition;
            botTransform.rotation = initialBotRotation;

            BotController botController = botTransform.GetComponent<BotController>();
            if (botController != null)
                botController.ResetForNewEpisode();

            if (cc != null)
                cc.enabled = true;
        }

        // Reset HP
        foreach (HPSystem hp in npcHPSystems)
            if (hp != null)
                hp.ResetHP();

        if (botHPSystem != null)
            botHPSystem.ResetHP();

        // Re-enable all NPCs (in case they were disabled on death)
        foreach (Transform npc in npcTransforms)
            npc.gameObject.SetActive(true);

        if (botTransform != null)
            botTransform.gameObject.SetActive(true);
    }

    Vector3 RandomInZone(Vector3 centre, Vector3 halfExtents)
    {
        float x = centre.x + Random.Range(-halfExtents.x, halfExtents.x);
        float z = centre.z + Random.Range(-halfExtents.z, halfExtents.z);
        return new Vector3(x, 0f, z);
    }
}