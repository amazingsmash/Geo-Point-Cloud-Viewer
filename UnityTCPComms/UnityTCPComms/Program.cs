using System;
using System.Threading;

namespace UnityTCPComms
{
    class ClientListener : ITCPEndListener
    {
   
        public void OnMessageReceived(string msg)
        {
            Console.Write("Client Received Msg:" + msg + "\n");


        }

        public void OnStatusChanged(TCPEnd.Status status)
        {
        }

        public void OnStatusMessage(string msg)
        {
            Console.Write("Status Msg:" + msg + "\n");
            Console.Write("\n--------\n");
        }
    }

    class ServerListener : ITCPEndListener
    {
        string ip;
        int port;

        public ServerListener(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public void OnMessageReceived(string msg)
        {
            Console.Write("Server Received Msg:" + msg);
        }

        public void OnStatusChanged(TCPEnd.Status status)
        {
        }

        public void OnStatusMessage(string msg)
        {
            Console.Write("Status Msg:" + msg);
            Console.Write("\n--------\n");
        }
    }


    class Program
    {
        static void TestServerAndClient()
        {
            string ip = "127.0.0.1";
            int port = 4040;
            ServerListener listener = new ServerListener(ip, port);
            TCPServer server = new TCPServer(ip, port, listener);

            Thread.Sleep(2000);

            TCPClient client = new TCPClient(ip, port, new ClientListener());
            client.ConnectToTCPServer();

            Thread.Sleep(2000);

            while (true)
            {
                client.SendMessage("Client msg.");
                Thread.Sleep(1000);
            }
        }

        static void TestClient(string ip, int port)
        {
            TCPClient client = new TCPClient(ip, port, new ClientListener());
            client.ConnectToTCPServer();

            Thread.Sleep(2000);

            while (true)
            {
                client.SendMessage("Client msg.");
                Thread.Sleep(1000);
            }
        }

        static void Main(string[] args)
        {
            //TestServerAndClient();
            TestClient("127.0.0.1", 1234);
        }
    }
}
