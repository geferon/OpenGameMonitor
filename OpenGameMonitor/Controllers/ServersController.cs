using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenGameMonitorLibraries;

namespace Core.OpenGameMonitorWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ServersController : ControllerBase
    {
        private readonly MonitorDBContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<MonitorUser> _userManager;

        public ServersController(MonitorDBContext context, IAuthorizationService authorizationService, UserManager<MonitorUser> userManager)
        {
            _context = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
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
        [Authorize(Policy = "ServerPolicy")]
        public async Task<ActionResult<Server>> GetServer(int id)
        {
            var server = await _context.Servers.FindAsync(id);

            if (server == null)
            {
                return NotFound();
            }

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
        public async Task<IActionResult> PutServer(int id, Server server)
        {
            if (id != server.Id)
            {
                return BadRequest();
            }

            _context.Entry(server).State = EntityState.Modified;

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

        // POST: api/Servers
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
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
