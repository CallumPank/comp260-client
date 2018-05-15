using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

#if TARGET_LINUX
using Mono.Data.Sqlite;
using sqliteConnection 	=Mono.Data.Sqlite.SqliteConnection;
using sqliteCommand 	=Mono.Data.Sqlite.SqliteCommand;
using sqliteDataReader	=Mono.Data.Sqlite.SqliteDataReader;
#endif

#if TARGET_WINDOWS
using System.Data.SQLite;
using sqliteConnection = System.Data.SQLite.SQLiteConnection;
using sqliteCommand = System.Data.SQLite.SQLiteCommand;
using sqliteDataReader = System.Data.SQLite.SQLiteDataReader;
#endif

namespace SUD
{
    class Program
    {
        static bool quit = false;

        static ConcurrentQueue<ClientMessageBase> clientCommand = new ConcurrentQueue<ClientMessageBase>();

        class ReceiveThreadLaunchInfo
        {
            public ReceiveThreadLaunchInfo(int ID, Socket socket)
            {
                this.ID = ID;
                this.socket = socket;
            }

            public int ID;
            public Socket socket;
        }

        //accepts client
        static void acceptClientThread(Object obj)
        {
            Socket s = obj as Socket;

            int ID = 0;

            while (quit == false)
            {
                var newClientSocket = s.Accept();

                var myThread = new Thread(clientReceiveThread);
                myThread.Start(new ReceiveThreadLaunchInfo(ID, newClientSocket));

                ID++;

                clientCommand.Enqueue(new ClientJoined(newClientSocket));

                Console.WriteLine("Client added");
            }
        }
        //gets player socket and process information they send
        static void clientReceiveThread(Object obj)
        {
            ReceiveThreadLaunchInfo receiveInfo = obj as ReceiveThreadLaunchInfo;

            ASCIIEncoding encoder = new ASCIIEncoding();
            bool socketLost = false;

            while ((quit == false) && (socketLost == false))
            {
                byte[] buffer = new byte[4096];

                try
                {
                    int result = receiveInfo.socket.Receive(buffer);

                    if (result > 0)
                    {
                        clientCommand.Enqueue(new ClientMessage(receiveInfo.socket, encoder.GetString(buffer, 0, result)));
                    }
                }
                catch (System.Exception)
                {
                    clientCommand.Enqueue(new ClientLost(receiveInfo.socket));
                    socketLost = true;
                }
            }
        }

        //Main loop
        static void Main(string[] args)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //IP address it runs from
            Console.WriteLine("Running from: " + "'" + args[0] + "'");
            IPEndPoint ipLocal = new IPEndPoint(IPAddress.Parse(args[0]), 8222);

            s.Bind(ipLocal);
            s.Listen(4);

            //creates instance of dungeon
            Dungeon dungeon = new Dungeon();
            dungeon.Init();


            Console.WriteLine("Waiting for clients ...");

            //new thread for new clients
            var myThread = new Thread(acceptClientThread);
            myThread.Start(s);

            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] outputBuffer;

