<Query Kind="Program">
  <NuGetReference>Humanizer.Core</NuGetReference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>NodaTime</NuGetReference>
  <NuGetReference>Octokit.Reactive</NuGetReference>
  <NuGetReference>System.Interactive</NuGetReference>
  <NuGetReference>System.Interactive.Async</NuGetReference>
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Serialization</Namespace>
  <Namespace>NodaTime</Namespace>
  <Namespace>NodaTime.Extensions</Namespace>
  <Namespace>Octokit</Namespace>
  <Namespace>Octokit.Reactive</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Reactive.Threading.Tasks</Namespace>
  <Namespace>NodaTime.Calendars</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Net.Http</Namespace>
</Query>

async Task Main()
{
	var mvpApiKey = "TODO";
	var githubKey = "TODO";
	var store = new Octokit.Internal.InMemoryCredentialStore(new Credentials(githubKey));

	var header = new ProductHeaderValue("personalmvpapiintegration", "2.0");
	var githubApi = new ObservableGitHubClient(header, store);

	var rsgRepos = githubApi.Repository.GetAllForOrg("RocketSurgeonsGuild");
	var omnisharpRepos = githubApi.Repository.GetAllForOrg("OmniSharp");

	var repos = rsgRepos.Merge(omnisharpRepos).Concat(githubApi.Repository.GetAllForOrg("reactivex"));

	var commitsForMe = repos.SelectMany(repo =>
		githubApi.Repository.Commit.GetAll(repo.Owner.Login, repo.Name, new CommitRequest() { Since = new DateTimeOffset(2020, 3, 8, 00, 00, 00, TimeSpan.Zero), Author = "david-driscoll" })
			.OnErrorResumeNext(Observable.Empty<GitHubCommit>()),
			(repository, commit) =>
			{
				return (repository, commit);
			}
		);
	var httpClient = new HttpClient() { };


	var c = commitsForMe
		.Select(data => (date: LocalDateTime.FromDateTime(data.commit.Commit.Author.Date.LocalDateTime).Date, data.repository, data.commit))
		//.Select(data => (data.date, data.repository, data.commit, month: data.date.Month + ":" + data.date.Year))
		.Select(data => (data.date, data.repository, data.commit, weekYear: WeekYearRules.Iso.GetWeekOfWeekYear(data.date).ToString() + ":" + data.date.Year))
		.ToArray()
		.SelectMany(z => z
			//.GroupBy(z => z.month)
			.GroupBy(z => z.weekYear)
			.SelectMany(group =>
			{
				var date = group.MinBy(z => z.date).First().date;
				return group.Select(z => (date, z.repository, z.commit));
			})
			.OrderBy(z => z.date)
		);

	await c
	.GroupBy(z =>
	{
		if (z.repository.Owner.Login.Equals("RocketSurgeonsGuild", StringComparison.OrdinalIgnoreCase))
		{
			return (z.date, name: z.repository.Owner.Login, url: z.repository.Owner.HtmlUrl);
		}
		else
		{
			return (z.date, name: z.repository.FullName, url: z.repository.HtmlUrl);
		}
	}
	)
	.Select(group =>
	{
		return group.Count().Select(count => (date: group.Key.date, name: group.Key.name, group.Key.url, count));
	})
	.Merge()
	.Select(data => new ContributionsModel()
	{
		ContributionId = 0,
		ContributionType = SampleCode,
		ContributionTechnology = DotNet,
		Visibility = MVPVisibility,
		ContributionTypeName = SampleCode.Name,
		StartDate = data.date.ToDateTimeUnspecified(),
		Title = $"Contributions to {data.name}",
		Description = $"Github contributions to {data.name}",
		AnnualQuantity = data.count,
		AnnualReach = data.count,
		ReferenceUrl = data.url,
		AdditionalTechnologies = new List<UserQuery.ContributionTechnologyModel>(),
	})
	.Select(data =>
	{
		var request = new HttpRequestMessage(HttpMethod.Post, "https://mvpapi.azure-api.net/mvp/api/contributions")
		{
			Content = new StringContent(JsonConvert.SerializeObject(data), null, "application/json"),
		};
		request.Headers.Add("Ocp-Apim-Subscription-Key", "TODO");
		request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "TODO");
		return request;
	})
	.Select(request => Observable.FromAsync(ct => httpClient.SendAsync(request, ct)))
	.Concat()
	.ForEachAsync(async z =>
	{
		z.Dump();
		await z.Content.ReadAsStringAsync().Dump();
	});

}


public ContributionTypeModel SampleCode = new ContributionTypeModel()
{
	Id = new Guid("e96464de-179a-e411-bbc8-6c3be5a82b68"),
	Name = "Sample Code/Projects/Tools",
	EnglishName = "Sample Code/Projects/Tools"

};

