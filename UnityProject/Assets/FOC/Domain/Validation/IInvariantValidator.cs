namespace FOC.Domain.Validation
{
    public interface IInvariantValidator<in T>
    {
        ValidationResult Validate(T subject);
    }
}

