using AutoMapper;
using TBD.API.DTOs;
using TBD.ServiceModule.Models;

namespace TBD.Shared.Utils;

public class ServiceMapping : Profile
{
    public ServiceMapping()
    {
        CreateMap<Service, ServiceDto>();
        CreateMap<ServiceDto, Service>().ReverseMap();
    }
}
