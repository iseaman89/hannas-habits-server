using FluentValidation;
using MediatR;

namespace HannasHabits.Application.Common.Behaviors;

public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }
    
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            // One context per validator: a context collects the failures, so sharing it would report every failure once
            // per validator (and let the validators, which run in parallel, write into the same list).
            var results = await Task.WhenAll(_validators.Select(v =>
                v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)));
            
            var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();
            
            if (failures.Count != 0) throw new ValidationException(failures);
        }
        
        return await next(cancellationToken);
    }
}