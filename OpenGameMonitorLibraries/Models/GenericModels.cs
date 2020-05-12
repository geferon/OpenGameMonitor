using AutoMapper.Configuration.Annotations;
using EntityFrameworkCore.Triggers;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

namespace OpenGameMonitorLibraries
{
	public abstract class Trackable
	{
		public DateTime Inserted { get; private set; }
		public DateTime Updated { get; private set; }

		static Trackable()
		{
			Triggers<Trackable>.Inserting += entry => entry.Entity.Inserted = entry.Entity.Updated = DateTime.UtcNow;
			Triggers<Trackable>.Updating += entry => entry.Entity.Updated = DateTime.UtcNow;
		}
	}

	public class ServerEnvironmentVariable
	{
		public string Key { get; set; }
		public string Value { get; set; }
	}

	public class Server : Trackable
	{
		[Key]
		public int Id { get; set; }
		[Required]
		public string Name { get; set; }
		[Required]
		//public User Owner { get; set; }
		public virtual MonitorUser Owner { get; set; }
		public virtual Group? Group { get; set; }
		public bool Enabled { get; set; }

		// Optional: Automatic install thing
		public string? Executable { get; set; }
		public string? Path { get; set; }
		public bool Graceful { get; set; }
		public bool RestartOnClose { get; set; }

		public string? StartParams { get; set; }
		public string? StartParamsHidden { get; set; }
		public bool StartParamsModifyAllowed { get; set; }
		public ProcessPriorityClass ProcessPriority { get; set; }
		public List<ServerEnvironmentVariable> EnvironmentVariables { get; set; }

		public string? IP { get; set; }
		public string? DisplayIP { get; set; }
		public int Port { get; set; }

		[Required]
		public virtual Game Game { get; set; }
		[Column(TypeName="varchar(40)")]
		public string? Branch { get; set; }
		public string? BranchPassword { get; set; }

		// Not needed as of the Trackable class anymore
		// public DateTime Created { get; set; }
		// public DateTime? LastModified { get; set; }

		public int? PID { get; set; }
		public int? UpdatePID { get; set; }
		public DateTime? LastStart { get; set; }

		public DateTime? LastUpdate { get; set; }
		public bool LastUpdateFailed { get; set; }
	}

	public class DTOServer : Trackable
	{
		public int Id { get; set; }
		public string Name { get; set; }

		//public User Owner { get; set; }
		public virtual DTOMonitorUser Owner { get; set; }
		public virtual Group? Group { get; set; }
		public bool Enabled { get; set; }

		// Optional: Automatic install thing
		public string? Executable { get; set; }
		public string? Path { get; set; }
		public bool Graceful { get; set; }
		public bool RestartOnClose { get; set; }

		public string? StartParams { get; set; }
		public string? StartParamsHidden { get; set; }
		public bool StartParamsModifyAllowed { get; set; }
		public ProcessPriorityClass ProcessPriority { get; set; }
		public List<ServerEnvironmentVariable> EnvironmentVariables { get; set; }

		public string? IP { get; set; }
		public string? DisplayIP { get; set; }
		public int Port { get; set; }

		[Required]
		public virtual Game Game { get; set; }
		[Column(TypeName = "varchar(40)")]
		public string? Branch { get; set; }
		public string? BranchPassword { get; set; }

		public DateTime? LastStart { get; set; }

		public DateTime? LastUpdate { get; set; }
		public bool LastUpdateFailed { get; set; }
	}

	public class Game
	{
		[Key]
		public string Id { get; set; }
		[Required]
		public string Name { get; set; }
		[Required]
		public string Engine { get; set; }
		public uint? SteamID { get; set; }
	}

	public class MonitorUser : IdentityUser<string>
	{
		public MonitorUser() : base() { }
		public MonitorUser(string userName) : base(userName) { }

		//public List<GroupUser> Groups { get; set; }
		public virtual ICollection<GroupUser> Groups { get; set; }
	}

	public class DTOMonitorUser
	{
		public string Id { get; set; }
		public string UserName { get; set; }
		public string Email { get; set; }
		public virtual ICollection<GroupUser> Groups { get; set; }
		public List<string> Roles { get; set; }
	}
	
	public class DTOMonitorUserSend : DTOMonitorUser
	{
		[Ignore]
		public string Password { get; set; }
	}

	public class MonitorRole : IdentityRole<string>
	{
		public MonitorRole() : base() { }
		public MonitorRole(string name) : base(name) { }
	}

	public class DTOMonitorRole
	{
		public string Id;
		public string Name;
	}

	//public class User
	//{
	//    [Key]
	//    public string Username { get; set; }
	//    [Required]
	//    public string Email { get; set; }
	//    [Column(TypeName = "varchar(2)")]
	//    public string Language { get; set; } // Maybe?
	//    public bool Admin { get; set; }

	//    public List<GroupUser> Groups { get; set; }
	//}

	public class Group : Trackable
	{
		[Key]
		public int Id { get; set; }
		[Required]
		public string Name { get; set; }

		//public List<GroupUser> Members { get; set; }
		public virtual ICollection<GroupUser> Members { get; set; }
		//public virtual ICollection<MonitorUser> Members { get; set; }
	}

	public class GroupUser
	{
		public string UserID { get; set; }
		//public User User { get; set; }
		public virtual MonitorUser User { get; set; }
		public int GroupID { get; set; }
		public virtual Group Group { get; set; }
	}

	public class Setting
	{
		[Key]
		public string Key { get; set; }
		[Required]
		public object Value { get; set; }
	}
}
