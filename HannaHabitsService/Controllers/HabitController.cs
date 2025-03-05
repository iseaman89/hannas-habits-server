using AutoMapper;
using HannaHabitsService.Dtos.Habits;
using HannaHabitsService.Models;
using HannaHabitsService.Repositories.Habits;
using HannasHabits.Data.Shared.Statics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HannaHabitsService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HabitController : ControllerBase
    {
        private readonly IHabitRepository _habitRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<HabitController> _logger;

        public HabitController(IHabitRepository habitRepository, IMapper mapper, ILogger<HabitController> logger)
        {
            _habitRepository = habitRepository;
            _mapper = mapper;
            _logger = logger;
        }
        
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<HabitReadOnlyDto>>> GetHabits(string userId, CancellationToken cancellationToken)
        {
            try
            {
                var habits = await _habitRepository.GetHabitsByUserIdAsync(userId, cancellationToken);
                var dtos = _mapper.Map<IEnumerable<HabitReadOnlyDto>>(habits);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing GET in {nameof(GetHabits)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }
        
        [HttpGet("{id:int}")]
        public async Task<ActionResult<HabitReadOnlyDto>> GetHabit(int id, CancellationToken cancellationToken)
        {
            try
            {
                var habit = await _habitRepository.GetHabitByIdAsync(id, cancellationToken);

                if (habit == null)
                {
                    _logger.LogWarning("Record Not Found: {Name} - ID: {Id}", nameof(GetHabit), id);
                    return NotFound();
                }

                var dto = _mapper.Map<HabitReadOnlyDto>(habit);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing GET in {nameof(GetHabit)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }
        
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutHabit(int id, HabitUpdateDto habitDto, CancellationToken cancellationToken)
        {
            if (id != habitDto.Id)
            {
                _logger.LogWarning("Update ID invalid in {Name} - ID: {Id}", nameof(PutHabit), id);
                return BadRequest();
            }
            
            var habit = await _habitRepository.GetHabitByIdAsync(id, cancellationToken);

            if (habit == null)
            {
                _logger.LogWarning("{Habit} record not found in {Name} - ID: {Id}", nameof(Habit), nameof(PutHabit), id);
                return NotFound();
            }

            _mapper.Map(habitDto, habit);

            try
            {
                await _habitRepository.UpdateAsync(habit, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!await HabitExists(id, cancellationToken))
                {
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, $"Error Performing PUT in {nameof(PutHabit)}");
                    return StatusCode(500, Messages.Error500Message);
                }
            }

            return NoContent();
        }
        
        [HttpPost]
        public async Task<ActionResult<HabitCreateDto>> PostHabit(HabitCreateDto habitDto, CancellationToken cancellationToken)
        {
            try
            {
                var habit = _mapper.Map<Habit>(habitDto);
                await _habitRepository.AddAsync(habit, cancellationToken);
                return CreatedAtAction(nameof(GetHabit), new { id = habit.Id }, habit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing POST in {nameof(PostHabit)}");
                return StatusCode(500, Messages.Error500Message);
            }
            
        }
        
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteHabit(int id, CancellationToken cancellationToken)
        {
            try
            {
                var habit = await _habitRepository.GetAsync(id, cancellationToken);
                if (habit == null)
                {
                    _logger.LogWarning("{Habit} record not found in {Name} - ID: {Id}", nameof(Habit), nameof(DeleteHabit), id);
                    return NotFound();
                }

                await _habitRepository.DeleteAsync(id, cancellationToken);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing DELETE in {nameof(DeleteHabit)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }

        private async Task<bool> HabitExists(int id, CancellationToken cancellationToken)
        {
            return await _habitRepository.Exists(id, cancellationToken);
        }
    }
}
