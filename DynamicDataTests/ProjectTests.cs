using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using Xunit;

namespace DynamicDataTests
{
	public class Project
	{
		internal SourceCache<Domain, long> Domains { get; } = new SourceCache<Domain, long>(_ => _.Id);

        //DD is already an observable so subjec should always be a last resort
		internal Subject<Unit> ParentUpdates { get; } = new Subject<Unit>();

		internal IObservable<IGroupChangeSet<Domain, long, Domain>> GroupedDomains { get; }

		public Project()
		{
			GroupedDomains = Domains.Connect().Group(_ => _.Parent);
		}
	}

	[System.Diagnostics.DebuggerDisplay("{Id}")]
	public class Domain : AbstractNotifyPropertyChanged
	{
		public long Id { get; }

		public Domain Parent
		{
			get => this.parent;

			set
			{
				SetAndRaise(ref this.parent, value);
				Project.ParentUpdates.OnNext(Unit.Default);
			}
		}
		private Domain parent;


        //What's disposing of ths?
		public IObservableCache<Domain, long> Children { get; }

		internal Project Project { get; }

		public Domain(Project project, long id, Func<Domain, IObservableCache<Domain, long>> childrenFactory, Domain parent = null)
		{
			Project = project;
			Id = id;
			this.parent = parent;

			Children = childrenFactory(this);
		}
	}



	public class ProjectTests
	{
		[Fact]
		public void CreateDomains_WithoutAutoRefresh()
		{
			CreateDomains(domain =>
				domain.Project.Domains.Connect()
					.Filter(_ => _.Parent != null && _.Parent == domain).AsObservableCache()
			);
		}

		[Fact]
		public void CreateDomains_WithProperty()
		{
			CreateDomains(domain =>
				domain.Project.Domains.Connect().AutoRefresh(_ => _.Parent)
					.Filter(_ => _.Parent != null && _.Parent == domain).AsObservableCache()
			);
		}

		[Fact]
		public void CreateDomains_WithObservable()
		{
			CreateDomains(domain =>
				domain.Project.Domains.Connect().AutoRefreshOnObservable(_ => domain.Project.ParentUpdates)
					.Filter(_ => _.Parent != null && _.Parent == domain).AsObservableCache()
			);
		}

		[Fact]
		public void CreateDomains_WithReapplyFilter_WithBug()
		{
			CreateDomains(domain =>
				domain.Project.Domains.Connect()
					.Filter(_ => _.Parent != null && _.Parent == domain)
					.Filter(domain.Project.ParentUpdates) // Exception here: does not seem normal: this method calls new DynamicFilter with predicateChanged = null and this constructor throws if predicateChanged is null
					.AsObservableCache()
			);
		}

		[Fact]
		public void CreateDomains_WithReapplyFilter()
		{
			CreateDomains(domain =>
				domain.Project.Domains.Connect()
					.Filter(Observable.Return<Func<Domain, bool>>(_ => _.Parent != null && _.Parent == domain), domain.Project.ParentUpdates)
					.AsObservableCache()
			);
		}




		private void CreateDomains(Func<Domain, IObservableCache<Domain, long>> childrenFactory)
		{
			// add domains to the flat cache
			const int nbChildren = 200;
			var project = new Project();

			var domains = new List<Domain>();

			for (int i = 0; i < 10; i++)
			{
				var parent = new Domain(project, i, childrenFactory);
				domains.Add(parent);

				for (int j = 0; j < nbChildren; j++)
				{
					domains.Add(new Domain(project, (i + 1) * nbChildren + j, childrenFactory, parent));
				}
			}

			project.Domains.AddOrUpdate(domains);

			// check a parent has the proper number of children and they all have it as parent
			var parent1 = project.Domains.Lookup(0).Value;
			Assert.Equal(nbChildren, parent1.Children.Count);
			Assert.True(parent1.Children.Items.All(_ => _.Parent == parent1));

			// move a domain from parent1 to parent2
			var domain = parent1.Children.Items.First();
			var parent2 = project.Domains.Lookup(1).Value;
			domain.Parent = parent2;

			// check the children of parent1 and parent2 are updated
			Assert.Equal(nbChildren - 1, parent1.Children.Count);
			Assert.DoesNotContain(domain, parent1.Children.Items);

			Assert.Equal(nbChildren + 1, parent2.Children.Count);
			Assert.Contains(domain, parent2.Children.Items);
		}
	}
}
