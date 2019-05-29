using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SLOBSharp.Client;
using SLOBSharp.Client.Requests;
using streamdeck_client_csharp;
using streamdeck_client_csharp.Events;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlobsAudio
{
    class Program
    {
        private static Dictionary<String, String> _AudioNames = new Dictionary<string, string>();
        private static Dictionary<String,double> _AudioResources = new Dictionary<String, double>();
        private static List<double> _DBLookup = new List<double>() { -46.7, -36.7, -30.0, -25.0, -20.0, -15.6, -11.2, -7.2, -3.6, 0 };

        private static SlobsClient _client;
        private static StreamDeckConnection connection;
        private const bool ENABLE_LOG = true;
        
        public class Options
        {
            [Option("port", Required = true, HelpText = "The websocket port to connect to", SetName = "port")]
            public int Port { get; set; }

            [Option("pluginUUID", Required = true, HelpText = "The UUID of the plugin")]
            public string PluginUUID { get; set; }

            [Option("registerEvent", Required = true, HelpText = "The event triggered when the plugin is registered?")]
            public string RegisterEvent { get; set; }

            [Option("info", Required = true, HelpText = "Extra JSON launch data")]
            public string Info { get; set; }
        }

        private static void SetupDevices(IEnumerable<SLOBSharp.Client.Responses.SlobsResult> slobsAudioDevices)
        {
            foreach (var device in slobsAudioDevices)
            {
                //0 is max, -114/null is min
                //setting the deflection goes up in 10% by double so 0.1, 0,2 etc
                //AudioResources.Add(device.ResourceId, )
                //0: null, 0.1: -46.7, 0.2: - -36.7, 0.3: -30.0, 0.4: -25.0, 0.5: -20.0: 0.6: -15.6, 0.7: -11.2, 0.8: -7.2, 0.9: -3.6, 1.0: 0 
                double scale = 0;
                if (device.Fader.DB == null || device.Fader.DB < _DBLookup[0])
                    scale = 0.1;
                else if (device.Fader.DB < _DBLookup[1])
                    scale = 0.2;
                else if (device.Fader.DB < _DBLookup[2])
                    scale = 0.3;
                else if (device.Fader.DB < _DBLookup[3])
                    scale = 0.4;
                else if (device.Fader.DB < _DBLookup[4])
                    scale = 0.5;
                else if (device.Fader.DB < _DBLookup[5])
                    scale = 0.6;
                else if (device.Fader.DB < _DBLookup[7])
                    scale = 0.7;
                else if (device.Fader.DB < _DBLookup[8])
                    scale = 0.9;
                else
                    scale = 1;
                _AudioResources.Add(device.ResourceId, scale);
                _AudioNames.Add(device.Name, device.ResourceId);
            }
        }

        private static void IncreaseVolume(string devID, string context)
        {
            lock (_AudioResources)
            {
                LogMessage("volume up", $"acquired lock");
                var newVal = (_AudioResources[devID] + 0.1) > 1 ? 1 : _AudioResources[devID] + 0.1;
                LogMessage("volume up", $"got new val {newVal}");
                var slobsRequest = SlobsRequestBuilder.NewRequest().SetMethod("setDeflection").SetResource(devID).AddArgs(newVal).BuildRequest();
                LogMessage("volume up", $"build request");
                var slobsRpcResponse = Task.Run(async () => await _client.ExecuteRequestAsync(slobsRequest).ConfigureAwait(false)).Result;
                LogMessage("volume up", $"request sent");
                if (slobsRpcResponse.Error == null)
                {
                    LogMessage("volume up", $"success");
                    _AudioResources[devID] = newVal;
                }
                else
                    LogError("volume up", slobsRpcResponse.Error.Message);
            }
        }
        private static void DecreaseVolume(string devID, string context)
        {         
            lock (_AudioResources)
            {
                LogMessage("volume up", $"acquired lock");
                var newVal = (_AudioResources[devID] - 0.1) < 0 ? 0 : _AudioResources[devID] - 0.1;
                LogMessage("volume up", $"got new val {newVal}");
                var slobsRequest = SlobsRequestBuilder.NewRequest().SetMethod("setDeflection").SetResource(devID).AddArgs(newVal).BuildRequest();
                LogMessage("volume up", $"build request");
                var slobsRpcResponse = Task.Run(async () => await _client.ExecuteRequestAsync(slobsRequest).ConfigureAwait(false)).Result;
                LogMessage("volume up", $"request sent");
                if (slobsRpcResponse.Error == null)
                {
                    LogMessage("volume up", $"success");
                    _AudioResources[devID] = newVal;
                } else
                    LogError("volume down", slobsRpcResponse.Error.Message);
            }
        }

        protected static bool IsFileLocked(string fileName)
        {
            FileInfo file = new FileInfo(fileName);
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        private static void LogError(string context, string error)
        {
            if (!ENABLE_LOG) return;
            while (IsFileLocked("logfile.log"));
            var text = File.ReadAllLines("logfile.log").ToList();
            text.Add($"{DateTime.Now}: ERROR: {context} - {error}");
            File.WriteAllLines("logfile.log", text);
        }

        private static void LogMessage(string context, string message)
        {
            if (!ENABLE_LOG) return;
            while (IsFileLocked("logfile.log"));
            var text = File.ReadAllLines("logfile.log").ToList();
            text.Add($"{DateTime.Now}: Message: {context} - {message}");
            File.WriteAllLines("logfile.log", text);
        }

        // StreamDeck launches the plugin with these details
        // -port [number] -pluginUUID [GUID] -registerEvent [string?] -info [json]
        static void Main(string[] args)
        {
            // Uncomment this line of code to allow for debugging
            //while (!System.Diagnostics.Debugger.IsAttached) { System.Threading.Thread.Sleep(100); }
            var connected = false;
            var attemptNum = 0;
            if (!File.Exists("logfile.log"))
            {
                File.Create("logfile.log");
            }
            while (!connected) {
                try
                {
                    attemptNum++;
                    _client = new SlobsPipeClient("slobs");
                    LogMessage("init", "create client");
                    var slobsRequest = SlobsRequestBuilder.NewRequest().SetMethod("getSources").SetResource("AudioService").BuildRequest();
                    LogMessage("init", "build request");
                    var slobsRpcResponse = Task.Run(async () => await _client.ExecuteRequestAsync(slobsRequest).ConfigureAwait(false)).Result;
                    LogMessage("init", "send request");
                    var slobsAudioDevices = slobsRpcResponse.Result;
                    LogMessage("init", "get result");
                    SetupDevices(slobsAudioDevices);
                    connected = true;
                } catch (Exception e)
                {
                    LogError("init", $"attempt {attemptNum}: {e.Message}");
                }
            }

            // The command line args parser expects all args to use `--`, so, let's append
            for (int count = 0; count < args.Length; count++)
            {
                if (args[count].StartsWith("-") && !args[count].StartsWith("--"))
                {
                    args[count] = $"-{args[count]}";
                }
            }

            Parser parser = new Parser((with) =>
            {
                with.EnableDashDash = true;
                with.CaseInsensitiveEnumValues = true;
                with.CaseSensitive = false;
                with.IgnoreUnknownArguments = true;
                with.HelpWriter = Console.Error;
            });

            ParserResult<Options> options = parser.ParseArguments<Options>(args);
            options.WithParsed<Options>(o => RunPlugin(o));
        }

        static void RunPlugin(Options options)
        {
            ManualResetEvent connectEvent = new ManualResetEvent(false);
            ManualResetEvent disconnectEvent = new ManualResetEvent(false);

            connection = new StreamDeckConnection(options.Port, options.PluginUUID, options.RegisterEvent);

            connection.OnConnected += (sender, args) =>
            {
                connectEvent.Set();
            };

            connection.OnDisconnected += (sender, args) =>
            {
                disconnectEvent.Set();
            };

            connection.OnApplicationDidLaunch += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"App Launch: {args.Event.Payload.Application}");
            };

            connection.OnApplicationDidTerminate += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"App Terminate: {args.Event.Payload.Application}");
            };

            Dictionary<string, int> counters = new Dictionary<string, int>();
            List<string> images = new List<string>();
            Dictionary<string, JObject> settings = new Dictionary<string, JObject>();
            connection.OnWillAppear += (sender, args) =>
            {
                JObject settingsStruct;
                switch (args.Event.Action)
                {
                    case "com.aallen.slobsvolume.volumeup":
                        if (!settings.TryGetValue(args.Event.Context, out settingsStruct))
                        {
                            LogMessage("keydown", $"couldn't find the context, request settings and return");
                            connection.GetSettingsAsync(args.Event.Context);
                            return;
                        }
                        break;
                    case "com.aallen.slobsvolume.volumedown":
                        if (!settings.TryGetValue(args.Event.Context, out settingsStruct))
                        {
                            LogMessage("keydown", $"couldn't find the context, request settings and spin");
                            connection.GetSettingsAsync(args.Event.Context);
                            return;
                        }
                        break;
                }
            };

            connection.OnKeyDown += (sender, args) =>
            {
                try
                {
                    string deviceID;
                    JObject settingsStruct;
                    LogMessage("keydown", "key pressed");
                    switch (args.Event.Action)
                    {
                        case "com.aallen.slobsvolume.volumeup":
                            LogMessage("keydown", "volume up event");
                            if (!settings.TryGetValue(args.Event.Context, out settingsStruct))
                            {
                                LogMessage("keydown", $"couldn't find the context, request settings and return");
                                connection.GetSettingsAsync(args.Event.Context);
                                return;
                            }
                            deviceID = settings[args.Event.Context]["deviceName"].ToString();
                            LogMessage("keydown", $"got device ID {deviceID}");
                            IncreaseVolume(deviceID, args.Event.Context);
                            break;
                        case "com.aallen.slobsvolume.volumedown":
                            LogMessage("keydown", "volume down event");
                            if (!settings.TryGetValue(args.Event.Context, out settingsStruct))
                            {
                                LogMessage("keydown", $"couldn't find the context, request settings and return");
                                connection.GetSettingsAsync(args.Event.Context);
                                return;
                            }
                            deviceID = settings[args.Event.Context]["deviceName"].ToString();
                            LogMessage("keydown", $"got device ID {deviceID}");
                            DecreaseVolume(deviceID, args.Event.Context);
                            break;
                    }
                } catch (Exception e)
                {
                    LogError("keydown", e.Message);
                }
            };

            /*connection.OnKeyUp += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"KeyDown");
            };*/

            connection.OnDidReceiveSettings += (sender, args) => {             
                lock (settings)
                {
                    settings[args.Event.Context] = args.Event.Payload.Settings;
                }
   
            };

            /*connection.OnWillDisappear += (sender, args) =>
            {
                lock (counters)
                {
                    if (counters.ContainsKey(args.Event.Context))
                    {
                        counters.Remove(args.Event.Context);
                    }
                }

                lock (images)
                {
                    if (images.Contains(args.Event.Context))
                    {
                        images.Remove(args.Event.Context);
                    }
                }

                lock (settings)
                {
                    if (settings.ContainsKey(args.Event.Context))
                    {
                        settings.Remove(args.Event.Context);
                    }
                }
            };*/

            // Start the connection
            connection.Run();


            // Wait for up to 10 seconds to connect
            if (connectEvent.WaitOne(TimeSpan.FromSeconds(10)))
            {                
                // We connected, loop every second until we disconnect
                while (!disconnectEvent.WaitOne(TimeSpan.FromMilliseconds(1000)))
                {                    
                }
            }
        }
    }
}
