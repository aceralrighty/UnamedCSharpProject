using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBD.ScheduleModule.Data;
using TBD.ScheduleModule.Models;

namespace TBD.ScheduleModule.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ScheduleController(ScheduleDbContext context) : ControllerBase
{
    // GET: api/Schedule
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Schedule>>> GetSchedules()
    {
        return await context.Schedules.ToListAsync();
    }

    // GET: api/Schedule/5
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Schedule>> GetSchedule(Guid id)
    {
        var schedule = await context.Schedules.FindAsync(id);

        if (schedule == null)
        {
            return NotFound();
        }

        return schedule;
    }

    // PUT: api/Schedule/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> PutSchedule(Guid id, Schedule schedule)
    {
        if (id != schedule.Id)
        {
            return BadRequest();
        }

        context.Entry(schedule).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ScheduleExists(id))
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

    // POST: api/Schedule
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    public async Task<ActionResult<Schedule>> PostSchedule(Schedule schedule)
    {
        context.Schedules.Add(schedule);
        await context.SaveChangesAsync();

        return CreatedAtAction("GetSchedule", new { id = schedule.Id }, schedule);
    }

    // DELETE: api/Schedule/5
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        var schedule = await context.Schedules
            .Where(s => s.DeletedAt == null)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (schedule == null)
        {
            return NotFound();
        }

        // Softly delete: set DeletedAt timestamp instead of removing
        schedule.DeletedAt = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;

        context.Entry(schedule).State = EntityState.Modified;
        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> DeleteSchedulePermanently(Guid id)
    {
        var schedule = await context.Schedules.FindAsync(id);
        if (schedule == null)
        {
            return NotFound();
        }

        context.Schedules.Remove(schedule);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private bool ScheduleExists(Guid id)
    {
        return context.Schedules.Any(e => e.Id == id);
    }
}
