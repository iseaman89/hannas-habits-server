using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MapsterMapper;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;

public class CreateDailyDiaryCommandHandler : IRequestHandler<CreateDailyDiaryCommand, CreateDailyDiaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUserContextService _userContextService;

    public CreateDailyDiaryCommandHandler(IApplicationDbContext context, IMapper mapper, IUserContextService userContextService)
    {
        _context = context;
        _mapper = mapper;
        _userContextService = userContextService;
    }
    
    public async Task<CreateDailyDiaryDto> Handle(CreateDailyDiaryCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var dailyDiary = DailyDiary.Create(userId.Value, request.Date, request.Text);
        
        _context.DailyDiaries.Add(dailyDiary);
        await _context.SaveChangesAsync(cancellationToken);
        
        return _mapper.Map<CreateDailyDiaryDto>(dailyDiary);
    }
}