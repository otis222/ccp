using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Linq;
using UnityEngine;

namespace ArduinoUnityGame
{
    public sealed class SerialInputReader : MonoBehaviour
    {
        private static readonly char[] TokenSeparators = { ';', ',', ' ', '\t' };

        [SerializeField] private string portName = "COM7";
        [SerializeField] private int baudRate = 9600;
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private bool autoReconnect = false;
        [SerializeField] private float reconnectInterval = 2f;
        [SerializeField] private float axisDeadZone = 0.08f;

        private readonly object queueLock = new object();
        private readonly object portLock = new object();
        private readonly object statusLock = new object();
        private readonly Queue<string> pendingLines = new Queue<string>();

        private object serialPort;
        private Type serialPortType;
        private MethodInfo readLineMethod;
        private MethodInfo writeLineMethod;
        private MethodInfo closeMethod;
        private MethodInfo disposeMethod;
        private Thread readerThread;
        private volatile bool threadRunning;
        private volatile bool connected;
        private string threadStatus = string.Empty;
        private float nextReconnectTime;

        public string PortName
        {
            get { return portName; }
            set { portName = string.IsNullOrWhiteSpace(value) ? "COM3" : value.Trim(); }
        }

        public bool IsConnected
        {
            get { return connected; }
        }

        public string Status { get; private set; } = "Serial idle";
        public string LastLine { get; private set; } = string.Empty;
        public float Axis { get; private set; }
        public float Pot01 { get; private set; } = 0.5f;
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool DashPressed { get; private set; }
        public bool DashHeld { get; private set; }

        private void Start()
        {
            portName = ResolveStartupPort();

            if (connectOnStart)
            {
                Connect(portName);
            }
        }

