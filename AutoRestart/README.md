# Vellum-AutoRestart
 Automatic restart plugin for Vellum

This is a plugin for Vellum that will enable automatic daily restarts of your Bedrock Dedicated Server for performance reasons.

## Features
- Specify a time for a daily reboot
- Specify how early to warn users the reboot is coming
- High Visibility Warnings: You can choose to make it very obvious to everyone that a restart is coming, pushing hard-to-miss warnings every minute, then every second during the last minute.
- Optional Single Warning: Optionally, it will just send a single plain text message

## Installation
- Download the [**latest release**](https://github.com/tomrhollis/Vellum-Plugins/releases)
- In whatever folder your vellum.exe is in, place it into a *plugins* subfolder
- Copy the sample configuration below into the Plugins section of vellum's configuration.json
- Read the descriptions of the settings below, and make any changes as needed
- Run vellum as normal, and the plugin will be loaded

## Notes
- If a backup or render is running when the plugin tries to restart your server, it will try again 30 minutes later, but the repeated warnings of an upcoming restart that doesn't happen may be annoying for your users.  Make sure your daily restart is scheduled away from a backup or render, ideally 5-15 minutes beforehand.

## Sample Configuration
```
    "AutoRestart": {
      "Enable": true,
      "Config": {
        "DailyRestartTime": "11:00",
        "WarningTime": 10,
        "HiVisShutdown": true,
        "IgnorePatterns": [
          "No targets matched selector",
          "command successfully executed"
        ],
        "TextStrings": {
          "RestartOneWarn": "The server will restart in {0} {1}",
          "RestartMinWarn": "§c§lLess than {0} min to scheduled restart!",
          "RestartSecTitle": "§c{0}",
          "RestartSecSubtl": "§c§lseconds until restart",
          "RestartAbort": "An important process is still running, can't restart. Trying again in {0} minutes",
          "MinutesWord": "minutes",
          "SecondsWord": "seconds",
          "LogLoad": "Plugin Loaded, next restart in {0} minutes, at {1}",
          "LogRestart": "Waiting 10 seconds",
          "LogUnload": "Plugin Unloaded"
        }
      }
    },
```

## Configuration Guide
```
DailyRestartTime          What time to restart your server every day. 
                          Accepts "1:00" or "1:00 AM" or "13:00" or "1:00 PM" for example
                          
WarningTime               Number of minutes before the restart to warn users

HiVisShutdown             true/false: Whether to have lots of colorful warnings during the WarningTime (true)
                          or just do one simple announcement at the WarningTime (false)
                          
IgnorePatterns            Lines containing this text should be prevented from displaying in the console
                          If you're using HiVisShutdown, it will spam your console with certain lines
                          unless the default text shown here is included

-----------------------------------------------------------------------------------------------------
Text Settings - Don't worry about these unless you really want to change the wording the plugin uses
-----------------------------------------------------------------------------------------------------

RestartOneWarn            The only restart warning when HiVisShutdown is false.
                          {0} is replaced with the number of minutes or seconds
                          {1} is replaced with MinutesWord or SecondsWord
                          
RestartMinWarn            With HiVisShutdown true, this text is displayed as an actionbar title
                          every minute before shutdown and all the time when less than a minute left
                          {0} is replaced with the number of minutes left
                          
RestartSecTitle           A big mid-screen title displayed every second for the last 10 seconds before shutdown
                          {0} = The number of seconds remaining
                          
RestartSecSubtl           The subtitle displayed every second for the last 10 seconds before shutdown
                          You can use {0} here for the number of seconds remaining if you want to change it up
                          
RestartAbort              Displayed when a backup or render is running so it isn't safe to restart now
                          {0} is the number of minutes until restart tries again
                          
MinutesWord               The word for minutes (for use in RestartOneWarn)

SecondsWord               The word for seconds (for use in RestartOneWarn)

LogLoad                   Displayed in the console when the plugin is loaded
                          {0} = Number of minutes until the first restart
                          {1} = The time the restart is set for
                          
LogRestart                Displayed in the console when the server is restarting

LogUnload                 Displayed in the console when the plugin is unloaded
                          
```
