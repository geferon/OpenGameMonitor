using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly MonitorDBContext _context;
        private readonly IMapper _mapper;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<MonitorUser> _userManager;

        public UsersController(
            MonitorDBContext context,
            IMapper mapper,
            IAuthorizationService authorizationService,
            UserManager<MonitorUser> userManager
        ) {
            _context = context;
            _mapper = mapper;
            _authorizationService = authorizationService;
            _userManager = userManager;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DTOMonitorUser>>> GetUsers()
        {
            return await Task.WhenAll(
                (await _context.Users.Include(b => b.Groups)
                .ToListAsync())
                .Select(async b =>
                {
                    var user = _mapper.Map<DTOMonitorUser>(b);
                    user.Roles = (await _userManager.GetRolesAsync(b)).ToList();
                    return user;
                })
            );
        }

        // GET: api/Users/5
        [HttpGet("{username}")]
        public async Task<ActionResult<DTOMonitorUser>> GetUser(string username)
        {
            var userObj = await _context.Users.Include(b => b.Groups)
                .FirstOrDefaultAsync(b => b.UserName == username);

            if (userObj == null)
            {
                return NotFound();
            }

            var user = _mapper.Map<DTOMonitorUser>(userObj);
            user.Roles = (await _userManager.GetRolesAsync(userObj)).ToList();

            return user;
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{username}")]
        public async Task<IActionResult> PutUser(string username, DTOMonitorUser user)
        {
            if (username != user.UserName)
            {
                return BadRequest();
            }

            var userReal = _mapper.Map<MonitorUser>(user);

            _context.Entry(userReal).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(username))
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

        // POST: api/Users
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<DTOMonitorUser>> PostUser(DTOMonitorUserSend user)
        {
            //_context.Users.Add(user);
            //await _context.SaveChangesAsync();

            var userReal = _mapper.Map<MonitorUser>(user);

            var result = await _userManager.CreateAsync(userReal, user.Password);

            if (!result.Succeeded)
            {
                var errMessage = new StringBuilder();
                foreach (var err in result.Errors)
                {
                    errMessage.Append(err.Code);
                    errMessage.Append(": ");
                    errMessage.Append(err.Description);
                    errMessage.Append(Environment.NewLine);
                }

                return new ContentResult()
                {
                    Content = string.Format("Error while adding the user! {0}", errMessage.ToString()),
                    StatusCode = (int) System.Net.HttpStatusCode.InternalServerError
                };
            }

            await _userManager.AddToRolesAsync(userReal, user.Roles);

            return CreatedAtAction("GetUser", new { username = user.UserName }, user);
        }

        // DELETE: api/Users/5
        [HttpDelete("{username}")]
        public async Task<ActionResult<DTOMonitorUser>> DeleteUser(string username)
        {
            var user = await _context.Users.FindAsync(username);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return _mapper.Map<DTOMonitorUser>(user);
        }

        private bool UserExists(string username)
        {
            return _context.Users.Any(e => e.UserName == username);
        }
    }
}
