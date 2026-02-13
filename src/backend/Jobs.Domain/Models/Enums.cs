namespace Jobs.Domain.Models;

public enum WorkMode { Unknown = 0, Remote = 1, Hybrid = 2, Onsite = 3 }
public enum Seniority { Unknown = 0, Intern = 1, Junior = 2, Mid = 3, Senior = 4, Staff = 5, Lead = 6, Principal = 7 }
public enum EmploymentType { Unknown = 0, CLT = 1, PJ = 2, Contractor = 3, Internship = 4, Temporary = 5 }
public enum JobStatus { Unknown = 0, Active = 1, Expired = 2 }
public enum SourceType
{
    Unknown = 0,
    LinkedIn = 1,
    Greenhouse = 2,
    Lever = 3,
    Indeed = 4,
    CareersPage = 5,
    JsonLd = 8,
    CorporateCareers = 9,
    Gupy = 10,
    Workday = 11,
    InfoJobs = 6,
    Vagas = 7,
    Fixture = 100
}
