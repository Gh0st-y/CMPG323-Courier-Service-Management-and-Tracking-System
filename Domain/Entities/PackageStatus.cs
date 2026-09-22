namespace CourierService.Domain.Entities
{
    /// <summary>
    /// The 4-state lifecycle from DECISIONS.md #7. Stored in dbo.Packages.Status as the
    /// matching string (enum name) — repositories convert to/from string at the DB boundary.
    /// Transition validation belongs to the Services layer, not here or in Data.
    /// </summary>
    public enum PackageStatus
    {
        Registered,
        InStorage,
        ReadyForCollection,
        Collected
    }
}
