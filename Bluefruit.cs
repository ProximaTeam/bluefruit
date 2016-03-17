using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Proxima
{
    public class Bluefruit
    {
        #region Constructor
        public Bluefruit()
        {
            this.Timeout = 1000;
            this.StandardOutput = Console.Out;

            //Settings: https://learn.adafruit.com/introducing-adafruit-ble-bluetooth-low-energy-friend/terminal-settings
            this.SerialPort = new SerialPort();
            this.SerialPort.BaudRate = 9600;
            this.SerialPort.DataBits = 8;
            this.SerialPort.Parity = Parity.None;
            this.SerialPort.StopBits = StopBits.One;
            this.SerialPort.Handshake = Handshake.RequestToSend; //Hardware flow control
        }
        #endregion

        #region Public properties
        /// <summary>
        /// Gets or sets a value indicating whether to echo sent/received messages to the console.
        /// </summary>
        public bool Debug
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets timeout in milliseconds to wait for the response.
        /// </summary>
        public int Timeout
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the echo output, e.g. Console.Out
        /// </summary>
        /// <param name="writer"></param>
        public TextWriter StandardOutput
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the serial port instance.
        /// </summary>
        public SerialPort SerialPort
        {
            get;
            protected set;
        }
        #endregion

        #region Protected methods
        /// <summary>
        /// Converts short to hex string as required by the protocol.
        /// </summary>
        /// <param name="value">UInt16 value.</param>
        /// <returns>Gets a hex string</returns>
        protected string shorthex(ushort value)
        {
            return "0x" + value.ToString("X4");
            //return "0x" + ((byte)value).ToString("X2") + ((byte)(value >> 8)).ToString("X2"); //Reverse bytes
        }

        /// <summary>
        /// Converts byte array to hex string as defined by the Bluefruit AT protocol.
        /// </summary>
        /// <param name="data">Array or list of bytes.</param>
        /// <returns>Gets a hex string</returns>
        protected string hex(IEnumerable<byte> data)
        {
            var builder = new StringBuilder();
            foreach (byte d in data)
            {
                builder.Append(builder.Length > 0 ? "-" : "").Append(d.ToString("X2"));
            }
            return builder.ToString();
        }

        /// <summary>
        /// Converts hex string to bytes.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        protected IEnumerable<byte> bytes(string hex)
        {
            if (hex.Length % 2 == 1)
            {
                throw new ArgumentException("Expected string aligned to 2 bytes!");
            }

            for (int i = 0; i < hex.Length; i += 2)
            {
                yield return Convert.ToByte("" + hex[i + 0] + hex[i + 1], 16);
            }
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Opens the serial port.
        /// </summary>
        /// <param name="port">Serial port name, e.g. COM1</param>
        public void Open(string port)
        {
            this.SerialPort.PortName = port;
            this.SerialPort.Open();
        }

        /// <summary>
        /// Closes the serial port.
        /// </summary>
        public void Close()
        {
            if (this.SerialPort.IsOpen)
            {
                this.SerialPort.Close();
            }
        }

        /// <summary>
        /// Sends an AT command to the Bluefruit device and waits for the response.
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Returns raw AT response string, without "OK\n" or "ERROR\n" ending.</returns>
        public string Request(string command)
        {
            if (this.Debug && this.StandardOutput != null)
            {
                this.StandardOutput.WriteLine(command);
            }

            //Send the command
            this.SerialPort.WriteLine(command);

            //Read until OK or ERROR string
            bool reading = true;
            var builder = new StringBuilder();
            var timeout = DateTime.Now.AddMilliseconds(this.Timeout);
            while (reading)
            {
                while (this.SerialPort.BytesToRead > 0)
                {
                    var c = (char)this.SerialPort.ReadByte();
                    if (this.Debug && this.StandardOutput != null)
                    {
                        this.StandardOutput.Write(c);
                    }
                    builder.Append(c);
                    if (Regex.IsMatch(builder.ToString(), "(OK|ERROR)\r\n"))
                    {
                        reading = false;
                        break;
                    }
                }
                if (!reading)
                {
                    break;
                }
                if (DateTime.Now > timeout)
                {
                    throw new BluefruitTimeoutException("Timeout expired while waiting for response!");
                }
                Thread.Sleep(1);
            }

            var response = builder.ToString();

            //Check the response string
            string status = null;
            var match = Regex.Match(response, "(?<status>OK|ERROR)[\r\n]+$");
            if (match.Success)
            {
                status = match.Groups["status"].Value;
            }

            if (status == "ERROR")
            {
                throw new BluefruitException("ERROR in response!");
            }

            string result = Regex.Replace(response, "(\r\n)*(OK)(\r\n)*$", "");
            return result;
        }

        /// <summary>
        /// Sends the "AT" commands and throws an exception on timeout or error.
        /// </summary>
        public void Test()
        {
            Request("AT");
        }

        /// <summary>
        /// Gets an array of all available AT commands.
        /// </summary>
        /// <param name="enable"></param>
        public string[] Help()
        {
            return Request("AT+HELP").Split(',');
        }

        /// <summary>
        /// Forces the device to enter DFU mode for firmware update over the air.
        /// </summary>
        /// <param name="enable"></param>
        public void DFU()
        {
            Request("AT+DFU");
        }

        /// <summary>
        /// Sends the "ATZ" command to soft-reset the device.
        /// </summary>
        public void Reset()
        {
            Request("ATZ");
        }

        /// <summary>
        /// Sends the "AT+FACTORYRESET" command to reset the device to factory defaults.
        /// </summary>
        public void FactoryReset()
        {
            Request("AT+FACTORYRESET");
        }

        /// <summary>
        /// Gets a value indicating whether echo of input characters is enabled.
        /// </summary>
        /// <returns></returns>
        public bool GetEcho()
        {
            return Request("ATE") == "1";
        }

        /// <summary>
        /// Sets a value indicating whether echo of input characters is enabled.
        /// </summary>
        /// <param name="enable"></param>
        public void SetEcho(bool enable)
        {
            Request("ATE=" + (enable ? "1" : "0" ));
        }

        /// <summary>
        /// Sends the "ATI" command to get the basic device information, i.e. board name, serial number, firmware version, etc.
        /// </summary>
        /// <returns></returns>
        public string GetDeviceInfo()
        {
            return Request("ATI");
        }

        /// <summary>
        /// Gets the device friendly name.
        /// </summary>
        /// <returns></returns>
        public string GetDeviceName()
        {
            return Request("AT+GAPDEVNAME");
        }

        /// <summary>
        /// Sets the device friendly name.
        /// </summary>
        /// <param name="enable"></param>
        public void SetDeviceName(string name)
        {
            Request("AT+GAPDEVNAME=" + name);
        }

        /// <summary>
        /// Starts transmitting advertising packets.
        /// </summary>
        public void StartAdvertising()
        {
            Request("AT+GAPSTARTADV");
        }

        /// <summary>
        /// Stops transmitting advertising packets.
        /// </summary>
        public void StopAdvertising()
        {
            Request("AT+GAPSTOPADV");
        }

        /// <summary>
        /// Sets advertising packet payload.
        /// </summary>
        /// <param name="data"></param>
        public void SetAdvertisingData(IEnumerable<byte> data)
        {
            Request("AT+GAPSETADVDATA=" + hex(data));
        }

        /// <summary>
        /// Sets advertising packet payload.
        /// </summary>
        /// <param name="hex"></param>
        public void SetAdvertisingData(string hex)
        {
            SetAdvertisingData(bytes(hex));
        }

        /// <summary>
        /// Gets connection and advertising intervals. Values in milliseconds.
        /// </summary>
        public int?[] GetAdvertisingIntervals()
        {
            string response = Request("AT+GAPINTERVALS");

            var result = new int?[4];
            var values = response.Split(',');
            for (int i = 0; i < 4; i++)
            {
                int numeric = -1;
                if (int.TryParse(values[i], out numeric))
                {
                    result[i] = numeric;
                }
            }

            return result;
        }

        /// <summary>
        /// Sets connection and advertising intervals. Values in milliseconds.
        /// </summary>
        public void SetAdvertisingIntervals(int? minConnectionInterval, int? maxConnectionInterval, int? advInterval, int? advTimeout)
        {
            Request("AT+GAPINTERVALS="
                + (minConnectionInterval != null ? minConnectionInterval.Value.ToString() : "") + ","
                + (maxConnectionInterval != null ? maxConnectionInterval.Value.ToString() : "") + ","
                + (advInterval != null ? advInterval.Value.ToString() : "") + ","
                + (advTimeout != null ? advTimeout.Value.ToString() : ""));
        }

        public string AddGATTService(string uuid)
        {
            return Request("AT+GATTADDSERVICE=UUID=" + uuid);
        }

        public string AddGATTCharacteristic(string uuid, string properties, int? minLength, int? maxLength, IEnumerable<byte> value)
        {
            return Request("AT+GATTADDCHAR=UUID=" + uuid + ", PROPERTIES=" + properties + (minLength != null ? ", MIN_LEN=" + minLength.Value : "") + (maxLength != null ? ", MAX_LEN=" + maxLength.Value : "") + (value != null ? ", VALUE=" + hex(value) : ""));
        }

        public void SetBLEBeacon(ushort manufacturerId, IEnumerable<byte> uuid, ushort major, ushort minor, short rssi)
        {
            Request("AT+BLEBEACON=" + shorthex(manufacturerId) + "," + hex(uuid) + "," + shorthex(major) + "," + shorthex(minor) + "," + rssi);
        }

        public void SetBLEBeacon(ushort manufacturerId, string uuid, ushort major, ushort minor, short rssi)
        {
            SetBLEBeacon(manufacturerId, bytes(uuid), major, minor, rssi);
        }

        public void SetBLEBeacon(string uuid, ushort major, ushort minor)
        {
            SetBLEBeacon(0x004C, uuid, major, minor, -59);
        }

        public void SetBLEURIBeacon(string uri)
        {
            Request("AT+BLEURIBEACON=" + uri);
        }

        public void SetEddystoneEnable(bool enable)
        {
            Request("AT+EDDYSTONEENABLE=" + (enable ? 1 : 0));
        }

        public bool GetEddystoneEnable()
        {
            var result = Request("AT+EDDYSTONEENABLE");
            return int.Parse(result) == 1;
        }

        public void SetEddystoneURL(string url)
        {
            SetEddystoneURL(url, false);
        }

        public void SetEddystoneURL(string url, bool adv)
        {
            Request("AT+EDDYSTONEURL=" + url + (adv ? ",1" : ""));
        }

        public void SetEddystoneConfig(int seconds)
        {
            Request("AT+EDDYSTONECONFIGEN=" + seconds);
        }

        public BLEAddressType GetBLEAddressType()
        {
            var result = Request("AT+BLEGETADDRTYPE");
            return (BLEAddressType)int.Parse(result);
        }

        public string GetBLEAddress()
        {
            return Request("AT+BLEGETADDR");
        }

        public string GetBLEPeerAddress()
        {
            return Request("AT+BLEGETPEERADDR");
        }

        public int GetBLERSSI()
        {
            var result = Request("AT+BLEGETRSSI");
            return int.Parse(result);
        }

        public int GetBLEPowerLevel()
        {
            var result = Request("AT+BLEPOWERLEVEL");
            return int.Parse(result);
        }

        public void SetBLEPowerLevel(int value)
        {
            Request("AT+BLEPOWERLEVEL=" + value);
        }

        /// <summary>
        /// Gets a value on the specified ADC pin.
        /// </summary>
        /// <param name="pin">ADC channel 0-7</param>
        /// <returns>Returns value of ADC conversion.</returns>
        public int GetADCPinValue(int pin)
        {
            return int.Parse(Request("AT+HWADC=" + pin));
        }

        /// <summary>
        /// Gets the state of the specified GPIO pin.
        /// </summary>
        /// <param name="pin">GPIO pin number</param>
        /// <returns>Returns HIGH (true) or LOW (false) value.</returns>
        public bool GetGPIOPinValue(int pin)
        {
            return int.Parse(Request("AT+HWGPIO=" + pin)) == 1;
        }

        /// <summary>
        /// Sets the state of the specified GPIO pin.
        /// </summary>
        /// <param name="pin">GPIO pin number</param>
        public void SetGPIOPinValue(int pin, bool state)
        {
            Request("AT+HWGPIO=" + pin + "," + (state ? "1" : "0"));
        }

        /// <summary>
        /// Gets the mode of the specified GPIO pin.
        /// </summary>
        /// <param name="pin">GPIO pin number</param>
        /// <returns>Returns GPIO mode.</returns>
        public GPIOMode GetGPIOPinMode(int pin)
        {
            return (GPIOMode)int.Parse(Request("AT+HWGPIOMODE=" + pin));
        }

        /// <summary>
        /// Sets the mode of the specified GPIO pin.
        /// </summary>
        /// <param name="pin">GPIO pin number</param>
        public void SetGPIOPinMode(int pin, GPIOMode mode)
        {
            Request("AT+HWGPIOMODE=" + pin + "," + (int)mode);
        }

        /// <summary>
        /// Gets the temperature of BLE module's die.
        /// </summary>
        /// <returns>Returns temperature value in degrees Celsius.</returns>
        public double GetDieTemperature()
        {
            return double.Parse(Request("AT+HWGETDIETEMP"), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Scans for I2C devices.
        /// </summary>
        /// <returns>Returns an array of I2C hex addresses.</returns>
        public string[] ScanI2C()
        {
            return Request("AT+HWI2CSCAN").Split(',');
        }

        /// <summary>
        /// Gets the power supply voltage level. Value in millivolts.
        /// </summary>
        /// <returns>Returns value in millivolts.</returns>
        public int GetPowerSupplyVoltage()
        {
            return int.Parse(Request("AT+HWBAT"));
        }

        /// <summary>
        /// Generates a random 32-bit number.
        /// </summary>
        /// <returns>Returns a hex value.</returns>
        public string GetRandomValue()
        {
            return Request("AT+HWRANDOM");
        }

        /// <summary>
        /// Gets the behaviour of MODE LED.
        /// </summary>
        /// <returns>Returns MODE LED behaviour.</returns>
        public LEDMode GetLEDMode()
        {
            string result = Request("AT+HWMODELED");
            switch (result.ToLower())
            {
                case "0": case "disable":
                    return LEDMode.Disable;

                case "1": case "mode":
                    return LEDMode.Mode;

                case "2": case "hwuart":
                    return LEDMode.HWUART;

                case "3": case "bleuart":
                    return LEDMode.BLEUART;

                case "4": case "spi":
                    return LEDMode.SPI;

                case "5": case "manual":
                    return LEDMode.Manual;
            }

            throw new BluefruitException("Unknown LED mode: " + result);
        }

        /// <summary>
        /// Sets the behaviour of MODE LED.
        /// </summary>
        public void SetLEDMode(LEDMode mode)
        {
            Request("AT+HWMODELED=" + mode.ToString().ToUpper());
        }

        /// <summary>
        /// Sets the behaviour of MODE LED.
        /// </summary>
        public void SetLEDMode(LEDMode mode, LEDModeManual manual)
        {
            Request("AT+HWMODELED=" + mode.ToString().ToUpper() + "," + manual.ToString().ToUpper());
        }

        /// <summary>
        /// Switches between the DATA and COMMAND mode.
        /// </summary>
        /// <returns></returns>
        public string ToggleDataMode()
        {
            return Request("+++");
        }
        #endregion
    }

    public class BluefruitException : Exception
    {
        public BluefruitException(string message) : base(message)
        {
        }
    }

    public class BluefruitTimeoutException : BluefruitException
    {
        public BluefruitTimeoutException(string message) : base(message)
        {
        }
    }

    public enum GPIOMode
    {
        Input = 0,
        Output = 1,
        InputPullup = 2,
        OutputPullup = 3
    }

    public enum LEDMode
    {
        Disable = 0,
        Mode = 1,
        HWUART = 2,
        BLEUART = 3,
        SPI = 4,
        Manual = 5
    }

    public enum LEDModeManual
    {
        Off,
        On,
        Toggle
    }

    public enum BLEAddressType
    {
        Public = 0,
        Random = 1
    }
}
