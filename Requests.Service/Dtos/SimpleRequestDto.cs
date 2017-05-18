using System;


namespace Cmas.Services.Requests.Dtos
{
    /// <summary>
    /// Простая модель заявки. Используется в списке заявок
    /// </summary>
    public class SimpleRequestDto
    {
        /// <summary>
        /// Уникальный внутренний идентификатор
        /// </summary>
        public string Id;

        /// <summary>
        /// Номер ревизии
        /// </summary>
        public string RevId;

        /// <summary>
        /// Идентификатор договора
        /// </summary>
        public string ContractId;

        /// <summary>
        /// Дата и время создания
        /// </summary>
        public DateTime CreatedAt;

        /// <summary>
        /// Дата и время обновления
        /// </summary>
        public DateTime UpdatedAt;

        /// <summary>
        /// Номер договора
        /// </summary>
        public string ContractNumber;

        /// <summary>
        /// Наименование подрядчика
        /// </summary>
        public string ContractorName;

        /// <summary>
        /// Название валюты договора
        /// </summary>
        public string CurrencyName = "руб.";

        /// <summary>
        /// Системное имя валюты
        /// </summary>
        public string CurrencySysName = "RUR";

        /// <summary>
        /// Сумма к оплате
        /// </summary>
        public double Amount;

        /// <summary>
        /// Имя статуса для показа
        /// </summary>
        public string StatusName;

        /// <summary>
        /// Системное имя статуса
        /// </summary>
        public string StatusSysName;
    }
}
