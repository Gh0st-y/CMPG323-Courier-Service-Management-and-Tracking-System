namespace CourierService.Domain
{
    /// <summary>Starts a new <see cref="IUnitOfWork"/> for a multi-step, multi-repository operation.</summary>
    public interface IUnitOfWorkFactory
    {
        IUnitOfWork Begin();
    }
}
