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
        var dailyDiary = DailyDiary.Create(_currentUser.UserId, request.Date, request.Text);

        _dailyDiaries.Add(dailyDiary);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CreateDailyDiaryDto>(dailyDiary);
    }
}
