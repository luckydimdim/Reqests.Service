using System;
using System.Collections.Generic;


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
        /// Счетчик, аналог ID. 
        /// </summary>
        public string Counter;

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
        /// Сумма к оплате
        /// </summary>
        public List<AmountDto> Amounts = new List<AmountDto>();

        /// <summary>
        /// Имя статуса для показа
        /// </summary>
        public string StatusName;

        /// <summary>
        /// Системное имя статуса
        /// </summary>
        public string StatusSysName;

        /// <summary>
        /// Возможность удаления
        /// </summary>
        public bool CanDelete;
    }
}
