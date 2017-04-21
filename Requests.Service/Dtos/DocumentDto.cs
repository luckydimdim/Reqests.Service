namespace Cmas.Services.Requests.Dtos
{
    public class DocumentDto
    {
        public virtual string Type
        {
            get
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Сумма
        /// </summary>
        public double Amount;

    }
}
