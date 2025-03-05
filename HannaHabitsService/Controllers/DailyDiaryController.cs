using AutoMapper;
using HannaHabitsService.Dtos.DailyDiaries;
using HannaHabitsService.Models;
using HannaHabitsService.Repositories.DailyDiaries;
using HannasHabits.Data.Shared.Statics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HannaHabitsService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DailyDiaryController : ControllerBase
    {
        private readonly IDailyDiaryRepository _dailyDiaryRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<DailyDiaryController> _logger;

        public DailyDiaryController(IDailyDiaryRepository dailyDiaryRepository, IMapper mapper, ILogger<DailyDiaryController> logger)
        {
            _dailyDiaryRepository = dailyDiaryRepository;
            _mapper = mapper;
            _logger = logger;
        }
        
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<DailyDiaryReadOnlyDto>>> GetDailyDiaries(string userId, CancellationToken cancellationToken)
        {
            try
            {
                var dailyDiaries = await _dailyDiaryRepository.GetDailyDiariesByUserIdAsync(userId, cancellationToken);
                var dtos = _mapper.Map<IEnumerable<DailyDiaryReadOnlyDto>>(dailyDiaries);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing GET in {nameof(GetDailyDiaries)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }
        
        [HttpGet("day/{userId}/{day:datetime}")]
        public async Task<ActionResult<DailyDiaryReadOnlyDto>> GetDailyDiaryByDay(string userId, DateTime day, CancellationToken cancellationToken)
        {
            try
            {
                var dailyDiary = await _dailyDiaryRepository.GetDailyDiaryByDayAsync(userId, day, cancellationToken);

                if (dailyDiary == null)
                {
                    _logger.LogWarning("Record Not Found: {Name} - ID: {Id} in {Day}. Create new", nameof(GetDailyDiaryByDay), userId, day);
                    var newDailyDiary = new DailyDiary()
                    {
                        UserId = userId,
                        Date = day
                    };
                    await _dailyDiaryRepository.AddAsync(newDailyDiary, cancellationToken);
                    return CreatedAtAction(nameof(GetDailyDiary), new { id = newDailyDiary.Id }, newDailyDiary);
                }

                var dto = _mapper.Map<DailyDiaryReadOnlyDto>(dailyDiary);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing GET in {nameof(GetDailyDiaryByDay)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }
        
        [HttpGet("{id:int}")]
        public async Task<ActionResult<DailyDiaryReadOnlyDto>> GetDailyDiary(int id, CancellationToken cancellationToken)
        {
            try
            {
                var dailyDiary = await _dailyDiaryRepository.GetDailyDiaryByIdAsync(id, cancellationToken);

                if (dailyDiary == null)
                {
                    _logger.LogWarning("Record Not Found: {Name} - ID: {Id}", nameof(GetDailyDiary), id);
                    return NotFound();
                }

                var dto = _mapper.Map<DailyDiaryReadOnlyDto>(dailyDiary);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing GET in {nameof(GetDailyDiary)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }
        
        [HttpGet("calendar/{userId}")]
        public async Task<ActionResult<IEnumerable<DailyDiaryReadOnlyDto>>> GetDailyDiaryIdForCalendar(string userId, CancellationToken cancellationToken)
        {
            try
            {
                var calendarItems = await _dailyDiaryRepository.GetDailyDiaryIdsForCalendarAsync(userId, cancellationToken);
                return Ok(calendarItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing GET in {nameof(GetDailyDiaryIdForCalendar)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }
        
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutDailyDiary(int id, DailyDiaryUpdateDto dailyDiaryDto, CancellationToken cancellationToken)
        {
            if (id != dailyDiaryDto.Id)
            {
                _logger.LogWarning("Update ID invalid in {Name} - ID: {Id}", nameof(PutDailyDiary), id);
                return BadRequest();
            }
            
            var dailyDiary = await _dailyDiaryRepository.GetDailyDiaryByIdAsync(id, cancellationToken);

            if (dailyDiary == null)
            {
                _logger.LogWarning("{DailyDiary} record not found in {Name} - ID: {Id}", nameof(DailyDiary), nameof(PutDailyDiary), id);
                return NotFound();
            }

            _mapper.Map(dailyDiaryDto, dailyDiary);

            try
            {
                await _dailyDiaryRepository.UpdateAsync(dailyDiary, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!await DailyDiaryExists(id, cancellationToken))
                {
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, $"Error Performing PUT in {nameof(PutDailyDiary)}");
                    return StatusCode(500, Messages.Error500Message);
                }
            }

            return Ok(_mapper.Map<DailyDiaryReadOnlyDto>(dailyDiary));
        }
        
        [HttpPost]
        public async Task<ActionResult<DailyDiaryCreateDto>> PostDailyDiary(DailyDiaryCreateDto dailyDiaryDto, CancellationToken cancellationToken)
        {
            try
            {
                var dailyDiary = _mapper.Map<DailyDiary>(dailyDiaryDto);
                await _dailyDiaryRepository.AddAsync(dailyDiary, cancellationToken);
                return CreatedAtAction(nameof(GetDailyDiary), new { id = dailyDiary.Id }, dailyDiary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing POST in {nameof(PostDailyDiary)}");
                return StatusCode(500, Messages.Error500Message);
            }
            
        }
        
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteDailyDiary(int id, CancellationToken cancellationToken)
        {
            try
            {
                var dailyDiary = await _dailyDiaryRepository.GetAsync(id, cancellationToken);
                if (dailyDiary == null)
                {
                    _logger.LogWarning("{DailyDiaryName} record not found in {Name} - ID: {Id}", nameof(DailyDiary), nameof(DeleteDailyDiary), id);
                    return NotFound();
                }

                await _dailyDiaryRepository.DeleteAsync(id, cancellationToken);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing DELETE in {nameof(DeleteDailyDiary)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }

        private async Task<bool> DailyDiaryExists(int id, CancellationToken cancellationToken)
        {
            return await _dailyDiaryRepository.Exists(id, cancellationToken);
        }
    }
}
