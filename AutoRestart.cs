using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;
using Vellum.Automation;
using Vellum.Extension;

namespace AutoRestart
{
    public class AutoRestart : IPlugin
    {
        public RestartConfig RConfig;

        private Timer autoRestartTimer;
        private Timer hiVisWarnTimer;
        private Timer hiVisWarnMsgs;
        private Timer normalWarnMsg;
        private uint msCountdown;
        private string _worldName = "Bedrock level";
        private bool crashing = false;
        private bool vzEnabled = false;
        private bool statusMessages = true;

        private ProcessManager bds;
        private BackupManager backupManager;
        private RenderManager renderManager;
        private Watchdog bdsWatchdog;
        private VellumZero.VellumZero vz;

        #region PLUGIN
        internal IHost Host; 
        public PluginType PluginType { get { return PluginType.EXTERNAL; } }
        private Dictionary<byte, IPlugin.HookHandler> _hookCallbacks = new Dictionary<byte, IPlugin.HookHandler>();
        public enum Hook
        {
            GOING_DOWN,
            STARTING_UP
        }

        public Dictionary<byte, string> GetHooks()
        {
            Dictionary<byte, string> hooks = new Dictionary<byte, string>();

            foreach (byte hookId in Enum.GetValues(typeof(Hook)))
                hooks.Add(hookId, Enum.GetName(typeof(Hook), hookId));

            return hooks;
        }

        public static object GetDefaultRunConfiguration()
        {
            return new RestartConfig()
            {
                AutoRestartTime = "12:00p",
                HiVisShutdown = true,
                TextStrings = new RestartStrings()
                {
                    RestartOneWarn = "The server will restart in {0} {1}",
                    RestartMinWarn = "§c§lLess than {0} min to scheduled restart!",
                    RestartSecTitle = "§c{0}",
                    RestartSecSubtl = "§c§lseconds until restart",
                    RestartAbort = "An important process is still running, can't restart. Trying again in 30 minutes",
                    MinutesWord = "minutes",
                    SecondsWord = "seconds",
                    RestartMsg = "§6{0} is going down for a scheduled restart",
                    LogLoad = "Plugin Loaded, next restart in {0} {1}, at {2}",
                    LogUnload = "Plugin Unloaded"
                }
            };
        }

        public void Initialize(IHost host)
        {
            Host = host;
            RConfig = host.LoadPluginConfiguration<RestartConfig>(this.GetType());

            using (StreamReader reader = new StreamReader(File.OpenRead(Path.Join(Path.GetDirectoryName(host.RunConfig.BdsBinPath), "server.properties"))))
                _worldName = Regex.Match(reader.ReadToEnd(), @"^level\-name\=(.+)", RegexOptions.Multiline).Groups[1].Value.Trim();

            bds = (ProcessManager)host.GetPluginByName("ProcessManager");
            backupManager = (BackupManager)host.GetPluginByName("BackupManager");
            renderManager = (RenderManager)host.GetPluginByName("RenderManager");
            bdsWatchdog = (Watchdog)host.GetPluginByName("Watchdog");
            vz = (VellumZero.VellumZero)host.GetPluginByName("VellumZero");

            // check if VZ plugin loaded and set up to use it if so
            if (vz != null)
            {
                vzEnabled = true;
                statusMessages = vz.vzConfig.ServerStatusMessages;
            }

            DateTime restartTime = DateTime.Parse(RConfig.AutoRestartTime);
            if (restartTime < DateTime.Now)
            {
                restartTime.AddDays(1);
            }
            double restartMins = restartTime.Subtract(DateTime.Now).TotalMinutes;

            if (autoRestartTimer != null) autoRestartTimer.Stop();
            autoRestartTimer = new Timer((restartMins) * 60000);
            autoRestartTimer.AutoReset = false;
            autoRestartTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
            {
                if (!backupManager.Processing && !renderManager.Processing)
                {
                    OnGoingDown();
                    bdsWatchdog.RetryLimit = 0;
                    bds.Close();
                    Log("Rebooting Server, waiting 10 seconds to be safe");
                    System.Threading.Thread.Sleep(10000);
                    bds.Start();
                    bdsWatchdog.RetryLimit = 3;
                    OnStartingUp();
                }
                else
                {
                    // if a backup or render is still going, abort restart and try again in 30 mins
                    RelayToServer(RConfig.TextStrings.RestartAbort);
                    Log(RConfig.TextStrings.RestartAbort);
                    autoRestartTimer.Interval = 1800 * 1000;
                    StartNotifyTimers(1800);
                }
            };

            /* - enable notifications for console-scheduled shutdown: need an event to be implemented in base vellum
            bds.OnShutdownScheduled += (object sender, ShutdownScheduledEventArgs e) =>
            {
                // if someone already ran "stop ##" in the console, doing it again doesn't overwrite the previous timer
                // so if this has already happened, don't redo anything here
                if (!alreadyStopping)
                {
                    StartNotifyTimers(e.Seconds);
                    alreadyStopping = true;
                }
            };
            */

            // set up shutdown messages
            StartNotifyTimers();
            autoRestartTimer.Start();


            // set up unexpected shutdown/crash handling
            bds.Process.Exited += (object sender, EventArgs e) =>
            {
                System.Threading.Thread.Sleep(1000); // give the watchdog a second to run the crashing hook if it's crashing
                if (!crashing) Unload();
            };
            ((IPlugin)bdsWatchdog).RegisterHook((byte)Watchdog.Hook.CRASH, (object sender, EventArgs e) => {
                crashing = true;
            });
            ((IPlugin)bdsWatchdog).RegisterHook((byte)Watchdog.Hook.LIMIT_REACHED, (object sender, EventArgs e) => {
                Unload();
            });
            ((IPlugin)bdsWatchdog).RegisterHook((byte)Watchdog.Hook.STABLE, (object sender, EventArgs e) => {
                crashing = false;
            });

            Log(String.Format(RConfig.TextStrings.LogLoad, (uint)restartMins, RConfig.TextStrings.MinutesWord, RConfig.AutoRestartTime));
        }

