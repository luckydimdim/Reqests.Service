using System;
using System.Collections.Generic;

namespace Cmas.Services.Requests.Dtos
{
    public class SummaryDto
    {
        /// <summary>
        /// Всего работ
        /// </summary>
        public int WorksQuantity;

        /// <summary>
        /// Сумма работ
        /// </summary>
        public List<AmountDto> WorksAmount = new List<AmountDto>();

        /// <summary>
        /// Всего материалов
        /// </summary>
        public int MaterialsQuantity;

        /// <summary>
        /// Сумма материалов
        /// </summary>
        public List<AmountDto> MaterialsAmount = new List<AmountDto>();

        /// <summary>
        /// Общая стоимость
        /// </summary>
        public List<AmountDto> Amounts = new List<AmountDto>();

        /// <summary>
        /// В том числе НДС
        /// </summary>
        public List<AmountDto> Vats = new List<AmountDto>();

        /// <summary>
        /// Итого к оплате
        /// </summary>
        public List<AmountDto> Totals = new List<AmountDto>();
    }

    public class DetailedRequestDto
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
        /// Идентификаторы наряд заказов
        /// </summary>
        public IEnumerable<string> CallOffOrderIds;

        /// <summary>
        /// Документация
        /// </summary>
        public IEnumerable<DocumentDto> Documents;

        /// <summary>
        /// Сводка
        /// </summary>
        public SummaryDto Summary;

        /// <summary>
        /// Имя статуса для показа
        /// </summary>
        public string StatusName;

        /// <summary>
        /// Системное имя статуса
        /// </summary>
        public string StatusSysName;

        public DetailedRequestDto()
        {
            Documents = new List<DocumentDto>();
            Summary = new SummaryDto();
        }
    }
}
