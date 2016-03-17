using System;
using System.IO.Ports;
using System.Threading;

namespace Proxima
{
    public class Program
    {
        public const int EXIT_SUCCESS = 0;
        public const int EXIT_ERROR = 1;
        public const int EXIT_ERROR_ARGS = 2;
        public const int EXIT_ERROR_PORT = 3;

        public static int Main(string[] args)
        {
            //Show help message if no arguments
            if (args.Length == 0)
            {
                Usage();
                return EXIT_ERROR;
            }

            //Command line arguments as variables
            string at = null;
            string next = null;
            string port = null;
            string name = null;
            string uuid = null;
            ushort major = 0;
            ushort minor = 0;
            int power = 0;
            bool done = false;
            bool echo = false;
            bool test = false;
            bool info = false;
            bool rssi = false;
            bool reset = false;
            bool verbose = false;
            bool factoryReset = false;
            var powermode = ConfigMode.None;
            var echomode = EchoMode.None;
            string eddystoneUrl = null;
            bool deviceAddress = false;
            bool peerAddress = false;

            try
            {
                //Parse command line arguments
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    switch (arg)
                    {
                        case "--port":
                            port = args[++i];
                            if (port.ToLower() == "auto")
                            {
                                port = null;
                            }
                            break;

                        case "--uuid":
                            uuid = args[++i];
                            break;

                        case "--major":
                            major = (ushort)int.Parse(args[++i]);
                            break;

                        case "--minor":
                            minor = (ushort)int.Parse(args[++i]);
                            break;

                        case "--at":
                            at = args[++i];
                            break;

                        case "--name":
                            name = args[++i];
                            break;

                        case "--power":
                            next = (i + 1 < args.Length ? args[i + 1] : null);
                            if (next != null && next.StartsWith("--"))
                            {
                                next = null;
                            }
                            if (next != null)
                            {
                                powermode = ConfigMode.Write;
                                if (next == "min")
                                {
                                    power = -20;
                                }
                                else if (next == "max")
                                {
                                    power = 4;
                                }
                                else power = int.Parse(next);
                                i++;
                            }
                            else
                            {
                                powermode = ConfigMode.Read;
                            }
                            break;

                        case "--echo":
                            next = (i + 1 < args.Length ? args[i + 1] : null);
                            if (next != null && next.StartsWith("--"))
                            {
                                next = null;
                            }
                            if (next != null)
                            {
                                if (next == "on")
                                {
                                    echomode = EchoMode.On;
                                }
                                else if (next == "off")
                                {
                                    echomode = EchoMode.Off;
                                }
                                i++;
                            }
                            else
                            {
                                echomode = EchoMode.Read;
                            }
                            break;

                        /*
                        case "--eddystone-id":
                            eddystoneId = args[++i];
                            break;
                        */

                        case "--eddystone-url":
                            eddystoneUrl = args[++i];
                            break;

                        case "--device-address":
                            deviceAddress = true;
                            break;

                        case "--peer-address":
                            peerAddress = true;
                            break;

                        case "--rssi":
                            rssi = true;
                            break;

                        case "--info":
                            info = true;
                            break;

                        case "--test":
                            test = true;
                            break;

                        case "--reset":
                            reset = true;
                            break;

                        case "--factory-reset":
                            factoryReset = true;
                            break;

                        case "--verbose":
                            verbose = true;
                            break;

                        default:
                        case "-h":
                        case "--help":
                            Usage();
                            return EXIT_SUCCESS;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return EXIT_ERROR_ARGS;
            }

            var ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Console.Error.WriteLine("No serial port available!");
                return EXIT_ERROR_PORT;
            }

            if (port == null)
            {
                port = ports[0];
            }

            try
            {
                var bluefruit = new Bluefruit();
                bluefruit.Debug = verbose;
                bluefruit.Open(port);

                Thread.Sleep(30);

                switch (echomode)
                {
                    case EchoMode.Read:
                        echo = bluefruit.GetEcho();
                        if (!verbose) Console.WriteLine(echo ? 1 : 0);
                        done = true;
                        break;

                    case EchoMode.On:
                        bluefruit.SetEcho(true);
                        done = true;
                        break;

                    case EchoMode.Off:
                        bluefruit.SetEcho(false);
                        done = true;
                        break;
                }

                if (!done && test)
                {
                    bluefruit.Test();
                    done = true;
                }

                if (!done && info)
                {
                    string result = bluefruit.GetDeviceInfo();
                    if (!verbose) Console.WriteLine(result);
                    done = true;
                }

                if (!done)
                {
                    switch (powermode)
                    {
                        case ConfigMode.Read:
                            power = bluefruit.GetBLEPowerLevel();
                            if (!verbose) Console.WriteLine(power);
                            done = true;
                            break;

                        case ConfigMode.Write:
                            bluefruit.SetBLEPowerLevel(power);
                            done = true;
                            break;
                    }
                }

                //Change BLE device name
                if (!done && name != null)
                {
                    bluefruit.SetDeviceName(name);
                    done = true;
                }

                //Start Eddystone URL advertising
                if (!done && eddystoneUrl != null)
                {
                    bluefruit.SetEddystoneEnable(true);
                    bluefruit.SetEddystoneConfig(300); //300s = 5min advertising
                    bluefruit.SetEddystoneURL(eddystoneUrl);
                    done = true;
                }

                //Start iBeacon advertising
                if (!done && uuid != null)
                {
                    bluefruit.SetEddystoneEnable(false);
                    bluefruit.SetBLEBeacon(uuid, major, minor);
                    done = true;
                }

                //Get device BLE address
                if (!done && deviceAddress)
                {
                    string result = bluefruit.GetBLEAddress();
                    if (!verbose) Console.WriteLine(result);
                    done = true;
                }

                //Get connected peer (central) BLE address
                if (!done && peerAddress)
                {
                    string result = bluefruit.GetBLEPeerAddress();
                    if (!verbose) Console.WriteLine(result);
                    done = true;
                }

                //Get RSSI in dBm
                if (!done && rssi)
                {
                    int result = bluefruit.GetBLERSSI();
                    if (!verbose) Console.WriteLine(result);
                    done = true;
                }

                //Reset the device
                if (!done && reset)
                {
                    bluefruit.Reset();
                    done = true;
                }

                //Reset to factory defaults
                if (!done && factoryReset)
                {
                    bluefruit.FactoryReset();
                    done = true;
                }

                //Send AT command string
                if (!done && at != null)
                {
                    string result = bluefruit.Request(at);
                    if (!verbose) Console.WriteLine(result);
                    done = true;
                }

                if (!done)
                {
                    Usage();
                    done = true;
                }

                bluefruit.Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return EXIT_ERROR;
            }

            return EXIT_SUCCESS;
        }