        public void RegisterHook(byte id, IPlugin.HookHandler callback)
        {
            if (!_hookCallbacks.ContainsKey(id))
            {
                _hookCallbacks.Add(id, callback);
            }
            else
            {
                _hookCallbacks[id] += callback;
            }
        }

        internal void CallHook(Hook hook, EventArgs e = null)
        {
            if (_hookCallbacks.ContainsKey((byte)hook))
                _hookCallbacks[(byte)hook]?.Invoke(this, e);
        }

        public void Unload()
        {
            StopTimers();
            Log(RConfig.TextStrings.LogUnload);
        }
        #endregion



        private void StartNotifyTimers(uint s = 0)
        {
            DateTime restartTime = DateTime.Parse(RConfig.AutoRestartTime);
            if (restartTime < DateTime.Now)
            {
                restartTime.AddDays(1);
            }
            double restartMins = restartTime.Subtract(DateTime.Now).TotalMinutes;

            // high visibility shutdown timers
            if (RConfig.HiVisShutdown)
            {
                uint timerMins;
                if (s > 0)
                {
                    timerMins = (s > 600) ? (s - 600) / 60 : 0;
                    msCountdown = (s > 600) ? 600000 : s * 1000;
                }
                else if (restartMins > 10)
                {
                    timerMins = (uint)restartMins - 10;
                    msCountdown = 600000;
                }
                else
                {
                    timerMins = 0;
                    msCountdown = (uint)restartMins * 60000;
                }
                // countdown for the warning messages to start
                if (hiVisWarnTimer != null) hiVisWarnTimer.Stop();
                hiVisWarnTimer = new Timer((timerMins * 60000) + 1);
                hiVisWarnTimer.AutoReset = false;
                hiVisWarnTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
                {
                    // repeating countdown for each warning message
                    if (hiVisWarnMsgs != null) hiVisWarnMsgs.Stop();
                    hiVisWarnMsgs = new Timer(1000);
                    hiVisWarnMsgs.AutoReset = true;
                    hiVisWarnMsgs.Elapsed += (object sender, ElapsedEventArgs e) =>
                    {
                        msCountdown -= 1000;
                        if ((msCountdown > 60500 && msCountdown % 60000 < 1000) || (msCountdown < 60500 && msCountdown > 10500))
                        {
                            Execute(String.Format("title @a actionbar " + RConfig.TextStrings.RestartMinWarn, (int)Math.Ceiling((decimal)msCountdown / 60000m)));
                        }
                        else if (msCountdown < 10500)
                        {
                            if (RConfig.TextStrings.RestartSecSubtl != "")
                                Execute(String.Format("title @a actionbar " + RConfig.TextStrings.RestartSecSubtl, (int)Math.Ceiling((decimal)msCountdown / 1000m)));
                            if (RConfig.TextStrings.RestartSecTitle != "")
                                Execute(String.Format("title @a title " + RConfig.TextStrings.RestartSecTitle, (int)Math.Ceiling((decimal)msCountdown / 1000m)));
                        }
                    };
                    hiVisWarnMsgs.Start();
                };
                hiVisWarnTimer.Start();
            }
            else
            {
                // normal warning message up to 5 mins out from restart
                double countdown = (s > 0) ? s : (restartMins > 5) ? 300 : restartMins * 60;
                string units = (countdown > 119) ? RConfig.TextStrings.MinutesWord : RConfig.TextStrings.SecondsWord;
                countdown = (units == RConfig.TextStrings.SecondsWord) ? countdown : countdown / 60;
                double timerTime = (restartMins > 5) ? restartMins - 5 : 0;

                if (normalWarnMsg != null) normalWarnMsg.Stop();
                normalWarnMsg = new Timer((timerTime * 60000) + 1);
                normalWarnMsg.AutoReset = false;
                normalWarnMsg.Elapsed += (object sender, ElapsedEventArgs e) =>
                {
                    RelayToServer(String.Format(RConfig.TextStrings.RestartOneWarn, countdown, units));
                };
                normalWarnMsg.Start();
            }
        }

