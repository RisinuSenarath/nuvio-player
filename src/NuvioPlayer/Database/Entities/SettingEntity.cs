using System.ComponentModel.DataAnnotations;

namespace NuvioPlayer.Database.Entities;

public class SettingEntity
{
    [Key]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