        public static void Usage()
        {
            var an = typeof(Program).Assembly.GetName();
            string name = an.Name;
            string version = string.Format("{0}.{1}.{2}", an.Version.Major, an.Version.Minor, an.Version.Build);

            Console.WriteLine("Bluefruit v" + version);
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("  " + name + " [options]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  --help                   This message");
            Console.WriteLine("  --verbose                Verbose mode");
            Console.WriteLine("  --port [COMx]            COM port name or 'auto'");
            Console.WriteLine("  --at [string]            AT command string");
            Console.WriteLine("  --uuid [uuid]            iBeacon UUID");
            Console.WriteLine("  --major [value]          iBeacon major");
            Console.WriteLine("  --minor [value]          iBeacon minor");
            Console.WriteLine("  --name [string]          Friendly device name");
            Console.WriteLine("  --eddystone-url [url]    Set Eddystone URL advertising");
            Console.WriteLine("  --device-address         Get device BLE address");
            Console.WriteLine("  --peer-address           Get connected peer BLE address");
            Console.WriteLine("  --rssi                   Get RSSI in dBm");
            Console.WriteLine("  --power [value]          Set power level in dBm or 'min' or 'max'");
            Console.WriteLine("  --power                  Get power level");
            Console.WriteLine("  --echo [on|off]          Set echo on or off");
            Console.WriteLine("  --echo                   Get echo flag");
            Console.WriteLine("  --test                   Test if device ready");
            Console.WriteLine("  --info                   Read device info");
            Console.WriteLine("  --reset                  Reset the device");
            Console.WriteLine("  --factory-reset          Reset to factory defaults");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  " + name + " --port COM7 --verbose --at AT+HELP");
            Console.WriteLine("  " + name + " --uuid f7826da64fa24e988024bc5b71e08901 --major 11061 --minor 425");
            Console.WriteLine("  " + name + " --port auto --power max --name \"Bluefruit 360\"");
            Console.WriteLine("  " + name + " --verbose --info");
            Console.WriteLine("");
            Console.WriteLine("List of AT commands:");
            Console.WriteLine("https://learn.adafruit.com/introducing-adafruit-ble-bluetooth-low-energy-friend?view=all");
            Console.WriteLine("");

            var ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Console.WriteLine("No serial port available!");
            }
            else
            {
                Console.WriteLine("Available serial ports:");
                foreach (var port in ports)
                {
                    Console.WriteLine("  " + port);
                }
            }
        }
    }

    public enum ConfigMode
    {
        None,
        Read,
        Write
    }

    public enum EchoMode
    {
        None,
        Read,
        On,
        Off
    }
}
