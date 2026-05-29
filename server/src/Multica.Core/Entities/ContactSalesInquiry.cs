using System.Net;

namespace Multica.Core.Entities;

public class ContactSalesInquiry
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string BusinessEmail { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string CompanySize { get; set; } = "";
    public string CountryRegion { get; set; } = "";
    public string UseCase { get; set; } = "";
    public string Goals { get; set; } = "";
    public bool ConsentOutreach { get; set; }
    public bool ConsentUpdates { get; set; }
    public IPAddress? SubmitterIp { get; set; }
    public string UserAgent { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
