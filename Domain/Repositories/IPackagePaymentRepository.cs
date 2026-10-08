using CourierService.Domain.Models;

namespace CourierService.Domain.Repositories
{
    /// <summary>
    /// Payment status of a package (T41, FR-15, FR-17). Separate from IPackageRepository so the many test fakes of that
    /// interface don't all need new members.
    /// </summary>
    public interface IPackagePaymentRepository
    {
        /// <summary>The payment status with who and when it was last changed. Null if the package doesn't exist.</summary>
        PaymentInfo GetPaymentInfo(int packageId, IUnitOfWork unitOfWork = null);

        /// <summary>Stores the new status, the time (database clock) and the staff member. False if the package doesn't exist.</summary>
        bool UpdatePaymentStatus(int packageId, string paymentStatus, int changedByUserId, IUnitOfWork unitOfWork = null);
    }
}
