using System;
using System.Collections.Generic;

namespace FOC.Domain.Validation
{
    public sealed class ValidationResult
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();

        public IReadOnlyList<ValidationIssue> Issues => _issues;

        public bool IsValid
        {
            get
            {
                foreach (var issue in _issues)
                {
                    if (issue.Severity == ValidationSeverity.Error)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void Add(ValidationIssue issue)
        {
            _issues.Add(issue ?? throw new ArgumentNullException(nameof(issue)));
        }

        public void AddError(string code, string message) => Add(new ValidationIssue(code, message, ValidationSeverity.Error));

        public void AddWarning(string code, string message) => Add(new ValidationIssue(code, message, ValidationSeverity.Warning));

        public void Merge(ValidationResult other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            foreach (var issue in other.Issues)
            {
                _issues.Add(issue);
            }
        }
    }
}

