using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;

namespace CRM.infrastructure.data
{
    /// <summary>
    /// Generates realistic demo data (customers + orders + payments spread
    /// over the last ~90 days) for ONE specific shop, so its reports,
    /// segmentation, and dashboard have something meaningful to show during
    /// a demo. Not part of normal app startup - invoked on demand via
    /// Program.cs "--seed-demo --company=1" (see README).
    /// </summary>
    public static class DemoDataSeeder
    {
        private static readonly string[] FirstNames =
        {
            "Juan", "Maria", "Jose", "Ana", "Pedro", "Rosa", "Carlos", "Elena",
            "Miguel", "Carmen", "Antonio", "Isabel", "Francisco", "Teresa", "Manuel",
            "Sofia", "Ramon", "Luz", "Rafael", "Cristina", "Eduardo", "Patricia",
            "Ricardo", "Gloria", "Fernando", "Beatriz", "Andres", "Lourdes", "Victor", "Angela"
        };

        private static readonly string[] LastNames =
        {
            "Santos", "Reyes", "Cruz", "Bautista", "Garcia", "Torres", "Flores",
            "Ramos", "Villanueva", "Mendoza", "Castillo", "Aquino", "Del Rosario",
            "Fernandez", "Gonzales", "Aguilar", "Domingo", "Pascual", "Rivera", "Salazar"
        };

