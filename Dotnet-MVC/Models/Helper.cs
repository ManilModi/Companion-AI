using AutoMapper;
using DotnetMVCApp.Models;
using DotnetMVCApp.ViewModels.Candidate;

public class MappingProfile : Profile
{
    public MappingProfile()
    {

        CreateMap<Job, JobSearchViewModel>().ReverseMap();


    }
}
