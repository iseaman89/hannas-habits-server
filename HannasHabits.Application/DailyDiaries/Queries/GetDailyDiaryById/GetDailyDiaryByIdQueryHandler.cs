using HannasHabits.Application.Common.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;

public class GetDailyDiaryByIdQueryHandler : IRequestHandler<GetDailyDiaryByIdQuery, DailyDiaryDetailsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetDailyDiaryByIdQueryHandler(IApplicationDbContext context, IUserContextService userContextService, IMapper mapper)
    {
        _context = context;
        _userContextService = userContextService;
        _mapper = mapper;
    }
    
    public async Task<DailyDiaryDetailsDto> Handle(GetDailyDiaryByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var dailyDiary = await _context.DailyDiaries
            .Where(d => d.Id == request.Id && d.UserId == userId.Value)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (dailyDiary is null) throw new Exception("DailyDiary not found");
        
        return _mapper.Map<DailyDiaryDetailsDto>(dailyDiary);
    }
}