        public static void Seed(LaundryShopDbContext db, int companyId, int orderCount = 200, int? randomSeed = null)
        {
            if (!db.Companies.Any(c => c.Id == companyId))
                throw new InvalidOperationException($"Company {companyId} does not exist. Log in as that shop's Owner at least once first.");

            var rng = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();

            // TenantDefaults.SeedFor already gives a new shop a starting
            // catalog, but seed one here too in case this company somehow
            // has none yet.
            if (!db.ServiceTypes.Any(s => s.CompanyId == companyId))
                TenantDefaults.SeedFor(db, companyId);

            var services = db.ServiceTypes.Where(s => s.IsActive && s.CompanyId == companyId).ToList();

            // Ensure enough customers exist in THIS shop to spread orders across realistically.
            var customers = db.Customers.Where(c => !c.IsDeleted && c.CompanyId == companyId).ToList();
            const int targetCustomerCount = 40;
            if (customers.Count < targetCustomerCount)
            {
                var toCreate = targetCustomerCount - customers.Count;
                for (int i = 0; i < toCreate; i++)
                {
                    var first = FirstNames[rng.Next(FirstNames.Length)];
                    var last = LastNames[rng.Next(LastNames.Length)];
                    var customer = new Customer
                    {
                        CompanyId = companyId,
                        FullName = $"{first} {last}",
                        ContactNumber = $"09{rng.Next(100000000, 999999999)}",
                        Email = rng.Next(0, 3) == 0 ? null : $"{first.ToLower()}.{last.ToLower()}{rng.Next(1, 999)}@example.com",
                        Segment = CustomerSegment.New
                    };
                    db.Customers.Add(customer);
                }
                db.SaveChanges();
                customers = db.Customers.Where(c => !c.IsDeleted && c.CompanyId == companyId).ToList();
            }

            var addOnItems = db.InventoryItems.Where(i => i.IsActive && i.SellingPrice > 0 && i.CompanyId == companyId).ToList();

            // Include one low-stock item so the dashboard's stock KPI has
            // a realistic example in a freshly seeded demo tenant.
            var lowStockItem = db.InventoryItems.FirstOrDefault(i => i.CompanyId == companyId && i.IsActive);
            if (lowStockItem != null)
                lowStockItem.QuantityOnHand = Math.Max(0m, lowStockItem.ReorderThreshold - 1m);

            for (int i = 0; i < orderCount; i++)
            {
                var customer = customers[rng.Next(customers.Count)];

                // Spread orders across the last 90 days for meaningful daily/weekly/monthly reports.
                var daysAgo = rng.Next(0, 90);
                var receivedAt = DateTime.UtcNow.AddDays(-daysAgo).AddHours(-rng.Next(0, 10));

                var order = new LaundryOrder
                {
                    CompanyId = companyId,
                    CustomerId = customer.Id,
                    Status = OrderStatus.Pending,
                    ReceivedAt = receivedAt
                };

                // 1-3 line items per order.
                var itemCount = rng.Next(1, 4);
                for (int j = 0; j < itemCount; j++)
                {
                    var service = services[rng.Next(services.Count)];
                    var qty = Math.Round((decimal)(rng.NextDouble() * 7 + 1), 1); // 1.0 - 8.0
                    order.Items.Add(new OrderItem
                    {
                        CompanyId = companyId,
                        ServiceName = service.Name,
                        Quantity = qty,
                        UnitPrice = service.UnitPrice
                    });
                }

                // Occasionally add a detergent/fabric-conditioner style add-on.
                if (addOnItems.Count > 0 && rng.Next(0, 2) == 0)
                {
                    var addOn = addOnItems[rng.Next(addOnItems.Count)];
                    order.AddOns.Add(new OrderAddOn
                    {
                        CompanyId = companyId,
                        InventoryItemId = addOn.Id,
                        Quantity = 1m,
                        UnitPrice = addOn.SellingPrice
                    });
                }

                order.RecalculateTotal();

                // Status distribution: mostly Completed (historical), a few
                // still in progress, a small handful cancelled.
                var roll = rng.Next(0, 100);
                if (roll < 75)
                {
                    order.Status = OrderStatus.Completed;
                    order.ReadyAt = receivedAt.AddHours(rng.Next(4, 24));
                    order.CompletedAt = order.ReadyAt.Value.AddHours(rng.Next(1, 48));
                }
                else if (roll < 90)
                {
                    order.Status = (OrderStatus)rng.Next(0, 3); // Pending, Ongoing, or ReadyForPickup
                    if (order.Status == OrderStatus.ReadyForPickup)
                        order.ReadyAt = receivedAt.AddHours(rng.Next(4, 24));
                }
                else
                {
                    order.Status = OrderStatus.Cancelled;
                }

                db.LaundryOrders.Add(order);

                // Update customer rollups.
                customer.TotalOrders += 1;
                if (!customer.LastOrderDate.HasValue || receivedAt > customer.LastOrderDate.Value)
                    customer.LastOrderDate = receivedAt;

                // Record a payment for completed orders (mostly paid in full,
                // occasionally partial), so sales reports have real numbers.
                if (order.Status == OrderStatus.Completed)
                {
                    var isPartial = rng.Next(0, 10) == 0; // ~10% partially paid
                    var amountPaid = isPartial ? Math.Round(order.TotalAmount * 0.5m, 2) : order.TotalAmount;

                    db.Payments.Add(new Payment
                    {
                        CompanyId = companyId,
                        LaundryOrder = order,
                        AmountDue = order.TotalAmount,
                        AmountPaid = amountPaid,
                        Method = (PaymentMethod)rng.Next(0, Enum.GetValues<PaymentMethod>().Length),
                        Status = isPartial ? PaymentStatus.PartiallyPaid : PaymentStatus.Paid,
                        PaidAt = order.CompletedAt,
                        ReceiptNumber = $"RCPT-DEMO-{i:D5}"
                    });

                    customer.TotalSpend += amountPaid;
                }

                // Save in batches to avoid one enormous change tracker.
                if (i % 25 == 0) db.SaveChanges();
            }

            // Sample ratings feed the dashboard's average-rating KPI.
            var sampleFeedback = customers.Take(Math.Min(customers.Count, 20)).Select((customer, index) => new Feedback
            {
                CompanyId = companyId,
                CustomerId = customer.Id,
                Type = index % 5 == 0 ? FeedbackType.Complaint : FeedbackType.Feedback,
                Status = index % 5 == 0 ? FeedbackStatus.Resolved : FeedbackStatus.Closed,
                Message = index % 5 == 0 ? "Demo feedback: service issue resolved." : "Demo feedback: service was good.",
                Rating = index % 5 == 0 ? 3 : rng.Next(4, 6)
            }).ToList();
            db.Feedbacks.AddRange(sampleFeedback);

            db.SaveChanges();
        }

        /// <summary>Populate a fresh tenant once, without duplicating existing orders.</summary>
        public static bool SeedIfNoOrders(LaundryShopDbContext db, int companyId, int orderCount = 120)
        {
            if (db.LaundryOrders.Any(o => o.CompanyId == companyId)) return false;
            Seed(db, companyId, orderCount);
            return true;
        }
    }
}
