# Minecraft Proximity Chat (Discord-Based)
This repository will allow you to set up proximity chat for your Minecraft server.

## Prerequisites
- A [Bukkit](https://getbukkit.org/), [Spigot](https://getbukkit.org/), or [Paper](https://papermc.io/) Minecraft server
- [Discord](https://discord.com/download)

## Setup
The proximity chat requires three components: a data API, a plugin, and an executable application. All three are easy to set up.

### Deploy the Data API
- Click this button: [![Deploy](https://www.herokucdn.com/deploy/button.svg)](https://heroku.com/deploy)
  - You'll sign up for a Heroku account if you don't already have one. 
- Leave the App name blank, and then click **Deploy App**. Wait for Heroku to build and deploy the API. 
- Once it's done, click **View**, which will open your newly-created API. You should see the message "API successfully deployed!" and a bolded host name. 
- Copy this host name for future use.

### Install the Plugin
- Download the .jar from plugin/target and drop it in the plugins folder of your server. 
- Run the server to generate the configuration file.
- Edit plugins/ProximityTracker/config.yml and change "xxx.herokuapp.com" to the host name that you copied earlier. 
- Reload or restart the server.

### Run the Application
TODO
