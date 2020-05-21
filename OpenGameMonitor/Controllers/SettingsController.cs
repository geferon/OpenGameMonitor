using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenGameMonitorLibraries;

namespace OpenGameMonitorWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly MonitorDBContext _context;

        public SettingsController(MonitorDBContext context)
        {
            _context = context;
        }

        // ONLY Get AND Update

        // GET: api/Config
        [HttpGet]
        [Authorize(Policy = "SettingsView")]
        public async Task<IEnumerable<Setting>> GetAll()
        {
            return await _context.Settings.ToListAsync();
        }

        /*
        // GET: api/Config/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/Config
        [HttpPost]
        [Authorize(Policy = "SettingsEdit")]
        public void Post([FromBody] string value)
        {
        }
        */

        // PUT: api/Config/5
        [HttpPut("{id}")]
        [Authorize(Policy = "SettingsEdit")]
        public async Task<IActionResult> Put(string id, [FromBody] Setting setting)
        {
            if (id != setting.Key)
            {
                return BadRequest();
            }

            _context.Entry(setting).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SettingExists(id))
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

        /*
        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "SettingsEdit")]
        public async Task<IActionResult> Delete(int id)
        {
        }
        */

        private bool SettingExists(string id)
        {
            return _context.Settings.Any(e => e.Key == id);
        }
    }
}
