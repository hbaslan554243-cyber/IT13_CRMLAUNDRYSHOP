using System.ComponentModel.DataAnnotations;

namespace CRM.domain.Entities.Tenant;

/// <summary>Central platform record for a tenant database and its subscription.</summary>
public class TenantRegistry
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string TenantKey { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
    public bool AllowStaffAccounts { get; set; } = true;
    public bool AllowManagerAccounts { get; set; } = true;
    public string? TermsAndConditions { get; set; }
    public int TermsVersion { get; set; } = 1;
    public int MaxBranches { get; set; } = 1;
}
