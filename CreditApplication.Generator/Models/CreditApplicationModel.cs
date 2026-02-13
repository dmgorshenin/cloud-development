namespace CreditApplication.Generator.Models;

/// <summary>
/// Модель кредитной заявки
/// </summary>
public class CreditApplicationModel
{
    /// <summary>
    /// Идентификатор в системе
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Тип кредита (Потребительский, Ипотека, Автокредит и т.д.)
    /// </summary>
    public string CreditType { get; set; } = string.Empty;

    /// <summary>
    /// Запрашиваемая сумма (округляется до 2 знаков)
    /// </summary>
    public decimal RequestedAmount { get; set; }

    /// <summary>
    /// Срок в месяцах
    /// </summary>
    public int TermInMonths { get; set; }

    /// <summary>
    /// Процентная ставка (не менее ставки ЦБ РФ, округляется до 2 знаков)
    /// </summary>
    public double InterestRate { get; set; }

    /// <summary>
    /// Дата подачи (не более 2 лет назад)
    /// </summary>
    public DateOnly ApplicationDate { get; set; }

    /// <summary>
    /// Необходимость страховки
    /// </summary>
    public bool InsuranceRequired { get; set; }

    /// <summary>
    /// Статус заявки (Новая, В обработке, Одобрена, Отклонена)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Дата решения (только для терминальных статусов)
    /// </summary>
    public DateOnly? DecisionDate { get; set; }

    /// <summary>
    /// Одобренная сумма (только для статуса "Одобрена", <= RequestedAmount)
    /// </summary>
    public decimal? ApprovedAmount { get; set; }
}
