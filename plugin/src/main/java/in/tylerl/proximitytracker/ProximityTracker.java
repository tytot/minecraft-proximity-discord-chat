package in.tylerl.proximitytracker;

import org.bukkit.configuration.file.FileConfiguration;
import org.bukkit.plugin.java.JavaPlugin;

public class ProximityTracker extends JavaPlugin {
	
	public static long BROADCAST_INTERVAL;
	public static int CUTOFF;
	public static String URL;
	
	private Broadcaster broadcaster;
	
	public void onEnable() {
		this.saveDefaultConfig();
		FileConfiguration config = this.getConfig();
		BROADCAST_INTERVAL = (long) (config.getLong("broadcastInterval", 1000) / 1000.0 * 20);
		System.out.println("[Proximity] Broadcast interval: " + BROADCAST_INTERVAL + " ticks");
		CUTOFF = config.getInt("cutoff", 50);
		System.out.println("[Proximity] Voice cutoff: " + CUTOFF + " blocks");
		URL = "https://" + config.getString("apiHost", "xxx.herokuapp.com");
		System.out.println("[Proximity] API URL: " + URL);
		
		broadcaster = new Broadcaster();
		broadcaster.runTaskTimerAsynchronously(this, 0L, BROADCAST_INTERVAL);
	}
	
	public void onDisable() {
		broadcaster.cancel();
	}
}
