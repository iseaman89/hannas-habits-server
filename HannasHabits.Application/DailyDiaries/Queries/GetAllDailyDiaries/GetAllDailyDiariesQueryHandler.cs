using HannasHabits.Application.Common.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;

public class GetAllDailyDiariesQueryHandler : IRequestHandler<GetAllDailyDiariesQuery, List<DailyDiaryListItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetAllDailyDiariesQueryHandler(IApplicationDbContext context, IUserContextService userContextService, IMapper mapper)
    {
        _context = context;
        _userContextService = userContextService;
        _mapper = mapper;
    }
    
    public async Task<List<DailyDiaryListItemDto>> Handle(GetAllDailyDiariesQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var dailyDiaries = await _context.DailyDiaries
            .Where(d => d.UserId == userId.Value)
            .ToListAsync(cancellationToken);
        
        return _mapper.Map<List<DailyDiaryListItemDto>>(dailyDiaries);
    }
}