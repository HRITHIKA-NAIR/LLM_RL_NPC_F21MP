using UnityEngine;
using System.IO;
using System.Text;

public class MetricsLogger : MonoBehaviour
{
    public static MetricsLogger instance;

    private string csvPath;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        csvPath = Path.Combine(Application.persistentDataPath, "episode_results.csv");
        WriteHeader();
        Debug.Log($"[MetricsLogger] CSV path: {csvPath}");
    }

    void WriteHeader()
    {
        if (!File.Exists(csvPath))
        {
            string header = "episode_num,combination,bot_type,duration_seconds,winner," +
                            "npcs_alive,bot_hp_end," +
                            "npc1_hp,npc2_hp,npc3_hp,npc4_hp,npc5_hp";
            File.WriteAllText(csvPath, header + "\n");
        }
    }

    public void LogEpisode(int episodeNum, string combo, string botType,
                           float duration, string winner, int npcsAlive,
                           float botHPEnd, float[] npcHPs)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"{episodeNum},{combo},{botType},{duration:F2},{winner},");
        sb.Append($"{npcsAlive},{botHPEnd:F0},");
        for (int i = 0; i < 5; i++)
            sb.Append($"{(i < npcHPs.Length ? npcHPs[i] : 0f):F0}" + (i < 4 ? "," : ""));

        File.AppendAllText(csvPath, sb.ToString() + "\n");
    }
}