public ContributionTechnologyModel DotNet = new ContributionTechnologyModel()
{
	Id = new Guid("aec301bb-189a-e411-93f2-9cb65495d3c4"),
	AwardName = "Developer Technologies",
	AwardCategory = "My Awarded Category",
	Name = ".NET",
};

public ContributionTechnologyModel AspNet = new ContributionTechnologyModel()
{
	Id = new Guid("82c301bb-189a-e411-93f2-9cb65495d3c4"),
	AwardName = "Developer Technologies",
	AwardCategory = "My Awarded Category",
	Name = "ASP.NET/IIS",
};

public ContributionTechnologyModel AzureDevOps = new ContributionTechnologyModel()
{
	Id = new Guid("84c301bb-189a-e411-93f2-9cb65495d3c4"),
	AwardName = "Developer Technologies",
	AwardCategory = "My Awarded Category",
	Name = "Azure DevOps",
};

public VisibilityViewModel MicrosoftVisibility = new VisibilityViewModel()
{
	Id = 100000000,
	Description = "Microsoft",
	LocalizeKey = "MicrosoftVisibilityText"
};
public VisibilityViewModel MVPVisibility = new VisibilityViewModel()
{

	Id = 100000001,
	Description = "MVP Community",
	LocalizeKey = "MVPVisibilityText"
};
public VisibilityViewModel PublicVisibility = new VisibilityViewModel()
{
	Id = 299600000,
	Description = "Everyone",
	LocalizeKey = "PublicVisibilityText"
};

public partial class ActivityTypeViewModel
{
	public Guid? Id { get; set; }
	public string Name { get; set; }
	public string EnglishName { get; set; }
}

public partial class ActivityViewModel
{
	public int? PrivateSiteId { get; set; }
	public ActivityTypeViewModel ActivityType { get; set; }
	public ActivityTechnologyViewModel ApplicableTechnology { get; set; }
	public DateTime? DateOfActivity { get; set; }
	public string DateOfActivityFormatted { get; set; }
	public DateTime? EndDate { get; set; }
	public string EndDateFormatted { get; set; }
	public string TitleOfActivity { get; set; }
	public string ReferenceUrl { get; set; }
	public VisibilityViewModel ActivityVisibility { get; set; }
	public int? AnnualQuantity { get; set; }
	public int? SecondAnnualQuantity { get; set; }
	public int? AnnualReach { get; set; }
	public string Description { get; set; }
	public OnlineIdentityViewModel OnlineIdentity { get; set; }
	public SocialNetworkViewModel SocialNetwork { get; set; }
	public string AllAnswersUrl { get; set; }
	public string AllPostsUrl { get; set; }
	public bool? IsSystemCollected { get; set; }
	public bool? IsBelongToLatestAwardCycle { get; set; }
	public string DisplayMode { get; set; }
	public List<int?> ChartColumnIndexes { get; set; }
	public string DescriptionSummaryFormat { get; set; }
	public string DataTableTitle { get; set; }
	public string SubtitleHeader { get; set; }
	public bool? IsAllowEdit { get; set; }
	public bool? IsAllowDelete { get; set; }
	public bool? IsFromBookmarklet { get; set; }
	public bool? Submitted { get; set; }
}

public partial class AwardAnswerViewModel
{
	public Guid? AwardQuestionId { get; set; }
	public string Answer { get; set; }
}

public partial class AwardQuestionViewModel
{
	public Guid? AwardQuestionId { get; set; }
	public string QuestionContent { get; set; }
	public bool? Required { get; set; }

}


