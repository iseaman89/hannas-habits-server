using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.DailyDiaries.Commands.UpdateDailyDiary;

public class UpdateDailyDiaryCommandHandler : IRequestHandler<UpdateDailyDiaryCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;

    public UpdateDailyDiaryCommandHandler(IApplicationDbContext context, IUserContextService userContextService)
    {
        _context = context;
        _userContextService = userContextService;
    }
    
    public async Task<Unit> Handle(UpdateDailyDiaryCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var dailyDiary = await _context.DailyDiaries
            .Where(d => d.Id == request.Id && d.UserId == userId.Value)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (dailyDiary is null) throw new NotFoundException(nameof(DailyDiary), request.Id);
        
        dailyDiary.Update(request.Text);
        
        await _context.SaveChangesAsync(cancellationToken);
        
        return Unit.Value;
    }
}