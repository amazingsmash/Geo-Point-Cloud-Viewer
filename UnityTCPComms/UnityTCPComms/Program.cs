using System;

namespace UnityTCPComms
{
    class Listener : ITCPListener
    {
   
        public void OnMessageReceived(string msg)
        {
            Console.Write("Received Msg:" + msg);
            Console.Write("\n--------\n");
        }

        public void OnStatusMessage(string msg)
        {
            Console.Write("Status Msg:" + msg);
            Console.Write("\n--------\n");
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            TCPClient client = new TCPClient(new Listener());
            client.ConnectToTCPServer();

            while (true)
            {
                Console.WriteLine("Give me a message for Unity");
                var msg = Console.ReadLine();
                client.SendMessage(msg);
            }
        }
    }
}
