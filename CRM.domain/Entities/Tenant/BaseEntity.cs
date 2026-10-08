using System;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// Shared base for every entity in the system. Putting audit fields here
    /// once (instead of on every class) is the OOP "inheritance over repetition"
    /// principle at work - every entity automatically knows when it was created
    /// and last touched, with zero extra code per entity.
    /// </summary>
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false; // soft delete - never lose shop history

        // Multi-tenancy: every business record belongs to exactly one
        // Company (shop). This is how multiple Owners can share one
        // database while staying completely isolated from each other -
        // every query filters by CompanyId.
        public int CompanyId { get; set; }
    }
}
