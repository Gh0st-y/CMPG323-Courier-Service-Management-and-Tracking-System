namespace CourierService.Domain.Repositories
{
    /// <summary>Reads settings from dbo.AppConfig (OR-01), e.g. the fees, so they change without a redeploy.</summary>
    public interface IAppConfigRepository
    {
        /// <summary>The value for a key, or null if the key isn't there.</summary>
        string GetValue(string key);
    }
}