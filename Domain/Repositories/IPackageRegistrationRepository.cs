using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    /// <summary>
    /// The extra writes that registering a package needs (FR-03). Kept apart from IPackageRepository so the
    /// existing package code and its test fakes don't change.
    /// </summary>
    public interface IPackageRegistrationRepository
    {
        /// <summary>Saves the recipient's details for this package and returns the new RecipientId.</summary>
        int InsertRecipient(Recipient recipient, IUnitOfWork unitOfWork);

        /// <summary>
        /// The next free number for an F20-nnnn identifier: one more than the highest number already used.
        /// Call it inside the registration's unit of work: it locks the range, so two clerks registering at the
        /// same moment can't be given the same number.
        /// </summary>
        int NextF20Number(IUnitOfWork unitOfWork);
    }
}