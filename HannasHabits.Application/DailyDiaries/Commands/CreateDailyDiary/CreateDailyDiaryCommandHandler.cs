using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MapsterMapper;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;

public class CreateDailyDiaryCommandHandler : IRequestHandler<CreateDailyDiaryCommand, CreateDailyDiaryDto>
{
    private readonly IDailyDiaryRepository _dailyDiaries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;

    public CreateDailyDiaryCommandHandler(IDailyDiaryRepository dailyDiaries, IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
    {
        _dailyDiaries = dailyDiaries;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<CreateDailyDiaryDto> Handle(CreateDailyDiaryCommand request, CancellationToken cancellationToken)
    {
        // "One entry per user and day" spans all diaries, so no single DailyDiary can enforce it: it is checked here
        // against the repository. The unique index stays as the safety net for concurrent requests (also a 409).
        if (await _dailyDiaries.ExistsForDateAsync(_currentUser.UserId, request.Date, cancellationToken))
            throw new ConflictException($"A diary entry for {request.Date:O} already exists.");

        var dailyDiary = DailyDiary.Create(_currentUser.UserId, request.Date, request.Text);

        _dailyDiaries.Add(dailyDiary);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CreateDailyDiaryDto>(dailyDiary);
    }
}
