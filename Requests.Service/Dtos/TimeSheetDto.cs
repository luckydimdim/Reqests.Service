using System;

namespace Cmas.Services.Requests.Dtos
{
    public class TimeSheetDto : DocumentDto
    {
        public override string Type {
            get
            {
                return "timesheet";
            }
          }

        /// <summary>
        /// Уникальный идентификатор
        /// </summary>
        public string Id;

        /// <summary>
        /// ФИО
        /// </summary>
        public string Assignee;

        /// <summary>
        /// Дата и время создания
        /// </summary>
        public DateTime CreatedAt;

        /// <summary>
        /// Дата и время обновления
        /// </summary>
        public DateTime UpdatedAt;

        /// <summary>
        /// Наименование работ
        /// </summary>
        public string Name;

        /// <summary>
        /// Должность
        /// </summary>
        public string Position;

        /// <summary>
        /// Имя статуса для показа
        /// </summary>
        public string StatusName;

        /// <summary>
        /// Системное имя статуса
        /// </summary>
        public string StatusSysName;

        /// <summary>
        /// Валюта
        /// </summary>
        public string CurrencyName = "руб";

        /// <summary>
        /// Валюта
        /// </summary>
        public string CurrencySysName = "RUR";
    }
}
