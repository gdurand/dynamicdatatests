﻿using System;
using System.Linq;
using DynamicData;
using DynamicData.Binding;
using Xunit;

namespace DynamicDataTests
{
	public class Project
	{
		internal SourceCache<Domain, long> Domains { get; } = new SourceCache<Domain, long>(_ => _.Id);
	}

	[System.Diagnostics.DebuggerDisplay("{Id}")]
	public class Domain : AbstractNotifyPropertyChanged
	{
		public long Id { get; }

		public Domain Parent { get => this.parent; set => SetAndRaise(ref this.parent, value); }
		private Domain parent;

		public IObservableCache<Domain, long> Children { get; }

		private Project Project { get; }

		public Domain(Project project, long id, Domain parent = null)
		{
			Project = project;
			Id = id;
			Parent = parent;

			Children = Project.Domains.Connect().AutoRefresh(_ => _.Parent)
					.Filter(_ => _.Parent != null && _.Parent == this).AsObservableCache();
		}
	}

	public class ProjectTests
	{
		[Fact]
		public void CreateDomainsTest()
		{
			// add domains to the flat cache
			const int nbChildren = 20;
			var project = new Project();

			for (int i = 0; i < 10; i++)
			{
				var parent = new Domain(project, i);
				project.Domains.AddOrUpdate(parent);

				for (int j = 0; j < nbChildren; j++)
				{
					project.Domains.AddOrUpdate(new Domain(project, (i + 1) * nbChildren + j, parent));
				}
			}

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
