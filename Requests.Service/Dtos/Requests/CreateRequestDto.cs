using System.Collections.Generic;

namespace Cmas.Services.Requests.Dtos
{
    public class CreateRequestDto
    {
        public string ContractId;

        public IList<string> CallOffOrderIds;

        public CreateRequestDto()
        {
            CallOffOrderIds = new List<string>();
        }
    }
}