            while (true)
            {
                ClientMessageBase command;

                if (clientCommand.TryDequeue(out command) == true)
                {
                    if (command is ClientJoined)
                    {
                        try
                        {
                            //sets player to room 0 the starting room
                            dungeon.SetClientInRoom(command.client, "Room 0");
                            String dungeonOutput = dungeon.RoomDescription(command.client);

                            dungeonOutput += "type 'help' for help\n";

                            try
                            {
                                //sends output from dungeon
                                command.client.Send(encoder.GetBytes(dungeonOutput));
                            }
                            catch (Exception) { }

                            Console.WriteLine("Send client welcome: " + dungeonOutput);

                            //tell all the clients that this client has entered

                            foreach (var player in dungeon.socketToRoomLookup)
                            {
                                if (player.Key != command.client)
                                {
                                    try
                                    {
                                        player.Key.Send(encoder.GetBytes("A new dungeoneer has entered\n"));
                                    }
                                    catch (Exception) { }
                                }
                            }
                        }
                        catch (System.Exception)
                        {
                            clientCommand.Enqueue(new ClientLost(command.client));
                        }
                    }

                    // decides on what to do based on client response
                    if (command is ClientMessage)
                    {
                        var clientMessage = command as ClientMessage;

                        String outputToUser = dungeon.RoomDescription(clientMessage.client);
                        //splits input message
                        String[] input = clientMessage.message.Split(' ');

                        switch (input[0].ToLower())
                        {
                            //gives users list to commands
                            case "help":
                                outputToUser += "\nCommands are ....\n";
                                outputToUser += "help - for this screen\n";
                                outputToUser += "look - to look around\n";
                                outputToUser += "go [north | south | east | west]  - to travel between locations\n";
                                outputToUser += "say - talk to all the dungeoneers in the room\n";
                                outputToUser += "\n";

                                break;

                            case "look":
                                //prints other dungeon users
                                outputToUser = dungeon.RoomDescription(clientMessage.client);
                                break;

                            case "say":
                                //used for chat
                                outputToUser += "\nYou say: ";
                                for (var i = 1; i < input.Length; i++)
                                {
                                    outputToUser += input[i] + " ";
                                }

                                outputToUser += "\n";


                                String messageToSend = "Someone says: ";

                                for (var i = 1; i < input.Length; i++)
                                {
                                    messageToSend += input[i] + " ";
                                }

                                outputBuffer = encoder.GetBytes(messageToSend);

                                foreach (var kvp in dungeon.socketToRoomLookup)
                                {
                                    if ((kvp.Key != clientMessage.client)
                                        && (kvp.Value != dungeon.socketToRoomLookup[clientMessage.client])
                                        )
                                    {
                                        try
                                        {
                                            kvp.Key.Send(outputBuffer);
                                        }
                                        catch (Exception)
                                        { }
                                    }
                                }


                                break;

                            case "go":
                                //allows user to navigate dungeon
                                // is arg[1] sensible?

                                bool newDestination = false;
                                var oldRoom = dungeon.socketToRoomLookup[clientMessage.client];
                                var sqlCommand = new sqliteCommand("select north, south, east, west from  table_rooms where name == '" + oldRoom.name + "'", dungeon.conn);

                                var reader = sqlCommand.ExecuteReader();

                                reader.Read();

                                //checks to see if player is moving to a new room
                                newDestination = true;
                                try
                                {
                                    var goToRoom = reader[input[1].ToLower()] as String;

                                    sqlCommand = new sqliteCommand("select * from  table_rooms where name == '" + goToRoom + "'", dungeon.conn);

                                    reader = sqlCommand.ExecuteReader();

                                    reader.Read();
                                    Room nextRoom = new Room(goToRoom, reader["desc"] as String);


                                    if (goToRoom == "")
                                    {
                                        newDestination = false;
                                    }
                                    dungeon.socketToRoomLookup[clientMessage.client] = nextRoom;



                                }
                                catch (Exception ex)
                                {
                                    newDestination = false;
                                }

                                if (newDestination == false)
                                {
                                    //handle error
                                    outputToUser += "\nERROR";
                                    outputToUser += "\nCan not go " + input[1] + " from here";
                                    outputToUser += "\n";
                                }
                                else
                                {
                                    //sends room description to users
                                    var newRoom = dungeon.socketToRoomLookup[clientMessage.client];

                                    outputToUser = dungeon.RoomDescription(clientMessage.client);
                                    //supposed to be used for telling users if users player has left/entered the room.
                                    foreach (var kvp in dungeon.socketToRoomLookup)
                                    {
                                        if ((kvp.Key != clientMessage.client)
                                            && (kvp.Value == oldRoom)
                                            )
                                        {
                                            try
                                            {
                                                kvp.Key.Send(encoder.GetBytes("A dungeoneer has left this room\n"));
                                            }
                                            catch (Exception)
                                            { }
                                        }

                                        if ((kvp.Key != clientMessage.client)
                                            && (kvp.Value == newRoom)
                                            )
                                        {
                                            try
                                            {
                                                kvp.Key.Send(encoder.GetBytes("A dungeoneer has entered this room\n"));
                                            }
                                            catch (Exception)
                                            { }
                                        }
                                    }
                                }

                                break;

                            default:
                                //handle error
                                outputToUser += "\nERROR";
                                outputToUser += "\nCan not " + clientMessage.message;
                                outputToUser += "\n";
                                break;
                        }

                        try
                        {
                            clientMessage.client.Send(encoder.GetBytes(outputToUser));
                            Console.WriteLine("Send client message: " + outputToUser);
                        }
                        catch (Exception) { }
                    }
                    //if client is lost it tells users a client left
                    if (command is ClientLost)
                    {
                        var clientMessage = command as ClientLost;

                        Console.WriteLine("Client Lost");

                        foreach (var player in dungeon.socketToRoomLookup)
                        {
                            if (player.Key != command.client)
                            {
                                try
                                {
                                    player.Key.Send(encoder.GetBytes("A dungeoneer has left the dungeon\n"));
                                }
                                catch (Exception)
                                {

                                }
                            }
                        }

                        dungeon.RemoveClient(clientMessage.client);
                    }
                }
            }
        }
    }
}
