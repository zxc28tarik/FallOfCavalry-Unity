using System;

namespace FOC.Domain.Validation
{
    public sealed class ValidationIssue
    {
        public ValidationIssue(string code, string message, ValidationSeverity severity)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Validation issue code is required.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Validation issue message is required.", nameof(message));
            }

            Code = code;
            Message = message;
            Severity = severity;
        }

        public string Code { get; }

        public string Message { get; }

        public ValidationSeverity Severity { get; }
    }
}

