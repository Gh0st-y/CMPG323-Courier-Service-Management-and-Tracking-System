using System;

namespace CourierService.Services.Diagnostics
{
    /// <summary>Preserves failure locations without recording exception messages or inner exception data.</summary>
    public static class ExceptionDiagnostics
    {
        public static string Format(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return exception.GetType().FullName + Environment.NewLine + exception.StackTrace;
        }
    }
}
