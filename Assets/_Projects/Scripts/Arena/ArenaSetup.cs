using UnityEngine;

public class ArenaSetup : MonoBehaviour
{
    [Header("NPC Spawn Zone (centre and half-size)")]
    public Vector3 npcSpawnCentre = new Vector3(-7f, 0f, 0f);
    public Vector3 npcSpawnHalfExtents = new Vector3(5f, 0f, 12f);

    [Header("Bot Spawn Zone")]
    public Vector3 botSpawnCentre = new Vector3(7f, 0f, 0f);
    public Vector3 botSpawnHalfExtents = new Vector3(4f, 0f, 8f);

    [Header("References")]
    public Transform[] npcTransforms;
    public Transform botTransform;
    public HPSystem[] npcHPSystems;
    public HPSystem botHPSystem;

    public void ResetAll()
    {
        // Reset positions
        foreach (Transform npc in npcTransforms)
        {
            Vector3 pos = RandomInZone(npcSpawnCentre, npcSpawnHalfExtents);
            npc.position = pos;
            npc.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }

        if (botTransform != null)
        {
            botTransform.position = RandomInZone(botSpawnCentre, botSpawnHalfExtents);
            botTransform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
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