using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.Habits;
using HannasHabits.Application.Habits.Queries.GetAllHabits;
using HannasHabits.Application.Habits.Queries.GetHabitById;
using HannasHabits.Application.Habits.Queries.GetHabitsOverview;
using HannasHabits.Application.Habits.Recurrence;
using HannasHabits.Application.SubHabits;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Queries;

public class HabitQueries : IHabitQueries
{
    private readonly ApplicationDbContext _context;

    public HabitQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<HabitListItemDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => _context.Habits
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .Select(h => new HabitListItemDto(h.Id, h.Title.Value, h.Description, RecurrenceDto.From(h.Schedule)))
            .ToListAsync(cancellationToken);

    public Task<HabitDetailsDto?> GetByIdAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => _context.Habits
            .AsNoTracking()
            .Where(h => h.Id == habitId && h.UserId == userId)
            .Select(h => new HabitDetailsDto(
                h.Id, h.Title.Value, h.Description, RecurrenceDto.From(h.Schedule), h.StartDate, h.CreatedAt,
                h.SubHabits
                    .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
                    .Select(s => new SubHabitDto(s.Id, s.Title.Value, s.Weekdays.Days))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    // Only the dates of the records are read, not the record entities: the streak needs the whole history (it can reach
    // back past the requested month), and a date is all it needs of each. The sub-habits come in the order they were
    // added, each with the dates it was done on.
    public Task<List<HabitWithCompletedDates>> GetWithCompletedDatesAsync(
        Guid userId, CancellationToken cancellationToken)
        => _context.Habits
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
            .Select(h => new HabitWithCompletedDates(
                h.Id, h.Title.Value, h.Schedule, h.StartDate, h.Records.Select(r => r.Date).ToList(),
                h.SubHabits
                    .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
                    .Select(s => new SubHabitWithCompletedDates(
                        s.Id, s.Title.Value, s.Weekdays, s.Records.Select(r => r.Date).ToList()))
                    .ToList()))
            .ToListAsync(cancellationToken);

    public async Task<HabitDayDto?> GetDayAsync(
        Guid userId, Guid habitId, DateOnly date, CancellationToken cancellationToken)
    {
        // One round trip; the user filter sits on the habit, so a foreign or unknown habit yields no row (null).
        var habit = await _context.Habits
            .AsNoTracking()
            .Where(h => h.Id == habitId && h.UserId == userId)
            .Select(h => new
            {
                Completed = h.Records.Any(r => r.Date == date),
                SubHabitIds = h.SubHabits
                    .Where(s => s.Records.Any(r => r.Date == date))
                    .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
                    .Select(s => s.Id)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return habit is null ? null : new HabitDayDto(date, habit.Completed, habit.SubHabitIds);
    }

    // One query for the whole range and all habits (not one per habit); the user filter goes through the part's habit.
    public Task<List<SubHabitNoteRow>> GetSubHabitNotesAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => _context.SubHabitNotes
            .AsNoTracking()
            .Where(n => n.Date >= from && n.Date <= to)
            .Where(n => _context.SubHabits.Any(s => s.Id == n.SubHabitId
                                                    && _context.Habits.Any(h => h.Id == s.HabitId && h.UserId == userId)))
            .OrderBy(n => n.Date).ThenBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .Select(n => new SubHabitNoteRow(n.SubHabitId, n.Date, n.Text))
            .ToListAsync(cancellationToken);

    public async Task<List<HabitRecordDto>?> GetRecordsAsync(
        Guid userId, Guid habitId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        // One round trip: the user filter sits on the habit, so a foreign or unknown habit yields no row (null)
        // while an existing habit without records yields an empty list.
        var habit = await _context.Habits
            .AsNoTracking()
            .Where(h => h.Id == habitId && h.UserId == userId)
            .Select(h => new
            {
                Records = h.Records
                    .Where(r => from == null || r.Date >= from)
                    .Where(r => to == null || r.Date <= to)
                    .OrderBy(r => r.Date)
                    .Select(r => new HabitRecordDto(r.Id, r.HabitId, r.Date))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return habit?.Records;
    }
}
