using AutoMapper;
using Cmas.BusinessLayers.Requests.Entities;
using Cmas.BusinessLayers.TimeSheets.Entities;
using Cmas.Services.Requests.Dtos;

namespace Cmas.Services.Requests
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Request, DetailedRequestDto>();
            CreateMap<Request, SimpleRequestDto>();
            CreateMap<TimeSheet, TimeSheetDto>();
        }
    }

}
