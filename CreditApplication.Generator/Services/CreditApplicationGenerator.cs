using Bogus;
using CreditApplication.Generator.Models;

namespace CreditApplication.Generator.Services;

/// <summary>
/// Генератор кредитных заявок на основе Bogus
/// </summary>
public class CreditApplicationGenerator(ILogger<CreditApplicationGenerator> logger)
{
    private const double MinInterestRate = 16.0;
    
    private static readonly string[] _creditTypes = 
    [
        "Потребительский",
        "Ипотека",
        "Автокредит",
        "Кредит на образование",
        "Кредит на рефинансирование"
    ];

    private static readonly string[] _statuses = 
    [
        "Новая",
        "В обработке",
        "Одобрена",
        "Отклонена"
    ];

    private static readonly string[] _terminalStatuses = ["Одобрена", "Отклонена"];

    private readonly ILogger<CreditApplicationGenerator> _logger = logger;

    /// <summary>
    /// Генерирует кредитную заявку по указанному идентификатору
    /// </summary>
    /// <param name="id">Идентификатор заявки (используется как seed для повторяемости)</param>
    /// <returns>Сгенерированная кредитная заявка</returns>
    public CreditApplicationModel Generate(int id)
    {
        _logger.LogInformation("Generating credit application with ID: {Id}", id);

        var faker = new Faker<CreditApplicationModel>("ru")
            .UseSeed(id)
            .RuleFor(x => x.Id, _ => id)
            .RuleFor(x => x.CreditType, f => f.PickRandom(_creditTypes))
            .RuleFor(x => x.RequestedAmount, f => Math.Round(f.Random.Decimal(50_000m, 10_000_000m), 2))
            .RuleFor(x => x.TermInMonths, f => f.Random.Int(6, 360))
            .RuleFor(x => x.InterestRate, f => Math.Round(f.Random.Double(MinInterestRate, 35.0), 2))
            .RuleFor(x => x.ApplicationDate, f => GenerateApplicationDate(f))
            .RuleFor(x => x.InsuranceRequired, f => f.Random.Bool())
            .RuleFor(x => x.Status, f => f.PickRandom(_statuses))
            .FinishWith((f, app) =>
            {
                SetStatusDependentFields(f, app);
            });

        var application = faker.Generate();
        
        _logger.LogInformation(
            "Credit application generated: ID={Id}, Type={CreditType}, Status={Status}, Amount={Amount}",
            application.Id,
            application.CreditType,
            application.Status,
            application.RequestedAmount);

        return application;
    }

    private static DateOnly GenerateApplicationDate(Faker faker)
    {
        var minDate = DateTime.Today.AddYears(-2);
        var maxDate = DateTime.Today;
        var randomDate = faker.Date.Between(minDate, maxDate);
        return DateOnly.FromDateTime(randomDate);
    }

    private static void SetStatusDependentFields(Faker faker, CreditApplicationModel app)
    {
        var isTerminal = _terminalStatuses.Contains(app.Status);
        
        if (isTerminal)
        {
            var applicationDateTime = app.ApplicationDate.ToDateTime(TimeOnly.MinValue);
            var decisionDateTime = faker.Date.Between(applicationDateTime, DateTime.Today);
            app.DecisionDate = DateOnly.FromDateTime(decisionDateTime);

            if (app.Status == "Одобрена")
            {
                var maxApproved = app.RequestedAmount;
                var minApproved = maxApproved * 0.5m;
                app.ApprovedAmount = Math.Round(faker.Random.Decimal(minApproved, maxApproved), 2);
            }
        }
    }
}
