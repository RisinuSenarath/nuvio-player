using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuvioPlayer.Interfaces;

public interface IFileAssociationService
{
    IReadOnlyList<string> SupportedExtensions { get; }
    Task<Dictionary<string, bool>> GetAssociationStatusesAsync();
    Task<bool> RegisterAssociationAsync(string extension);
    Task<bool> UnregisterAssociationAsync(string extension);
    Task<bool> RegisterAllAssociationsAsync();
    Task<bool> UnregisterAllAssociationsAsync();
}
