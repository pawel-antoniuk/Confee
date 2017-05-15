using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.UI.Core;

namespace Confee
{
    class KinectBridgeClient
    {
        private readonly int _port;
        private readonly byte[] _readBuffer = new byte[64];

        public event EventHandler<string> OnMessageReceived;

        public KinectBridgeClient(int port)
        {
            _port = port;
        }

        public void BeginListen()
        {
            new Task(async delegate
            {
                var client = new StreamSocket();
                await client.ConnectAsync(new HostName("127.0.0.1"), _port.ToString());
                while (true)
                {
                    var len = await client.InputStream.AsStreamForRead().ReadAsync(_readBuffer, 0, _readBuffer.Length);
                    var msg = Encoding.UTF8.GetString(_readBuffer, 0, len);
                    OnMessageReceived?.Invoke(this, msg);
                }
            }).Start();
        }
    }
}
