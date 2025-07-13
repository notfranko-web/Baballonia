using System.IO.Ports;
using System.Text;

namespace Baballonia.Tests
{
    public class SerialCommandSender : ICommandSender
    {
        private const int DefaultBaudRate = 115200; // esptool-rs: Setting baud rate higher than 115,200 can cause issues
        private string port;
        private SerialPort serialPort;
        private int timeout = 10000; // 10 seconds maximum wait time
        private readonly SemaphoreSlim _lock = new(1, 1);

        public SerialCommandSender(string port)
        {
            this.port = port;

            serialPort = new SerialPort(port, DefaultBaudRate);

            // Set serial port parameters
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Parity = Parity.None;
            serialPort.Handshake = Handshake.None;

            // Set read/write timeouts
            serialPort.ReadTimeout = 30000;
            serialPort.WriteTimeout = 30000;
            serialPort.Encoding = Encoding.UTF8;

            serialPort.Open();
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
        }

        public void Dispose()
        {
            if (serialPort.IsOpen)
                serialPort.Close();

            serialPort.Dispose();
        }

        public string ReadLine()
        {
            DateTime startTime = DateTime.Now;

            StringBuilder responseBuilder = new StringBuilder();
            do
            {
                // Check for timeout
                if ((DateTime.Now - startTime).TotalMilliseconds > timeout)
                {
                    throw new TimeoutException("Reading timeout reached");
                }

                // Read available data
                if (serialPort.BytesToRead > 0)
                {
                    string receivedData = serialPort.ReadLine();
                    responseBuilder.Append(receivedData);
                }
                else
                {
                    // Small delay to prevent CPU spinning
                    Thread.Sleep(10);
                }
            } while (responseBuilder.Length == 0);

            return responseBuilder.ToString().Trim();
        }
        public void WriteLine(string payload)
        {
            serialPort.DiscardInBuffer();

            // Convert the payload to bytes
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            // Write the payload to the serial port
            const int chunkSize = 64;
            for (int i = 0; i < payloadBytes.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, payloadBytes.Length - i);
                serialPort.Write(payloadBytes, i, length);
                Thread.Sleep(50); // Small pause between chunks
            }
            serialPort.Write("\n");

        }

        //Async stuff currently unused
        private async Task<T> WithLock<T>(Func<Task<T>> run)
        {
            await _lock.WaitAsync();
            try
            {
                return await run();
            }
            finally
            {
                _lock.Release();
            }
        }
        private async Task WithLock(Func<Task> run)
        {
            await _lock.WaitAsync();
            try
            {
                await run();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task WriteCommandAsync(string payload)
        {
            await WithLock(async () =>
            {
                await Task.Run(() => WriteLine(payload));
            });
        }

        public async Task<string> ReadResponseAsync()
        {
            return await WithLock(async () =>
            {
                await _lock.WaitAsync();
                return await Task.Run(ReadLine);
            });
        }

        public async Task<string> WriteCommandAndReadResponseAsync(string payload)
        {
            return await WithLock(async () =>
            {
                return await Task.Run(() =>
                {
                    WriteLine(payload);
                    return ReadLine();
                });
            });
        }

    }
}
