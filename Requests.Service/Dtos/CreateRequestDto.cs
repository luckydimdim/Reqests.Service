using System.Collections.Generic;

namespace Cmas.Services.Requests.Dtos
{
    public class CreateRequestDto
    {
        public string ContractId;

        public IList<string> CallOffsOrdersIds;

        public CreateRequestDto()
        {
            CallOffsOrdersIds = new List<string>();
        }
    }
}
