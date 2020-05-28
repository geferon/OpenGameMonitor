using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenGameMonitor.Services;
using OpenGameMonitorLibraries;

namespace OpenGameMonitorWeb.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize]
	public class ServersController : ControllerBase
	{
		private readonly MonitorDBContext _context;
		private readonly IMapper _mapper;
		private readonly IAuthorizationService _authorizationService;
		private readonly UserManager<MonitorUser> _userManager;
		private readonly IPCClient _ipcClient;

		public ServersController(
			MonitorDBContext context,
			IMapper mapper,
			IAuthorizationService authorizationService,
			UserManager<MonitorUser> userManager,
			IPCClient ipcClient
		) {
			_context = context;
			_mapper = mapper;
			_authorizationService = authorizationService;
			_userManager = userManager;
			_ipcClient = ipcClient;
		}

		// GET: api/Servers
		[HttpGet]
		public async Task<ActionResult<IEnumerable<DTOServer>>> GetServers()
		{
			var user = await _userManager.GetUserAsync(User);
			var test = await _userManager.GetRolesAsync(user);

			IQueryable<Server> servers = _context.Servers;
			if (!User.HasClaim("Permission", "Servers.ViewAll"))
			{
				servers = servers.Where((server) =>
					user == server.Owner ||
					(server.Group != null
					? server.Group.Members.Any((group) => group.User == user)
					: false)
				);
			}
			return await servers
				.Include(s => s.Owner)
				.Include(s => s.Group)
				.Include(s => s.Game)
				.Select(s => _mapper.Map<DTOServer>(s))
				.ToListAsync();
		}

		// GET: api/Servers/5
		[HttpGet("{id}")]
		public async Task<ActionResult<DTOServer>> GetServer(int id)
		{
			var server = await _context.Servers
				.Include(s => s.Owner)
				.Include(s => s.Group)
				.Include(s => s.Game)
				.FirstAsync(s => s.Id == id);

			if (server == null)
			{
				return NotFound();
			}

			var user = await _userManager.GetUserAsync(User);

			var authResult = await _authorizationService.AuthorizeAsync(User, server, "ServersView");

			if (!authResult.Succeeded)
			{
				return Forbid();
			}

			return _mapper.Map<DTOServer>(server);
		}

		// PUT: api/Servers/5
		// To protect from overposting attacks, please enable the specific properties you want to bind to, for
		// more details see https://aka.ms/RazorPagesCRUD.
		[HttpPut("{id}")]
		//[Authorize(Policy = "ServerPolicy")]
		public async Task<ActionResult> PutServer(int id, DTOServer dtoserver)
		{
			if (id != dtoserver.Id)
			{
				return BadRequest();
			}

			var server = _mapper.Map<Server>(dtoserver);

			// Auth
			var authResult = await _authorizationService.AuthorizeAsync(User, server, "ServersModify");

			if (!authResult.Succeeded)
			{
				return Forbid();
			}

			// Modify it
			_context.Entry(server).State = EntityState.Modified;

			// Read only properties
			//_context.Entry(server).Property(x => x.Created).IsModified = false;
			//_context.Entry(server).Property(x => x.LastModified).IsModified = false;
			_context.Entry(server).Property(x => x.LastStart).IsModified = false;
			_context.Entry(server).Property(x => x.LastUpdate).IsModified = false;
			_context.Entry(server).Property(x => x.LastUpdateFailed).IsModified = false;

			// Admin only properties
			if (!User.IsInRole("Admin"))
			{
				_context.Entry(server).Property(x => x.Owner).IsModified = false;
				_context.Entry(server).Property(x => x.Group).IsModified = false;
				_context.Entry(server).Property(x => x.Enabled).IsModified = false;
				_context.Entry(server).Property(x => x.StartParamsHidden).IsModified = false;
				_context.Entry(server).Property(x => x.Game).IsModified = false;
				_context.Entry(server).Property(x => x.Path).IsModified = false;
				_context.Entry(server).Property(x => x.Executable).IsModified = false;
				_context.Entry(server).Property(x => x.IP).IsModified = false;
				_context.Entry(server).Property(x => x.DisplayIP).IsModified = false;
				_context.Entry(server).Property(x => x.Port).IsModified = false;
			}
			// _context.Entry(server).Property(x => x.PROPERTY).IsModified = false;

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!ServerExists(id))
				{
					return NotFound();
				}
				else
				{
					throw;
				}
			}

			return NoContent();
		}

		[HttpPatch("{id}")]
		//[Authorize(Policy = "ServerPolicy")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult> PatchServer(int id, DTOServer dtoserver)
		{
			if (id != dtoserver.Id)
			{
				return BadRequest();
			}

			var server = _mapper.Map<Server>(dtoserver);

			// Auth
			var authResult = await _authorizationService.AuthorizeAsync(User, server, "ServersModify");

			if (!authResult.Succeeded)
			{
				return Forbid();
			}

			if (!await ParseServer(server))
				return BadRequest();

			_context.Entry(server).State = EntityState.Modified;

			// Read only properties
			//_context.Entry(server).Property(x => x.Created).IsModified = false;
			//_context.Entry(server).Property(x => x.LastModified).IsModified = false;
			_context.Entry(server).Property(x => x.LastStart).IsModified = false;
			_context.Entry(server).Property(x => x.LastUpdate).IsModified = false;
			_context.Entry(server).Property(x => x.LastUpdateFailed).IsModified = false;

			// Admin only properties
			if (!User.IsInRole("Admin"))
			{
				_context.Entry(server).Property(x => x.Owner).IsModified = false;
				_context.Entry(server).Property(x => x.Group).IsModified = false;
				_context.Entry(server).Property(x => x.Enabled).IsModified = false;
				_context.Entry(server).Property(x => x.StartParamsHidden).IsModified = false;
				_context.Entry(server).Property(x => x.Game).IsModified = false;
				_context.Entry(server).Property(x => x.Path).IsModified = false;
				_context.Entry(server).Property(x => x.Executable).IsModified = false;
				_context.Entry(server).Property(x => x.IP).IsModified = false;
				_context.Entry(server).Property(x => x.DisplayIP).IsModified = false;
				_context.Entry(server).Property(x => x.Port).IsModified = false;
			}
			// _context.Entry(server).Property(x => x.PROPERTY).IsModified = false;

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!ServerExists(id))
				{
					return NotFound();
				}
				else
				{
					throw;
				}
			}

			return NoContent();
		}

		[HttpGet("{id}/tracking/{page=0}")]
		public async Task<ActionResult<IEnumerable<ServerResourceMonitoringRegistry>>> GetServerTrackingRecords(int id, int page)
		{
			var server = await _context.Servers
				.FirstOrDefaultAsync(s => s.Id == id);

			if (server == null)
			{
				return NotFound();
			}

			var user = await _userManager.GetUserAsync(User);

			var authResult = await _authorizationService.AuthorizeAsync(User, server, "ServersInteract");

			if (!authResult.Succeeded)
			{
				return Forbid();
			}

			var recordsPerPage = 10;

			var trackingRecords = _context.ServerResourceMonitoring
				.Include(r => r.Server)
				.Where(r => r.Server.Id == server.Id)
				.OrderByDescending(r => r.TakenAt)
				.Skip(recordsPerPage * page)
				.Take(recordsPerPage);

			return await trackingRecords.ToListAsync();
		}

		[HttpPost("{id}/{function}")]
		public async Task<ActionResult> PostServerAction(int id, string function)
		{
			var server = await _context.Servers.FirstOrDefaultAsync(s => s.Id == id);

			if (server == null)
			{
				return NotFound();
			}

			var user = await _userManager.GetUserAsync(User);

			var authResult = await _authorizationService.AuthorizeAsync(User, server, "ServersInteract");

			if (!authResult.Succeeded)
			{
				return Forbid();
			}

			bool result = true;

			switch (function)
			{
				case "start":
					try
					{
						result = await _ipcClient.ComsClient.ServerOpen(id);
					}
					catch (Exception err)
					{
						return StatusCode(500, new { Error = err.Message });
					}
					break;
				case "stop":
					try
					{
						result = await _ipcClient.ComsClient.ServerClose(id);
					}
					catch (Exception err)
					{
						return StatusCode(500, new { Error = err.Message });
					}
					break;
				case "update":
					try
					{
						_ = Task.Run(async delegate
						{
							await _ipcClient.ComsClient.ServerUpdate(id);
						});
					}
					catch (Exception err)
					{
						return StatusCode(500, new { Error = err.Message });
					}
					break;
				default:
					return BadRequest();
					break;
			}

			if (!result)
			{
				return StatusCode(500);
			}

			return Ok();
		}

		// POST: api/Servers
		// To protect from overposting attacks, please enable the specific properties you want to bind to, for
		// more details see https://aka.ms/RazorPagesCRUD.
		// TODO: Only specific valid properties
		[HttpPost]
		[Authorize(Policy = "ServersCreate")]
		public async Task<ActionResult<DTOServer>> PostServer(DTOServer dtoserver)
		{
			var server = _mapper.Map<Server>(dtoserver);

			if (!await ParseServer(server))
				return BadRequest();

			_context.Servers.Add(server);
			await _context.SaveChangesAsync();

			// Defer, going to take long to complete
			_ = Task.Run(async delegate
			  {
				  //await _ipcClient.ComsClient.Connected();
				  var tempVar = await _ipcClient.ComsClient.ServerInstall(server.Id);
			  });

			return CreatedAtAction("GetServer", new { id = server.Id }, server);
		}

		// DELETE: api/Servers/5
		[HttpDelete("{id}")]
		[Authorize(Policy = "ServersCreate")]
		public async Task<ActionResult<DTOServer>> DeleteServer(int id)
		{
			var server = await _context.Servers.FirstOrDefaultAsync(s => s.Id == id);
			if (server == null)
			{
				return NotFound();
			}

			if (server.ProcessStatus != ServerProcessStatus.Stopped)
			{
				return BadRequest();
			}

			_context.Servers.Remove(server);
			await _context.SaveChangesAsync();

			return _mapper.Map<DTOServer>(server);
		}

		private async Task<bool> ParseServer(Server server)
		{
			server.Owner = await _context.Users.FindAsync(server.Owner.Id); // Only allow existing, do NOT create
			server.Game = await _context.Games.FindAsync(server.Game.Id) ?? server.Game; // Allow both existing and creation
			if (server.Group != null)
			{
				if ((server.Group = await _context.Groups.FindAsync(server.Group.Id)) == null) return false;
			}

			if (server.Owner == null || server.Game == null)
			{
				return false;
			}

			return true;
		}

		private bool ServerExists(int id)
		{
			return _context.Servers.Any(e => e.Id == id);
		}
	}
}
