namespace SushiAndFish.Casino.Original
{
using global::System;

[Serializable]
public class BugReportPayload
{
	public string title;

	public string version;

	public string severity;

	public string category;

	public string whatHappened;

	public string expected;

	public string[] reproSteps;

	public string frequency;

	public string priority;

	public bool canReproduceNow;

	public string build;

	public string branch;

	public string channel;

	public string platform;

	public string os;

	public string gpu;

	public string cpu;

	public int ram;

	public string driverVersion;

	public string scene;

	public string level;

	public string seed;

	public string role;

	public string lobby;

	public string region;

	public int ping;

	public float jitter;

	public float packetLoss;

	public string matchId;

	public string sessionId;

	public int playerCount;

	public string networkBackend;

	public string timeSinceMatchStart;

	public string timestampUtc;

	public string gameId;
}

}
