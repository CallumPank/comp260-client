using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Program
{
    public class Player
    {
        public Dungeon dungeonRef;
        public Room currentRoom;
        public String playerName;

        public void Init()
        {
            currentRoom = dungeonRef.roomMap["Room 1"]; ;
        }
    }
}