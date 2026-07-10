namespace TolllgaFinale.Services;

/// <summary>
/// Encapsulates platform-specific USB permission handling.
/// Android uses this to request access to a USB printer before sending data.
/// </summary>
public interface IUsbPermissionService
{
    Task<bool> EnsurePermissionAsync(int vendorId, int productId, CancellationToken cancellationToken = default);
}
