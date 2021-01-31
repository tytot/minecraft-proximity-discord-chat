package in.tylerl.proximitytracker;

import org.bukkit.command.Command;
import org.bukkit.command.CommandSender;
import org.bukkit.configuration.file.FileConfiguration;
import org.bukkit.plugin.java.JavaPlugin;

public class ProximityTracker extends JavaPlugin {
	
	public static int CUTOFF;
	public static String URL;
	public static long BROADCAST_INTERVAL;
	
	private Broadcaster broadcaster;
	private boolean enabled;
	
	public void onEnable() {
		this.saveDefaultConfig();
		FileConfiguration config = this.getConfig();
		CUTOFF = config.getInt("cutoff", 50);
		System.out.println("[Proximity] Voice cutoff: " + CUTOFF + " blocks");
		URL = "https://" + config.getString("apiHost", "xxx.herokuapp.com");
		System.out.println("[Proximity] API URL: " + URL);
		BROADCAST_INTERVAL = (long) (config.getLong("broadcastInterval", 1000) / 1000.0 * 20);
		System.out.println("[Proximity] Broadcast interval: " + BROADCAST_INTERVAL + " ticks");
		
		broadcaster = new Broadcaster();
		enabled = config.getBoolean("enabledByDefault", false);
		if (enabled) {
			broadcaster.runTaskTimer(this, 0L, BROADCAST_INTERVAL);
		} else {
			broadcaster.postToAPI("{}");
		}
	}
	
	public boolean onCommand(CommandSender sender, Command cmd, String label, String[] args) {
		if (cmd.getName().equalsIgnoreCase("proximity")) {
			if (args.length == 1) {
				if (args[0].equalsIgnoreCase("on")) {
					if (enabled) {
						sender.sendMessage("Proximity chat is already on.");
					} else {
						enabled = true;
						broadcaster.runTaskTimer(this, 0L, BROADCAST_INTERVAL);
					}
					return true;
				} else if (args[0].equalsIgnoreCase("off")) {
					if (enabled) {
						enabled = false;
						broadcaster.cancel();
						broadcaster.postToAPI("{}");
					} else {
						sender.sendMessage("Proximity chat is already off.");
					}
					return true;
				}
			}
			return false;
		}
		return true;
	}
	
	public void onDisable() {
		broadcaster.cancel();
		broadcaster.postToAPI("{}");
	}
}