        private void Update()
        {
            JumpPressed = false;
            DashPressed = false;

            string latestThreadStatus = TakeThreadStatus();
            if (!string.IsNullOrEmpty(latestThreadStatus))
            {
                Status = latestThreadStatus;
            }

            string line;
            while (TryDequeueLine(out line))
            {
                ParseLine(line);
            }

            if (autoReconnect && !connected && Time.unscaledTime >= nextReconnectTime)
            {
                nextReconnectTime = Time.unscaledTime + reconnectInterval;
                Connect(portName);
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void Connect(string requestedPort)
        {
            if (!string.IsNullOrWhiteSpace(requestedPort))
            {
                portName = requestedPort.Trim();
            }

            PlayerPrefs.SetString("SerialStarRunner.Port", portName);
            PlayerPrefs.Save();
            Disconnect();

            serialPortType = FindSerialPortType();
            if (serialPortType == null)
            {
                Status = "Serial unavailable: System.IO.Ports was not found. Keyboard fallback is active.";
                connected = false;
                return;
            }

            try
            {
                serialPort = Activator.CreateInstance(serialPortType, new object[] { portName, baudRate });
                closeMethod = serialPortType.GetMethod("Close", Type.EmptyTypes);
                disposeMethod = serialPortType.GetMethod("Dispose", Type.EmptyTypes);
                SetPortProperty("ReadTimeout", 100);
                SetPortProperty("WriteTimeout", 100);
                SetPortProperty("DtrEnable", true);
                SetPortProperty("RtsEnable", true);

                serialPortType.GetMethod("Open", Type.EmptyTypes).Invoke(serialPort, null);
                readLineMethod = serialPortType.GetMethod("ReadLine", Type.EmptyTypes);
                writeLineMethod = serialPortType.GetMethod("WriteLine", new[] { typeof(string) });

                threadRunning = true;
                connected = true;
                Status = "Serial connected: " + portName;
                readerThread = new Thread(ReadLoop);
                readerThread.IsBackground = true;
                readerThread.Name = "Serial Star Runner Reader";
                readerThread.Start();
            }
            catch (Exception exception)
            {
                connected = false;
                Status = "Serial failed on " + portName + ": " + Unwrap(exception).Message;
                CleanupPort();
            }
        }

        public void Disconnect()
        {
            threadRunning = false;

            if (readerThread != null && readerThread.IsAlive)
            {
                readerThread.Join(150);
            }

            readerThread = null;
            connected = false;
            CleanupPort();
        }

        public void SendToArduino(string message)
        {
            if (!connected || serialPort == null || writeLineMethod == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            try
            {
                lock (portLock)
                {
                    writeLineMethod.Invoke(serialPort, new object[] { message });
                }
            }
            catch (Exception exception)
            {
                connected = false;
                Status = "Serial write failed: " + Unwrap(exception).Message;
            }
        }

        public static string[] GetAvailablePortNames()
        {
            Type type = FindSerialPortType();
            if (type == null)
            {
                return new string[0];
            }

            try
            {
                MethodInfo getPortNames = type.GetMethod("GetPortNames", BindingFlags.Public | BindingFlags.Static);
                return getPortNames == null ? new string[0] : (string[])getPortNames.Invoke(null, null);
            }
            catch
            {
                return new string[0];
            }
        }

        private string ResolveStartupPort()
        {
            string savedPort = PlayerPrefs.GetString("SerialStarRunner.Port", portName);
            string[] ports = GetAvailablePortNames();

            if (ports.Contains("COM7"))
            {
                return "COM7";
            }

            if (!string.IsNullOrWhiteSpace(savedPort) && ports.Contains(savedPort))
            {
                return savedPort;
            }

            return ports.Length > 0 ? ports[0] : savedPort;
        }

        private void ReadLoop()
        {
            while (threadRunning)
            {
                try
                {
                    string line;
                    lock (portLock)
                    {
                        line = (string)readLineMethod.Invoke(serialPort, null);
                    }

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lock (queueLock)
                        {
                            pendingLines.Enqueue(line);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Exception actual = Unwrap(exception);
                    if (actual.GetType().Name.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    connected = false;
                    SetThreadStatus("Serial read stopped: " + actual.Message);
                    break;
                }
            }
        }

        private bool TryDequeueLine(out string line)
        {
            lock (queueLock)
            {
                if (pendingLines.Count == 0)
                {
                    line = null;
                    return false;
                }

                line = pendingLines.Dequeue();
                return true;
            }
        }

        private void ParseLine(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                return;
            }

            LastLine = trimmed;

            bool sawAxis = false;
            bool sawPot = false;
            bool sawJump = false;
            bool sawDash = false;
            float nextAxis = Axis;
            bool nextJumpHeld = JumpHeld;
            bool nextDashHeld = DashHeld;
            string[] tokens = trimmed.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                int separator = token.IndexOf('=');
                if (separator < 0)
                {
                    separator = token.IndexOf(':');
                }

                if (separator > 0)
                {
                    string key = token.Substring(0, separator).Trim().ToUpperInvariant();
                    string value = token.Substring(separator + 1).Trim();
                    float number;

                    switch (key)
                    {
                        case "A":
                        case "AXIS":
                            if (TryParseFloat(value, out number))
                            {
                                nextAxis = NormalizeAxisValue(number);
                                sawAxis = true;
                            }
                            break;
                        case "P":
                        case "POT":
                            if (TryParseFloat(value, out number))
                            {
                                Pot01 = Mathf.Clamp01(number / 1023f);
                                sawPot = true;
                            }
                            break;
                        case "J":
                        case "BTN":
                        case "BUTTON":
                        case "JUMP":
                            nextJumpHeld = ParseBool(value);
                            sawJump = true;
                            break;
                        case "D":
                        case "DASH":
                            nextDashHeld = ParseBool(value);
                            sawDash = true;
                            break;
                    }
                }
                else
                {
                    ApplyKeyword(token.ToUpperInvariant(), ref nextAxis, ref nextJumpHeld, ref nextDashHeld, ref sawAxis, ref sawJump, ref sawDash);
                }
            }

            if (!sawAxis && sawPot)
            {
                nextAxis = ApplyAxisDeadZone(Pot01 * 2f - 1f);
            }

            Axis = nextAxis;

            if (sawJump)
            {
                if (!JumpHeld && nextJumpHeld)
                {
                    JumpPressed = true;
                }

                JumpHeld = nextJumpHeld;
            }

            if (sawDash)
            {
                if (!DashHeld && nextDashHeld)
                {
                    DashPressed = true;
                }

                DashHeld = nextDashHeld;
            }
        }

        private void ApplyKeyword(string keyword, ref float nextAxis, ref bool nextJumpHeld, ref bool nextDashHeld, ref bool sawAxis, ref bool sawJump, ref bool sawDash)
        {
                switch (keyword)
                {
                    case "LEFT":
                    nextAxis = -1f;
                    sawAxis = true;
                    break;
                case "RIGHT":
                    nextAxis = 1f;
                    sawAxis = true;
                    break;
                case "CENTER":
                case "STOP":
                    nextAxis = 0f;
                    sawAxis = true;
                    break;
                    case "JUMP":
                    case "JUMP_DOWN":
                    case "BTN_DOWN":
                    case "BUTTON_DOWN":
                        JumpPressed = true;
                        nextJumpHeld = true;
                        sawJump = true;
                        break;
                    case "JUMP_UP":
                    case "BTN_UP":
                    case "BUTTON_UP":
                        nextJumpHeld = false;
                        sawJump = true;
                        break;
                    case "DASH":
                    case "DASH_DOWN":
                        DashPressed = true;
                        nextDashHeld = true;
                        sawDash = true;
                        break;
                    case "DASH_UP":
                        nextDashHeld = false;
                        sawDash = true;
                        break;
                }
        }

        private float NormalizeAxisValue(float value)
        {
            if (Mathf.Abs(value) > 1.5f)
            {
                value /= 100f;
            }

            return ApplyAxisDeadZone(Mathf.Clamp(value, -1f, 1f));
        }

        private float ApplyAxisDeadZone(float value)
        {
            return Mathf.Abs(value) < axisDeadZone ? 0f : value;
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool ParseBool(string text)
        {
            string normalized = text.Trim().ToUpperInvariant();
            return normalized == "1" || normalized == "TRUE" || normalized == "ON" || normalized == "LOW" || normalized == "PRESSED";
        }

        private void SetPortProperty(string propertyName, object value)
        {
            PropertyInfo property = serialPortType.GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(serialPort, value, null);
            }
        }

        private void CleanupPort()
        {
            if (serialPort == null)
            {
                return;
            }

            try
            {
                if (closeMethod != null)
                {
                    closeMethod.Invoke(serialPort, null);
                }
            }
            catch
            {
                // Port may already be closed by the OS.
            }

            try
            {
                if (disposeMethod != null)
                {
                    disposeMethod.Invoke(serialPort, null);
                }
            }
            catch
            {
                // Ignore dispose errors during shutdown.
            }

            serialPort = null;
            readLineMethod = null;
            writeLineMethod = null;
            closeMethod = null;
            disposeMethod = null;
        }

        private static Type FindSerialPortType()
        {
            Type loadedType = FindTypeInLoadedAssemblies();
            if (loadedType != null)
            {
                return loadedType;
            }

            string[] assemblyNames = { "System.IO.Ports", "System" };
            for (int i = 0; i < assemblyNames.Length; i++)
            {
                try
                {
                    Assembly assembly = Assembly.Load(assemblyNames[i]);
                    Type type = assembly.GetType("System.IO.Ports.SerialPort");
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Try the next assembly name.
                }
            }

            return FindTypeInLoadedAssemblies();
        }

        private static Type FindTypeInLoadedAssemblies()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType("System.IO.Ports.SerialPort");
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private void SetThreadStatus(string message)
        {
            lock (statusLock)
            {
                threadStatus = message;
            }
        }

        private string TakeThreadStatus()
        {
            lock (statusLock)
            {
                string message = threadStatus;
                threadStatus = string.Empty;
                return message;
            }
        }

        private static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocationException = exception as TargetInvocationException;
            return invocationException != null && invocationException.InnerException != null ? invocationException.InnerException : exception;
        }
    }
}
