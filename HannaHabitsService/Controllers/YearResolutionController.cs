using AutoMapper;
using HannaHabitsService.Dtos.DailyDiaries;
using HannaHabitsService.Dtos.YearResolutions;
using HannaHabitsService.Models;
using HannaHabitsService.Repositories.YearResolutions;
using HannasHabits.Data.Shared.Statics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HannaHabitsService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class YearResolutionController : ControllerBase
{
    private readonly IYearResolutionRepository _yearResolutionRepository;
    private readonly ILogger<YearResolutionController> _logger;
    private readonly IMapper _mapper;

    public YearResolutionController(IYearResolutionRepository yearResolutionRepository, ILogger<YearResolutionController> logger, IMapper mapper)
    {
        _yearResolutionRepository = yearResolutionRepository;
        _logger = logger;
        _mapper = mapper;
    }
    
     [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<YearResolutionReadOnlyDto>>> GetYearResolutions(string userId, CancellationToken cancellationToken)
        {
            try
            {
                var yearResolutions = await _yearResolutionRepository.GetYearResolutionsByUserIdAsync(userId, cancellationToken);

                if (!yearResolutions.Any())
                {
                    var newYearResolution = new YearResolution()
                    {
                        UserId = userId,
                        Year = DateTime.UtcNow.Year
                    };
                    await _yearResolutionRepository.AddAsync(newYearResolution, cancellationToken);
                    return Ok(new List<YearResolution>(){newYearResolution});
                }
                
                var dtos = _mapper.Map<IEnumerable<YearResolutionReadOnlyDto>>(yearResolutions);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing GET in {nameof(GetYearResolutions)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }
        
        [HttpGet("{id:int}")]
        public async Task<ActionResult<YearResolutionReadOnlyDto>> GetYearResolution(int id, CancellationToken cancellationToken)
        {
            try
            {
                var yearResolution = await _yearResolutionRepository.GetYearResolutionByIdAsync(id, cancellationToken);

                if (yearResolution == null)
                {
                    _logger.LogWarning("Record Not Found: {Name} - ID: {Id}", nameof(GetYearResolution), id);
                    return NotFound();
                }

                var dto = _mapper.Map<YearResolutionReadOnlyDto>(yearResolution);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing GET in {nameof(GetYearResolution)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }
        
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutYearResolution(int id, YearResolutionUpdateDto yearResolutionDto, CancellationToken cancellationToken)
        {
            if (id != yearResolutionDto.Id)
            {
                _logger.LogWarning("Update ID invalid in {Name} - ID: {Id}", nameof(PutYearResolution), id);
                return BadRequest();
            }
            
            var yearResolution = await _yearResolutionRepository.GetYearResolutionByIdAsync(id, cancellationToken);

            if (yearResolution == null)
            {
                _logger.LogWarning("{YearResolution} record not found in {Name} - ID: {Id}", nameof(YearResolution), nameof(PutYearResolution), id);
                return NotFound();
            }

            _mapper.Map(yearResolutionDto, yearResolution);

            try
            {
                await _yearResolutionRepository.UpdateAsync(yearResolution, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!await YearResolutionExists(id, cancellationToken))
                {
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, $"Error Performing PUT in {nameof(PutYearResolution)}");
                    return StatusCode(500, Messages.Error500Message);
                }
            }

            return Ok(_mapper.Map<YearResolutionReadOnlyDto>(yearResolution));
        }
        
        [HttpPost]
        public async Task<ActionResult<YearResolutionCreateDto>> PostYearResolution(YearResolutionCreateDto yearResolutionDto, CancellationToken cancellationToken)
        {
            try
            {
                var yearResolution = _mapper.Map<YearResolution>(yearResolutionDto);
                await _yearResolutionRepository.AddAsync(yearResolution, cancellationToken);
                return CreatedAtAction(nameof(GetYearResolution), new { id = yearResolution.Id }, yearResolution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing POST in {nameof(PostYearResolution)}");
                return StatusCode(500, Messages.Error500Message);
            }
            
        }
        
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteYearResolution(int id, CancellationToken cancellationToken)
        {
            try
            {
                var yearResolution = await _yearResolutionRepository.GetAsync(id, cancellationToken);
                if (yearResolution == null)
                {
                    _logger.LogWarning("{YearResolution} record not found in {Name} - ID: {Id}", nameof(YearResolution), nameof(DeleteYearResolution), id);
                    return NotFound();
                }

                await _yearResolutionRepository.DeleteAsync(id, cancellationToken);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Performing DELETE in {nameof(DeleteYearResolution)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }

        private async Task<bool> YearResolutionExists(int id, CancellationToken cancellationToken)
        {
            return await _yearResolutionRepository.Exists(id, cancellationToken);
        }
}