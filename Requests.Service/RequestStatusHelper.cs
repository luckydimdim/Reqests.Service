using Cmas.BusinessLayers.Requests.Entities;

namespace Cmas.Services.Requests
{
    public static class RequestStatusHelper
    {
        /// <summary>
        /// Получить название статуса.
        /// </summary>
        public static string GetName(this RequestStatus status)
        {
            switch (status)
            {
                case RequestStatus.Empty:
                    return "Не заполнена";
                case RequestStatus.Creating:
                    return "Заполнение";
                case RequestStatus.Created:
                    return "Заполнена";
                case RequestStatus.Approving:
                    return "На проверке";
                case RequestStatus.Correcting:
                    return "Содержит ошибки";
                case RequestStatus.Corrected:
                    return "Исправлена";
                case RequestStatus.Approved:
                    return "Проверена";
                default:
                    return "";
            }
        }
    }
}
