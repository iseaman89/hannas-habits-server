using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;

public class DeleteDailyDiaryCommandHandler : IRequestHandler<DeleteDailyDiaryCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;

    public DeleteDailyDiaryCommandHandler(IApplicationDbContext context, IUserContextService userContextService)
    {
        _context = context;
        _userContextService = userContextService;
    }
    
    public async Task<Unit> Handle(DeleteDailyDiaryCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var dailyDiary = await _context.DailyDiaries
            .Where(d => d.Id == request.Id && d.UserId == userId.Value)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (dailyDiary is null) throw new NotFoundException(nameof(DailyDiary), request.Id);
        
        _context.DailyDiaries.Remove(dailyDiary);
        await _context.SaveChangesAsync(cancellationToken);
        
        return Unit.Value;
    }
}