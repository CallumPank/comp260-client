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
        static void Main(string[] args)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint ipLocal = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8221);

            bool connected = false;

            

            int ID = 0;
            bool initialMsg = false;
            while (true)
            {

                while (connected == false)
                {
                    try
                    {
                        s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        s.Connect(ipLocal);
                        connected = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("No server - please wait, you call is important to us");
                        Thread.Sleep(1000);
                    }
                }

                while (connected == true)
                {
                    //Console.Clear();
                    if (initialMsg == false)
                    {
                        Console.WriteLine("Type help to begin the adventure\n");
                        initialMsg = true;
                    }
                    Console.Write("\n> ");
                    String ClientText = Console.ReadLine();
                    String Msg = ID.ToString() + ClientText; // " testing, testing, 1,2,3";
                    ID++;
                    ASCIIEncoding encoder = new ASCIIEncoding();
                    byte[] buffer = encoder.GetBytes(ClientText);

                    if (ClientText != "")
                    {
                        try
                        {
                            //Console.WriteLine("Writing to server:\n " + ClientText);
                            int bytesSent = s.Send(buffer);

                            buffer = new byte[4096];
                            int reciever = s.Receive(buffer);
                            if (reciever > 0)
                            {
                                String userCmd = encoder.GetString(buffer, 0, reciever);
                                Console.WriteLine(userCmd);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            connected = false;
                        }
                    }
                }
                
            }
        }
    }
}