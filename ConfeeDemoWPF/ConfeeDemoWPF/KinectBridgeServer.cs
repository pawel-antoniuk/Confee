using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConfeeDemoWPF
{
    class KinectBridgeServer
    {
        private readonly List<Socket> _clients = new List<Socket>();
        private readonly TcpListener _listener;
        private readonly int _port;

        public KinectBridgeServer(int port)
        {
            _port = port;
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            RunListenLoop();
        }

        private void RunListenLoop()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    var client = _listener.AcceptSocket();
                    var receiveArgs = new SocketAsyncEventArgs();
                    receiveArgs.SetBuffer(new byte[1], 0, 1);
                    receiveArgs.Completed += MessageReceived;
                    client.ReceiveAsync(receiveArgs);
                    _clients.Add(client);
                }
            });
        }

        public void BroadcastMessage(string msg)
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            foreach (var client in _clients)
            {
                client.Send(bytes);
            }
        }

        private void MessageReceived(object sender, SocketAsyncEventArgs e)
        {
            var socket = (Socket) sender;
            _clients.Remove(socket);
            socket.Disconnect(false);
        }
    }
}
