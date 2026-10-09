namespace CourierService.Web.Models.Api.Packages
{
    /// <summary>Body of PATCH /api/packages/{f20Identifier}/payment (docs/API_CONTRACT.md).</summary>
    public class PaymentStatusRequest
    {
        /// <summary>"Paid", "Unpaid" or "Exempt".</summary>
        public string PaymentStatus { get; set; }
    }
}