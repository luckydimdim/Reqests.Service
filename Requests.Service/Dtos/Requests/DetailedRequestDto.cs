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
        public double WorksAmount;

        /// <summary>
        /// Всего материалов
        /// </summary>
        public int MaterialsQuantity;

        /// <summary>
        /// Сумма материалов
        /// </summary>
        public double MaterialsAmount;

        /// <summary>
        /// Общая стоимость
        /// </summary>
        public double Amount;

        /// <summary>
        /// В том числе НДС
        /// </summary>
        public double Vat;

        /// <summary>
        /// Зачет аванса
        /// </summary>
        public double PrepaidAmount;

        /// <summary>
        /// Удержано резерва
        /// </summary>
        public double ReserveAmount;

        /// <summary>
        /// Итого к оплате
        /// </summary>
        public double Total;
         
        /// <summary>
        /// Валюта
        /// </summary>
        public string CurrencySysName = "RUR";
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
