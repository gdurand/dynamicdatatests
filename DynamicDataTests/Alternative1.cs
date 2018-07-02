using System;
using System.Collections.Generic;
using DynamicData;

namespace DynamicDataTests
{
    public class DomainDto
    {
        public int Id { get; }
        public int ProjectId { get; }
    }

    public class ProjectDto
    {
        public int Id { get; }
    }

    public class ProjectWithDomainCache
    {
        public ProjectDto Left { get; }
        public IObservableCache<DomainDto, int> Cache { get; }

        public ProjectWithDomainCache(ProjectDto left, IObservableCache<DomainDto, int> cache)
        {
            Left = left;
            Cache = cache;
        }
    }

    public class ProjectWithDomainItems
    {
        public ProjectDto Project { get; }

        public ProjectWithDomainItems(ProjectDto projectDto, IEnumerable<DomainDto> domainDtos)
        {
            Project = projectDto;
            //	arg3.
        }
    }


    //Generally try to avoid manual creation of nested caches as it is very difficult to
    //a) get it right
    //b) make it perform well
    public class Alternatives
    {
        public void JoinMany()
        {
            var domainCache = new SourceCache<DomainDto, int>(d => d.Id);
            var projectCache = new SourceCache<ProjectDto, int>(p => p.Id);

            IObservable<IChangeSet<ProjectWithDomainItems, int>> combined = projectCache.Connect()
                .InnerJoinMany(domainCache.Connect(), domain => domain.ProjectId, (key, left, right) => new ProjectWithDomainItems(left, right.Items));
            //From here we would need another tranform and some manual editing of a cache
        }

        public void Grouping()
        {
            var domainCache = new SourceCache<DomainDto, int>(d => d.Id);
            var projectCache = new SourceCache<ProjectDto, int>(p => p.Id);

            //if domain has a project id, it is very efficient to use Group
            var domainWithInnerGroup = domainCache.Connect().Group(d => d.ProjectId);


            IObservable<IChangeSet<ProjectWithDomainCache, int>> combind = projectCache.Connect()
                .InnerJoin(domainWithInnerGroup, domain => domain.Key, (key, left, right) => new ProjectWithDomainCache(left, right.Cache));
        }


    }
}
