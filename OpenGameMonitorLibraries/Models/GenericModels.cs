using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OpenGameMonitorLibraries
{
    public class Server
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public User Owner { get; set; }
        public Group Group { get; set; }
		public bool Enabled { get; set; }

        public string Executable { get; set; }
        public string Path { get; set; }
        public bool Graceful { get; set; }

        public Game Game { get; set; }
        public string Branch { get; set; }
        public string BranchPassword { get; set; }

        public int PID { get; set; }
        public int UpdatePID { get; set; }

    }

    public class Game
    {
        [Key]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Engine { get; set; }
        public uint SteamID { get; set; }
    }

    public class User
    {
        [Key]
        public string Username { get; set; }
        public string Email { get; set; }
        public string Language { get; set; } // Maybe?
        public bool Admin { get; set; }

        public ICollection<Group> Groups { get; set; }
    }

    public class Group
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<User> Members { get; set; }
    }

    public class Setting
    {
        [Key]
        public string Key { get; set; }
        public string Value { get; set; } // THIS WILL BE JSON
    }
}
