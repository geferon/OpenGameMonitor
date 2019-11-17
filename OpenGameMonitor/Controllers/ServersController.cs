using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<MonitorUser> _userManager;
        private readonly IPCClient _ipcClient;

        public ServersController(MonitorDBContext context, IAuthorizationService authorizationService, UserManager<MonitorUser> userManager, IPCClient ipcClient)
        {
            _context = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
            _ipcClient = ipcClient;
        }

        // GET: api/Servers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Server>>> GetServers()
        {
            var user = await _userManager.GetUserAsync(User);

            IQueryable<Server> servers = _context.Servers;
            if (!User.IsInRole("Admin"))
            {
                servers = servers.Where((server) =>
                    user == server.Owner ||
                    (server.Group != null
                    ? server.Group.Members.Any((group) => group.User == user)
                    : false)
                );
            }
            return await servers.ToListAsync();
        }

        // GET: api/Servers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Server>> GetServer(int id)
        {
            var server = await _context.Servers.FindAsync(id);

            if (server == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);

            var authResult = await _authorizationService.AuthorizeAsync(User, server, "ServerPolicy");

            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return server;
        }

        // PUT: api/Servers/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        [Authorize(Policy = "ServerPolicy")]
        public async Task<IActionResult> PutServer(int id, Server server)
        {
            if (id != server.Id)
            {
                return BadRequest();
            }

            _context.Entry(server).State = EntityState.Modified;

            // Read only properties
            _context.Entry(server).Property(x => x.Created).IsModified = false;
            _context.Entry(server).Property(x => x.LastModified).IsModified = false;
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

        [HttpPatch]
        public async void PatchServer(int id, Server server)
        {

        }

        [HttpPost("{id}")]
        public async Task<ActionResult> PostServerAction(int id, string action)
        {
            var server = await _context.Servers.FindAsync(id);

            if (server == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);

            var authResult = await _authorizationService.AuthorizeAsync(User, server, "ServerPolicy");

            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            switch (action)
            {
                case "start":
                    try
                    {
                        await _ipcClient.ComsClient.ServerOpen(id);
                    }
                    catch (Exception err)
                    {
                        return StatusCode(500, new { Error = err.Message });
                    }
                    break;
                case "stop":
                    try
                    {
                        await _ipcClient.ComsClient.ServerClose(id);
                    }
                    catch (Exception err)
                    {
                        return StatusCode(500, new { Error = err.Message });
                    }
                    break;
                case "update":
                    try
                    {
                        await _ipcClient.ComsClient.ServerUpdate(id);
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

            return Ok();
        }

        // POST: api/Servers
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [Authorize(Roles="Admin")]
        [HttpPost]
        public async Task<ActionResult<Server>> PostServer(Server server)
        {
            _context.Servers.Add(server);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetServer", new { id = server.Id }, server);
        }

        // DELETE: api/Servers/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Server>> DeleteServer(int id)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server == null)
            {
                return NotFound();
            }

            _context.Servers.Remove(server);
            await _context.SaveChangesAsync();

            return server;
        }

        private bool ServerExists(int id)
        {
            return _context.Servers.Any(e => e.Id == id);
        }
    }
}
