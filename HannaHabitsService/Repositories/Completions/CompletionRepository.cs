using HannaHabitsService.Data;
using HannaHabitsService.Models;

namespace HannaHabitsService.Repositories.Completions;

public class CompletionRepository(HannaDbContext context)
    : GenericRepository<Completion>(context), ICompletionRepository
{
    private readonly HannaDbContext _context = context;
}