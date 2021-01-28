package in.tylerl.proximitytracker;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.entity.Player;
import org.bukkit.scheduler.BukkitRunnable;

public class Broadcaster extends BukkitRunnable {
	
	public void run() {
		Player[] players = Bukkit.getOnlinePlayers().toArray(new Player[0]);
		int[][] proximities = new int[players.length][players.length];
		for (int i = 0; i < players.length; i++) {
			for (int j = i + 1; j < players.length; j++) {
				int dist = (int) distance(players[i].getLocation(), players[j].getLocation());
				int volume = distanceToVolume(dist);
				proximities[i][j] = volume;
				proximities[j][i] = volume;
			}
		}
		String json = "{";
		for (int i = 0; i < proximities.length; i++) {
			json += "\"" + players[i].getName() + "\":{";
			for (int j = 0; j < proximities[i].length; j++) {
				if (i != j) {
					json += "\"" + players[j].getName() + "\":" + proximities[i][j] + ",";
				}
			}
			if (proximities[i].length > 1) {
				json = json.substring(0, json.length() - 1);
			}
			json += "},";
		}
		if (proximities.length > 0) {
			json = json.substring(0, json.length() - 1);
		}
		json += "}";
//		System.out.println(json);

		try {
			URL url = new URL(ProximityTracker.URL);
			HttpURLConnection c = (HttpURLConnection) url.openConnection();
			c.setRequestMethod("POST");
			c.setRequestProperty("Content-Type", "application/json; charset=UTF-8");
			c.setRequestProperty("Accept", "application/json");
			c.setDoOutput(true);
			OutputStream os = c.getOutputStream();
			byte[] input = json.getBytes("utf-8");
			os.write(input);
			BufferedReader br = new BufferedReader(new InputStreamReader(c.getInputStream(), "utf-8"));
			br.readLine();
		} catch (Exception e) {
			// TODO Auto-generated catch block
			e.printStackTrace();
		}
	}

	private double distance(Location loc1, Location loc2) {
		return Math.sqrt(Math.pow(loc1.getX() - loc2.getX(), 2)
		+ Math.pow(loc1.getY() - loc2.getY(), 2)
		+ Math.pow(loc1.getZ() - loc2.getZ(), 2));
	}
	
	private int distanceToVolume(int distance) {
		if (distance > ProximityTracker.CUTOFF) {
			return 0;
		}
		return (int) (100.0 * (ProximityTracker.CUTOFF - distance) / ProximityTracker.CUTOFF);
	}
}
