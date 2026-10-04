#if NET_4_6 || NET_UNITY_4_8
#define SERIAL_PORTS
#endif

using System;
using System.Threading;
#if SERIAL_PORTS
using System.IO.Ports;
#endif

namespace PhysicalDigital.Communication
{
    public sealed class SerialPortConnection
    {
        private const int ReadBufferSize = 512;
        private const int ReadTimeoutMs = 50;
        private const int WriteTimeoutMs = 200;
        private const int ThreadJoinTimeoutMs = 500;
        private const int DataBits = 8;
        private const string UnsupportedMessage = "Set Api Compatibility Level to .NET Framework (Project Settings > Player)";

        private readonly Action<byte[], int> onBytesReceived;
        private Thread readThread;
        private volatile bool running;
        private volatile string error;
#if SERIAL_PORTS
        private SerialPort port;
#endif

        public SerialPortConnection(Action<byte[], int> onBytesReceived)
        {
            this.onBytesReceived = onBytesReceived;
        }

        public bool IsOpen => running;
        public string Error => error;

        public static string[] AvailablePorts()
        {
#if SERIAL_PORTS
            try
            {
                string[] ports = SerialPort.GetPortNames();
                Array.Sort(ports, ComparePortNames);
                return ports;
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
#else
            return Array.Empty<string>();
#endif
        }

        public bool TryOpen(string portName, int baudRate, out string failure)
        {
            Close();
#if SERIAL_PORTS
            try
            {
                port = new SerialPort(portName, baudRate, Parity.None, DataBits, StopBits.One)
                {
                    ReadTimeout = ReadTimeoutMs,
                    WriteTimeout = WriteTimeoutMs,
                    DtrEnable = true,
                };
                port.Open();
                port.DiscardInBuffer();
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                port = null;
                return false;
            }
            failure = "";
            error = null;
            running = true;
            readThread = new Thread(ReadLoop) { IsBackground = true, Name = nameof(SerialPortConnection) };
            readThread.Start();
            return true;
#else
            failure = UnsupportedMessage;
            return false;
#endif
        }

        public void Close()
        {
            running = false;
            if (readThread != null)
            {
                readThread.Join(ThreadJoinTimeoutMs);
                readThread = null;
            }
#if SERIAL_PORTS
            if (port != null)
            {
                try
                {
                    port.Close();
                }
                catch (Exception)
                {
                }
                port = null;
            }
#endif
            error = null;
        }

        public bool TryWrite(string text)
        {
#if SERIAL_PORTS
            if (!running || port == null)
            {
                return false;
            }
            try
            {
                port.Write(text);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
#else
            return false;
#endif
        }

        private static int ComparePortNames(string first, string second)
        {
            int byLength = first.Length.CompareTo(second.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(first, second);
        }

#if SERIAL_PORTS
        private void ReadLoop()
        {
            byte[] buffer = new byte[ReadBufferSize];
            while (running)
            {
                int count;
                try
                {
                    count = port.Read(buffer, 0, buffer.Length);
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (Exception exception)
                {
                    if (running)
                    {
                        error = exception.Message;
                    }
                    return;
                }
                onBytesReceived(buffer, count);
            }
        }
#endif
    }
}
