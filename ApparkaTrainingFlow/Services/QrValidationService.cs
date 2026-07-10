namespace ApparkaTrainingFlow.Services;

public sealed class QrValidationService
{
    public QrValidationResult Validate(string? scannedCode, string expectedCode)
    {
        if (string.IsNullOrWhiteSpace(scannedCode))
        {
            return new(false, "Debes escanear o ingresar un código QR.");
        }

        if (!string.Equals(scannedCode.Trim(), expectedCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new(false, "El QR no corresponde a la sede asignada.");
        }

        return new(true, "QR validado correctamente.");
    }
}

public sealed record QrValidationResult(bool IsValid, string Message);
