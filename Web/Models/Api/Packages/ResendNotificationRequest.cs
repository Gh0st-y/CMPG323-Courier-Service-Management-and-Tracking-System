namespace CourierService.Web.Models.Api.Packages
{
    /// <summary>Body of POST /api/packages/{f20Identifier}/notifications/resend (docs/API_CONTRACT.md).</summary>
    public class ResendNotificationRequest
    {
        /// <summary>"Email" or "SMS". Optional: Email when left out.</summary>
        public string Channel { get; set; }
    }
}