using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Client
{
    class client
    {
        static LinkedList<String> incommingMessages = new LinkedList<string>();

        static void serverReceiveThread(Object obj)
        {
            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] receiveBuffer = new byte[8192];

            Socket s = obj as Socket;

            while (true)
            {
                try
                {
                    int reciever = s.Receive(receiveBuffer);
                    
                    if (reciever > 0)
                    {
                        String clientMsg = encoder.GetString(receiveBuffer, 0, reciever);
                        Console.WriteLine(clientMsg);
                    }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        static void Main(string[] args)
        {

            string ipAdress = "127.0.0.1";
            int port = 8221;

            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipLocal = new IPEndPoint(IPAddress.Parse(ipAdress), port);
            ipLocal = new IPEndPoint(IPAddress.Parse(ipAdress), port);
            bool connected = false;

            while (connected == false)
            {
                Console.WriteLine("Looking for server: " + ipLocal);
                try
                {
                    s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    

                    s.Connect(ipLocal);
                    Console.Clear();
                    Console.WriteLine("Connected To Server\n\nType help for assistance");
                    connected = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("No server");
                    Thread.Sleep(1000);
                }
            }

            int ID = 0;

            bool firstMsg = false;

            var myThread = new Thread(serverReceiveThread);
            myThread.Start(s);

            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] buffer = new byte[4096];

            while (true)
            {
                if (firstMsg == false)
                {
                    Console.WriteLine("Type help to begin adventure");
                    firstMsg = true;
                }
                Console.Write("\n> ");
                String CliText = Console.ReadLine();
                String Msg = ID.ToString() + CliText; 
                ID++;
                ASCIIEncoding encoder = new ASCIIEncoding();
                byte[] buffer = encoder.GetBytes(CliText);

                if (CliText != "")
                {

                    try
                    {
                        // Writes messages to server
                        Console.WriteLine("Writing to server: " + CliText);
                        int bytesSent = s.Send(buffer);

                        buffer = new byte[4096];
                        int reciever = s.Receive(buffer);
                        if (reciever > 0)
                        {
                            string userCmd = encoder.GetString(buffer, 0, reciever);
                            Console. WriteLine(userCmd);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }
        }
    }
}