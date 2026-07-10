using System.Text.Json.Serialization;

namespace ApparkaTrainingFlow.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlanStatus
{
    Draft,
    Assigned,
    Active,
    AtRisk,
    Completed,
    Failed,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationStatus
{
    Locked,
    Available,
    InProgress,
    PendingEvaluation,
    ReinforcementRequired,
    Completed,
    Expired,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RubricRating
{
    NotEvaluated,
    Complies,
    PartiallyComplies,
    DoesNotComply
}

public sealed class TrainingPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CollaboratorName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string ExpectedQrCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public PlanStatus Status { get; set; } = PlanStatus.Assigned;
    public List<TrainingValidation> Validations { get; set; } = [];

    [JsonIgnore]
    public int CompletedCount => Validations.Count(x => x.Status == ValidationStatus.Completed);

    [JsonIgnore]
    public int ProgressPercentage => Validations.Count == 0
        ? 0
        : (int)Math.Round(CompletedCount * 100d / Validations.Count);
}

public sealed class TrainingValidation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public int Week { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime AvailableFrom { get; set; }
    public DateTime DueDate { get; set; }
    public ValidationStatus Status { get; set; } = ValidationStatus.Locked;
    public int AttemptNumber { get; set; } = 1;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool QrValidated { get; set; }
    public DateTime? QrValidatedAt { get; set; }
    public bool TaskCompleted { get; set; }
    public DateTime? TaskCompletedAt { get; set; }
    public bool TutorConfirmed { get; set; }
    public bool CollaboratorConfirmed { get; set; }
    public bool ReinforcementRequired { get; set; }
    public int QuizScore { get; set; }
    public bool QuizSubmitted { get; set; }
    public List<RubricEvaluation> Rubric { get; set; } = [];
    public List<QuizQuestion> Questions { get; set; } = [];
    public List<AuditEntry> Audit { get; set; } = [];

    [JsonIgnore]
    public bool RubricCompleted => Rubric.Count > 0 && Rubric.All(x => x.Rating != RubricRating.NotEvaluated);

    [JsonIgnore]
    public bool HasCriticalFailure => Rubric.Any(x => x.IsCritical && x.Rating == RubricRating.DoesNotComply);

    [JsonIgnore]
    public bool IsEvidenceComplete =>
        QrValidated &&
        TaskCompleted &&
        RubricCompleted &&
        QuizSubmitted &&
        TutorConfirmed &&
        CollaboratorConfirmed;
}

public sealed class RubricEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Criterion { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public RubricRating Rating { get; set; } = RubricRating.NotEvaluated;
    public string Observation { get; set; } = string.Empty;
}

public sealed class QuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    public int CorrectOptionIndex { get; set; }
    public int? SelectedOptionIndex { get; set; }
}

public sealed class AuditEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class SupervisorAlert
{
    public Guid ValidationId { get; set; }
    public string Severity { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
