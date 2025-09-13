using AutoMapper;
using DotnetMVCApp.ViewModels.Candidate;

public class MappingProfile : Profile
{
    public MappingProfile()
    {

        CreateMap<Job, JobSearchViewModel>()
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.JobDescription));

        CreateMap<JobSearchViewModel, Job>()
            .ForMember(dest => dest.JobDescription, opt => opt.MapFrom(src => src.Description));
    }
}