        private void StopTimers()
        {
            if (autoRestartTimer != null) autoRestartTimer.Stop();
            if (hiVisWarnTimer != null) hiVisWarnTimer.Stop();
            if (hiVisWarnMsgs != null) hiVisWarnMsgs.Stop();
            if (normalWarnMsg != null) normalWarnMsg.Stop();
        }

        private void OnGoingDown()
        {
            CallHook(Hook.GOING_DOWN, EventArgs.Empty);

            if (statusMessages && RConfig.TextStrings.RestartMsg != "") Broadcast(String.Format(RConfig.TextStrings.RestartMsg, _worldName));

        }

        private void OnStartingUp()
        {
            CallHook(Hook.STARTING_UP, EventArgs.Empty);

            // set up for next restart
            DateTime restartTime = DateTime.Parse(RConfig.AutoRestartTime);
            if (restartTime < DateTime.Now)
            {
                restartTime.AddDays(1);
            }
            double restartMins = restartTime.Subtract(DateTime.Now).TotalMinutes;
            autoRestartTimer.Interval = restartMins * 60000;
            autoRestartTimer.Start();
        }

        // run a command on the server
        // if VZ exists, pass to it so it can use the bus, otherwise do it here
        private void Execute(string command)
        {
            if (vzEnabled) vz.Execute(command);
            else bds.SendInput(command); // this needs extra testing for character/encoding issues and if all in-game commands work this way
        }

        // messages meant for this server only
        // pass to VZ if it exists so it can use the bus, otherwise sendtellraw here
        private void RelayToServer(string message)
        {
            if (vzEnabled)
            {
                vz.RelayToServer(message);
            } else
            {
                SendTellraw(message);
            }
        }

        // messages that should be broadcast to discord and the bus if VZ is enabled
        // otherwise they fall back to SendTellraw() and only go to this server
        private void Broadcast(string message)
        {
            if (vzEnabled)
            {
                vz.Broadcast(message);
            } else
            {
                SendTellraw(message);
            }
        }

        // makes an announcement in game
        private void SendTellraw(string message)
        {
            message.Replace("\"", "'");
            bds.SendInput("/tellraw @a {\"rawtext\":[{\"text\":\"" + message + "\"}]}");
        }

        internal void Log(string line)
        {
            Console.WriteLine("[       AUTORESTART      ] " + line);
        }

    }
    public struct RestartConfig
    {
        public RestartStrings TextStrings;
        public string AutoRestartTime;
        public bool HiVisShutdown;
    }
    public struct RestartStrings
    {
        public string RestartMsg;
        public string RestartOneWarn;
        public string RestartMinWarn;
        public string RestartSecTitle;
        public string RestartSecSubtl;
        public string RestartAbort;
        public string MinutesWord;
        public string SecondsWord;
        public string LogLoad;
        public string LogUnload;
    }
}