public partial class AwardRecognitionViewModel
{
	public int? PrivateSiteId { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
	public DateTime? DateEarned { get; set; }
	public string ReferenceUrl { get; set; }
	public VisibilityViewModel AwardRecognitionVisibility { get; set; }
}
public partial class CertificationViewModel
{
	public int? PrivateSiteId { get; set; }
	public Guid? Id { get; set; }
	public string Title { get; set; }
	public VisibilityViewModel CertificationVisibility { get; set; }
}
public partial class ContentMetadata
{
	public string PageTitle { get; set; }
	public string TemplateName { get; set; }
	public string Keywords { get; set; }
	public string Description { get; set; }
}
public partial class ContributionsModel
{
	public int? ContributionId { get; set; }
	public string ContributionTypeName { get; set; }
	public ContributionTypeModel ContributionType { get; set; }
	public ContributionTechnologyModel ContributionTechnology { get; set; }
	public List<ContributionTechnologyModel> AdditionalTechnologies { get; set; }
	public DateTime? StartDate { get; set; }
	public string Title { get; set; }
	public string ReferenceUrl { get; set; }
	public VisibilityViewModel Visibility { get; set; }
	public int? AnnualQuantity { get; set; }
	public int? SecondAnnualQuantity { get; set; }
	public int? AnnualReach { get; set; }
	public string Description { get; set; }
}
public partial class ContributionTechnologyModel
{
	public Guid? Id { get; set; }
	public string Name { get; set; }
	public string AwardName { get; set; }
	public string AwardCategory { get; set; }
}
public partial class ContributionTypeModel
{
	public Guid? Id { get; set; }
	public string Name { get; set; }
	public string EnglishName { get; set; }
}
public partial class ContributionViewModel
{
	public List<ContributionsModel> Contributions { get; set; }
	public int? TotalContributions { get; set; }
	public int? PagingIndex { get; set; }
}
public partial class MvpHighlightViewModel
{
	public string Title { get; set; }
	public DateTime? Date { get; set; }
	public string DateFormatted { get; set; }
	public string Url { get; set; }
	public string Type { get; set; }
	public string Language { get; set; }
}
public partial class OnlineIdentity
{
	public Guid? OnlineIdentityId { get; set; }
	public Guid? MvpGuid { get; set; }
	public string Name { get; set; }
	public int? PrivateSiteId { get; set; }
	public SharingPreference OnlineIdentityVisibility { get; set; }
	public SocialNetwork SocialNetwork { get; set; }
	public string Url { get; set; }
	public string DisplayName { get; set; }
	public string UserId { get; set; }
	public string MicrosoftAccount { get; set; }
	public bool? ContributionCollected { get; set; }
	public bool? PrivacyConsentStatus { get; set; }
	public bool? Submitted { get; set; }
}
public partial class OnlineIdentityViewModel
{
	public int? PrivateSiteId { get; set; }
	public SocialNetworkViewModel SocialNetwork { get; set; }
	public string Url { get; set; }
	public VisibilityViewModel OnlineIdentityVisibility { get; set; }
	public bool? ContributionCollected { get; set; }
	public string DisplayName { get; set; }
	public string UserId { get; set; }
	public string MicrosoftAccount { get; set; }
	public bool? PrivacyConsentStatus { get; set; }
	public bool? PrivacyConsentCheckStatus { get; set; }
	public DateTime? PrivacyConsentCheckDate { get; set; }
	public DateTime? PrivacyConsentUnCheckDate { get; set; }
	public bool? Submitted { get; set; }
}
public partial class ProfileViewModel
{
	public ContentMetadata Metadata { get; set; }
	public string MvpId { get; set; }
	public int? YearsAsMvp { get; set; }
	public string FirstAwardYear { get; set; }
	public string AwardCategoryDisplay { get; set; }
	public string TechnicalExpertise { get; set; }
	public bool? InTheSpotlight { get; set; }
	public string Headline { get; set; }
	public string Biography { get; set; }
	public string DisplayName { get; set; }
	public string FullName { get; set; }
	public string PrimaryEmailAddress { get; set; }
	public string ShippingCountry { get; set; }
	public string ShippingStateCity { get; set; }
	public string Languages { get; set; }
	public List<OnlineIdentityViewModel> OnlineIdentities { get; set; }
	public List<CertificationViewModel> Certifications { get; set; }
	public List<ActivityViewModel> Activities { get; set; }
	public List<AwardRecognitionViewModel> CommunityAwards { get; set; }
	public List<MvpHighlightViewModel> NewsHighlights { get; set; }
	public List<MvpHighlightViewModel> UpcomingEvent { get; set; }
}
public partial class SharingPreference
{
	public int? Id { get; set; }
	public string Description { get; set; }
}
public partial class SocialNetwork
{
	public Guid? SocialNetworkId { get; set; }
	public string Name { get; set; }
	public string Website { get; set; }
	public SocialNetworkStatusCode StatusCode { get; set; }
	public bool? SystemCollectionEnabled { get; set; }
}
public partial class SocialNetworkViewModel
{
	public Guid? Id { get; set; }
	public string Name { get; set; }
	public string IconUrl { get; set; }
	public bool? SystemCollectionEnabled { get; set; }
}
public partial class VisibilityViewModel
{
	public int? Id { get; set; }
	public string Description { get; set; }
	public string LocalizeKey { get; set; }
}
public partial class ActivityTechnologyViewModel
{
	public Guid? Id { get; set; }
	public string Name { get; set; }
	public string AwardName { get; set; }
	public string AwardCategory { get; set; }
	public int? Statuscode { get; set; }
	public bool? Active { get; set; }
}
public partial class SocialNetworkStatusCode
{
	public int? Id { get; set; }
	public string Description { get; set; }
}
