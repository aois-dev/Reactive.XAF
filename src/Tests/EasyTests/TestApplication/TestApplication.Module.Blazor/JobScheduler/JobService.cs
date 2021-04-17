﻿using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Model.Core;
using Xpand.Extensions.Reactive.Transform;
using Xpand.Extensions.XAF.NonPersistentObjects;
using Xpand.TestsLib.Common.BO;
using Xpand.XAF.Modules.JobScheduler.Hangfire;
using Xpand.XAF.Modules.JobScheduler.Hangfire.BusinessObjects;
using Xpand.XAF.Modules.Reactive.Services;

namespace TestApplication.Module.Blazor.JobScheduler {
    class JoSchedulerSourceUpdater:ModelNodesGeneratorUpdater<ModelJobSchedulerSourceModelGenerator> {
        public override void UpdateNode(ModelNode node) {
            var source = node.AddNode<IModelJobSchedulerSource>();
            source.AssemblyName = typeof(JobService).Assembly.GetName().Name;
        }
    }
    public static class JobService {
        public static IObservable<Unit> ConnectJobScheduler(this ApplicationModulesManager manager) {
            return manager.ConfigureNewJob();
        }

        private static IObservable<Unit> ConfigureNewJob(this ApplicationModulesManager manager) 
            => manager.WhenApplication(application => application.WhenDetailViewCreated(typeof(Xpand.XAF.Modules.JobScheduler.Hangfire.BusinessObjects.Job)))
                .Do(t => {
                    var job = (Xpand.XAF.Modules.JobScheduler.Hangfire.BusinessObjects.Job)t.e.View.CurrentObject;
                    if (job.IsNewObject&&job.JobType==null) {
                        job.JobType = job.JobTypes.First();
                        job.JobMethod = job.JobMethods.First();
                    }
                    // else if (job is ExecuteActionJob actionJob) {
                    //     actionJob.Action = actionJob.Actions.First(s => s.Name=="Delete");
                    //     actionJob.View = actionJob.Views.First(s => s.Name=="Product_ListView");
                    //     actionJob.Id = Guid.NewGuid().ToString();
                    //     actionJob.SelectedObjectsCriteria = $"{nameof(Product.ProductName)}='del'";
                    // }
                })
                .ToUnit();

    }
}