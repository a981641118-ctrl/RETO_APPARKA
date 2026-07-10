using ApparkaTrainingFlow.Models;

namespace ApparkaTrainingFlow.Services;

public sealed class TrainingStateService(LocalStorageService storage, QrValidationService qrValidator)
{
    private const string StorageKey = "apparkatrainingflow.plan.v1";
    private bool _initialized;

    public TrainingPlan? Plan { get; private set; }
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        Plan = await storage.GetAsync<TrainingPlan>(StorageKey) ?? CreateDemoPlan();
        NormalizePlan(Plan);
        _initialized = true;
        await SaveAsync();
    }

    public TrainingValidation? GetValidation(Guid id) => Plan?.Validations.FirstOrDefault(x => x.Id == id);

    public async Task<OperationResult> StartValidationAsync(Guid id)
    {
        var validation = GetValidation(id);
        if (validation is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        if (validation.Status is not (ValidationStatus.Available or ValidationStatus.ReinforcementRequired or ValidationStatus.InProgress))
        {
            return OperationResult.Fail("Esta validación todavía está bloqueada.");
        }

        if (validation.Status == ValidationStatus.ReinforcementRequired)
        {
            return OperationResult.Fail("Primero debes iniciar el intento de refuerzo.");
        }

        if (validation.Status == ValidationStatus.Available)
        {
            validation.Status = ValidationStatus.InProgress;
            validation.StartedAt = DateTime.Now;
            AddAudit(validation, "Colaborador", "VALIDATION_STARTED", $"Intento {validation.AttemptNumber} iniciado.");
            Plan!.Status = PlanStatus.Active;
        }

        await SaveAsync();
        return OperationResult.Ok("Validación iniciada.");
    }

    public async Task<OperationResult> ValidateQrAsync(Guid id, string? scannedCode)
    {
        var validation = GetValidation(id);
        if (validation is null || Plan is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        if (validation.Status != ValidationStatus.InProgress)
        {
            return OperationResult.Fail("La actividad debe estar en proceso.");
        }

        var result = qrValidator.Validate(scannedCode, Plan.ExpectedQrCode);
        if (!result.IsValid)
        {
            AddAudit(validation, "Colaborador", "QR_REJECTED", result.Message);
            await SaveAsync();
            return OperationResult.Fail(result.Message);
        }

        validation.QrValidated = true;
        validation.QrValidatedAt = DateTime.Now;
        AddAudit(validation, "Colaborador", "QR_VALIDATED", $"Sede validada: {Plan.LocationName}.");
        await SaveAsync();
        return OperationResult.Ok(result.Message);
    }

    public async Task<OperationResult> CompleteTaskAsync(Guid id)
    {
        var validation = GetValidation(id);
        if (validation is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        if (!validation.QrValidated)
        {
            return OperationResult.Fail("Primero debes validar el QR de la sede.");
        }

        validation.TaskCompleted = true;
        validation.TaskCompletedAt = DateTime.Now;
        validation.Status = ValidationStatus.PendingEvaluation;
        AddAudit(validation, "Colaborador", "TASK_COMPLETED", "La tarea práctica fue marcada como ejecutada.");
        await SaveAsync();
        return OperationResult.Ok("Tarea práctica registrada.");
    }

    public async Task<OperationResult> SaveRubricAsync(Guid id)
    {
        var validation = GetValidation(id);
        if (validation is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        if (!validation.TaskCompleted)
        {
            return OperationResult.Fail("La tarea práctica aún no ha sido registrada.");
        }

        if (!validation.RubricCompleted)
        {
            return OperationResult.Fail("Debes evaluar todos los criterios.");
        }

        var missingObservation = validation.Rubric.Any(x =>
            (x.Rating is RubricRating.PartiallyComplies or RubricRating.DoesNotComply) &&
            string.IsNullOrWhiteSpace(x.Observation));

        if (missingObservation)
        {
            return OperationResult.Fail("Agrega una observación en los criterios que no cumplen totalmente.");
        }

        validation.ReinforcementRequired = validation.HasCriticalFailure;
        AddAudit(
            validation,
            "Tutor",
            "RUBRIC_SAVED",
            validation.HasCriticalFailure
                ? "Rúbrica registrada con incumplimiento crítico."
                : "Rúbrica registrada sin incumplimientos críticos.");

        await SaveAsync();
        return OperationResult.Ok("Rúbrica guardada.");
    }

    public async Task<OperationResult> SubmitQuizAsync(Guid id)
    {
        var validation = GetValidation(id);
        if (validation is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        if (!validation.RubricCompleted)
        {
            return OperationResult.Fail("La rúbrica debe completarse antes de la evaluación.");
        }

        if (validation.Questions.Any(x => x.SelectedOptionIndex is null))
        {
            return OperationResult.Fail("Responde todas las preguntas.");
        }

        var correctAnswers = validation.Questions.Count(x => x.SelectedOptionIndex == x.CorrectOptionIndex);
        validation.QuizScore = (int)Math.Round(correctAnswers * 100d / validation.Questions.Count);
        validation.QuizSubmitted = true;

        if (validation.QuizScore < 70)
        {
            validation.ReinforcementRequired = true;
        }

        AddAudit(validation, "Colaborador", "QUIZ_SUBMITTED", $"Puntaje obtenido: {validation.QuizScore}%.");
        await SaveAsync();

        return validation.QuizScore >= 70
            ? OperationResult.Ok($"Evaluación aprobada con {validation.QuizScore}%.")
            : OperationResult.Fail($"Puntaje {validation.QuizScore}%. Se requiere refuerzo.");
    }

    public async Task<OperationResult> SetTutorConfirmationAsync(Guid id, bool value)
    {
        var validation = GetValidation(id);
        if (validation is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        validation.TutorConfirmed = value;
        AddAudit(validation, "Tutor", "TUTOR_CONFIRMATION", value ? "Evaluación confirmada." : "Confirmación retirada.");
        await SaveAsync();
        return OperationResult.Ok("Confirmación del tutor actualizada.");
    }

    public async Task<OperationResult> SetCollaboratorConfirmationAsync(Guid id, bool value)
    {
        var validation = GetValidation(id);
        if (validation is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        validation.CollaboratorConfirmed = value;
        AddAudit(validation, "Colaborador", "COLLABORATOR_CONFIRMATION", value ? "Retroalimentación confirmada." : "Confirmación retirada.");
        await SaveAsync();
        return OperationResult.Ok("Confirmación del colaborador actualizada.");
    }

    public async Task<OperationResult> CloseValidationAsync(Guid id)
    {
        var validation = GetValidation(id);
        if (validation is null || Plan is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        if (!validation.IsEvidenceComplete)
        {
            return OperationResult.Fail("La evidencia está incompleta. Revisa todos los pasos obligatorios.");
        }

        if (validation.ReinforcementRequired || validation.HasCriticalFailure || validation.QuizScore < 70)
        {
            validation.Status = ValidationStatus.ReinforcementRequired;
            Plan.Status = PlanStatus.AtRisk;
            AddAudit(validation, "Sistema", "REINFORCEMENT_REQUIRED", "La validación fue derivada a refuerzo.");
            await SaveAsync();
            return OperationResult.Fail("La validación requiere refuerzo antes de continuar.");
        }

        validation.Status = ValidationStatus.Completed;
        validation.CompletedAt = DateTime.Now;
        AddAudit(validation, "Sistema", "VALIDATION_COMPLETED", "La evidencia obligatoria fue validada.");

        UnlockNextValidation(validation.Number);

        if (Plan.Validations.All(x => x.Status == ValidationStatus.Completed))
        {
            Plan.Status = PlanStatus.Completed;
        }
        else
        {
            Plan.Status = PlanStatus.Active;
        }

        await SaveAsync();
        return OperationResult.Ok("Validación completada. Se habilitó la siguiente actividad.");
    }

    public async Task<OperationResult> BeginReinforcementAsync(Guid id)
    {
        var validation = GetValidation(id);
        if (validation is null)
        {
            return OperationResult.Fail("No se encontró la validación.");
        }

        if (validation.Status != ValidationStatus.ReinforcementRequired)
        {
            return OperationResult.Fail("La validación no se encuentra en refuerzo.");
        }

        validation.AttemptNumber++;
        validation.Status = ValidationStatus.InProgress;
        validation.StartedAt = DateTime.Now;
        validation.CompletedAt = null;
        validation.QrValidated = false;
        validation.QrValidatedAt = null;
        validation.TaskCompleted = false;
        validation.TaskCompletedAt = null;
        validation.TutorConfirmed = false;
        validation.CollaboratorConfirmed = false;
        validation.ReinforcementRequired = false;
        validation.QuizScore = 0;
        validation.QuizSubmitted = false;

        foreach (var item in validation.Rubric)
        {
            item.Rating = RubricRating.NotEvaluated;
            item.Observation = string.Empty;
        }

        foreach (var question in validation.Questions)
        {
            question.SelectedOptionIndex = null;
        }

        AddAudit(validation, "Supervisor", "REINFORCEMENT_STARTED", $"Se inició el intento {validation.AttemptNumber}.");
        await SaveAsync();
        return OperationResult.Ok("Intento de refuerzo iniciado.");
    }

    public IReadOnlyList<SupervisorAlert> GetAlerts()
    {
        if (Plan is null)
        {
            return [];
        }

        var alerts = new List<SupervisorAlert>();

        foreach (var validation in Plan.Validations)
        {
            if (validation.Status == ValidationStatus.ReinforcementRequired)
            {
                alerts.Add(new SupervisorAlert
                {
                    ValidationId = validation.Id,
                    Severity = "Alta",
                    Title = $"Validación {validation.Number} requiere refuerzo",
                    Detail = $"{Plan.CollaboratorName}: {validation.Title}"
                });
            }

            if (validation.Status is not (ValidationStatus.Completed or ValidationStatus.Locked) && validation.DueDate.Date < DateTime.Today)
            {
                alerts.Add(new SupervisorAlert
                {
                    ValidationId = validation.Id,
                    Severity = "Media",
                    Title = $"Validación {validation.Number} vencida",
                    Detail = $"La fecha límite fue {validation.DueDate:dd/MM/yyyy}."
                });
            }

            if (validation.Audit.Count(x => x.Action == "QR_REJECTED") >= 2)
            {
                alerts.Add(new SupervisorAlert
                {
                    ValidationId = validation.Id,
                    Severity = "Media",
                    Title = "Intentos de QR inválido",
                    Detail = $"Se detectaron varios intentos en la validación {validation.Number}."
                });
            }
        }

        return alerts;
    }

    public async Task ResetDemoAsync()
    {
        Plan = CreateDemoPlan();
        _initialized = true;
        await SaveAsync();
    }

    private void UnlockNextValidation(int completedNumber)
    {
        if (Plan is null)
        {
            return;
        }

        var next = Plan.Validations.FirstOrDefault(x => x.Number == completedNumber + 1);
        if (next is not null && next.Status == ValidationStatus.Locked)
        {
            next.Status = ValidationStatus.Available;
            AddAudit(next, "Sistema", "VALIDATION_UNLOCKED", "La validación anterior fue completada.");
        }
    }

    private async Task SaveAsync()
    {
        if (Plan is null)
        {
            return;
        }

        await storage.SetAsync(StorageKey, Plan);
        Changed?.Invoke();
    }

    private static void NormalizePlan(TrainingPlan plan)
    {
        foreach (var validation in plan.Validations)
        {
            validation.Rubric ??= [];
            validation.Questions ??= [];
            validation.Audit ??= [];
        }
    }

    private static void AddAudit(TrainingValidation validation, string actor, string action, string detail)
    {
        validation.Audit.Add(new AuditEntry
        {
            Actor = actor,
            Action = action,
            Detail = detail,
            Timestamp = DateTime.Now
        });
    }

    private static TrainingPlan CreateDemoPlan()
    {
        var start = DateTime.Today;
        var plan = new TrainingPlan
        {
            CollaboratorName = "María Torres",
            PositionName = "Operadora de estacionamiento",
            LocationName = "Sede Lima Centro",
            ExpectedQrCode = "APPARKA-SEDE-LIMA-001",
            StartDate = start,
            EndDate = start.AddDays(20),
            Status = PlanStatus.Assigned
        };

        var definitions = new[]
        {
            (1, 1, "Reconocimiento y seguridad", "Identifica el área, los equipos y aplica los protocolos básicos."),
            (2, 1, "Registro básico", "Realiza correctamente un registro de ingreso o salida."),
            (3, 2, "Operación acompañada", "Ejecuta una operación real utilizando el procedimiento establecido."),
            (4, 2, "Atención de incidencia frecuente", "Registra y comunica correctamente una situación habitual."),
            (5, 3, "Operación con mínima asistencia", "Completa el proceso con autonomía y sin errores críticos."),
            (6, 3, "Validación final", "Demuestra dominio integral del puesto y de la atención al cliente.")
        };

        foreach (var definition in definitions)
        {
            var availableFrom = start.AddDays((definition.Item2 - 1) * 7 + (definition.Item1 % 2 == 0 ? 3 : 0));
            var validation = new TrainingValidation
            {
                Number = definition.Item1,
                Week = definition.Item2,
                Title = definition.Item3,
                Description = definition.Item4,
                AvailableFrom = availableFrom,
                DueDate = availableFrom.AddDays(3),
                Status = definition.Item1 == 1 ? ValidationStatus.Available : ValidationStatus.Locked,
                Rubric = CreateRubric(),
                Questions = CreateQuestions()
            };

            AddAudit(validation, "Sistema", definition.Item1 == 1 ? "VALIDATION_AVAILABLE" : "VALIDATION_LOCKED", "Plan creado automáticamente.");
            plan.Validations.Add(validation);
        }

        return plan;
    }

    private static List<RubricEvaluation> CreateRubric() =>
    [
        new() { Criterion = "Sigue el procedimiento establecido", IsCritical = true },
        new() { Criterion = "Utiliza correctamente el sistema o equipo", IsCritical = true },
        new() { Criterion = "Cumple las medidas de seguridad", IsCritical = true },
        new() { Criterion = "Brinda una atención adecuada", IsCritical = false },
        new() { Criterion = "Finaliza la operación sin errores", IsCritical = false }
    ];

    private static List<QuizQuestion> CreateQuestions() =>
    [
        new()
        {
            Text = "¿Qué debe hacerse antes de iniciar una operación?",
            Options = ["Validar el área y el procedimiento", "Omitir el registro", "Esperar el cierre de turno"],
            CorrectOptionIndex = 0
        },
        new()
        {
            Text = "Si ocurre una incidencia, ¿cuál es la primera acción correcta?",
            Options = ["Ocultarla", "Aplicar el protocolo y comunicarla", "Abandonar el puesto"],
            CorrectOptionIndex = 1
        },
        new()
        {
            Text = "¿Qué evidencia reemplaza al video en esta plataforma?",
            Options = ["Solo una fotografía", "QR, desempeño, rúbrica y evaluación", "Un mensaje verbal"],
            CorrectOptionIndex = 1
        }
    ];
}

public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
