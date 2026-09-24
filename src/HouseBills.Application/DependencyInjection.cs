using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Overview;
using HouseBills.Application.Payees;
using HouseBills.Application.RecurringBills;
using HouseBills.Application.Reminders;

using Microsoft.Extensions.DependencyInjection;

namespace HouseBills.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application services. The host must bind <see cref="BillingOptions"/> to configuration
    /// and register implementations of the persistence interfaces and <see cref="Common.IClock"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<IPayeeService, PayeeService>();
        services.AddSingleton<IBillService, BillService>();
        services.AddSingleton<IRecurringBillService, RecurringBillService>();
        services.AddSingleton<IOverviewService, OverviewService>();
        services.AddSingleton<IReminderService, ReminderService>();

        services.AddOptions<BillingOptions>()
            .Validate(
                o => o.GenerationLookaheadDays is >= 0 and <= BillingOptions.MaxLookaheadDays,
                $"{BillingOptions.SectionName}:GenerationLookaheadDays must be between 0 and {BillingOptions.MaxLookaheadDays}.")
            .ValidateOnStart();

        return services;
    }
}