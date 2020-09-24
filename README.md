# Vellum-AutoRestart
 Automatic restart plugin for Vellum

This is a plugin for Vellum that will enable automatic daily restarts of your Bedrock Dedicated Server for performance reasons.

It is not ready for primetime yet; when it is, I'll post a release!

It will have:
- Specify a time for a daily reboot
- High Visibility Warnings: Starting 10 minutes before the restart, the plugin will push warnings to users that will be hard to miss.
- Optional Single Warning: Optionally, it will just send a single plain text message 5 minutes before the restart (`HiVisWarnMsgs = false`)
- If the VellumZero plugin is loaded, it will use that to send messages to the server instead, and to the bus and/or Discord connections if they exist


## Notes
- If a backup or render is running when the plugin tries to restart your server, it will try again 30 minutes later, but the repeated warnings of an upcoming restart that doesn't happen may be annoying for your users.  Make sure your daily restart is scheduled away from a backup or render, ideally 5-15 minutes beforehand.
