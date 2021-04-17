﻿using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using akarnokd.reactive_extensions;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using Xpand.Extensions.Blazor;
using Xpand.Extensions.Reactive.Utility;
using Xpand.Extensions.XAF.FrameExtensions;
using Xpand.Extensions.XAF.XafApplicationExtensions;
using Xpand.TestsLib.Common;
using Xpand.TestsLib.Common.Attributes;
using Xpand.XAF.Modules.JobScheduler.Hangfire.BusinessObjects;
using Xpand.XAF.Modules.Reactive.Services;
using Xpand.XAF.Modules.Reactive.Services.Actions;

namespace Xpand.XAF.Modules.JobScheduler.Hangfire.Tests{
    [NonParallelizable]
    public class ViewActionJobTests:JobSchedulerCommonTest {
        [TestCase(false)]
        [TestCase(true)]
        [XpandTest()]
        public void Customize_Job_Schedule(bool newObject) {
            var application = NewBlazorApplication();
            using (var _ = application.WhenApplicationModulesManager()
                .SelectMany(manager => manager.RegisterViewSimpleAction("test"))
                .Subscribe()) {
                JobSchedulerModule(application);
                
                application.ServiceProvider.GetService<ISharedXafApplicationProvider>().Application.WhenFrameViewChanged()
                    .WhenFrame();
            }
        }

    }

    [NonParallelizable]
    public class JobSchedulerTests:JobSchedulerCommonTest{


        [TestCase(false)]
        [TestCase(true)]
        [XpandTest()]
        public void Customize_Job_Schedule(bool newObject) {
            GlobalConfiguration.Configuration.UseMemoryStorage();
            using var application = JobSchedulerModule().Application;
            var objectSpace = application.CreateObjectSpace();
            
            var scheduledJob = objectSpace.CreateObject<Job>();
            scheduledJob.Id = "test";
            var testObserver = JobSchedulerService.CustomJobSchedule.Handle().SelectMany(args => args.Instance).Test();
            objectSpace.CommitChanges();
            
            testObserver.ItemCount.ShouldBe(1);                           
            testObserver.Items.Last().ShouldBe(scheduledJob);
            if (!newObject) {
                scheduledJob.Id = "t";
                objectSpace.CommitChanges();
                testObserver.ItemCount.ShouldBe(2);
                testObserver.Items.Last().ShouldBe(scheduledJob);
            }
        }

        [TestCase(typeof(TestJobDI))]
        [TestCase(typeof(TestJob))]
        [XpandTest()]
        public async Task Inject_BlazorApplication_In_JobType_Ctor(Type testJobType) {
            MockHangfire().Test();
            var jobs = TestJob.Jobs.SubscribeReplay();
            using var application = JobSchedulerModule().Application.ToBlazor();
            application.CommitNewJob(testJobType);

            var testJob = await jobs.FirstAsync();

            if (testJobType==typeof(TestJobDI)) {
                testJob.Application.ShouldNotBeNull();
            }
            else {
                testJob.Application.ShouldBeNull();
            }
            
        }
        
        [Test()]
        [XpandTest()]
        public async Task Inject_PerformContext_In_JobType_Method() {
            MockHangfire().Test();
            var jobs = TestJob.Jobs.SubscribeReplay();
            using var application = JobSchedulerModule().Application.ToBlazor();
            var job = application.CommitNewJob(typeof(TestJob),nameof(TestJob.TestJobId));
            
            var testJob = await jobs.FirstAsync();

            testJob.Context.ShouldNotBeNull();
            testJob.Context.JobId().ShouldBe(job.Id);
            var objectSpace = application.CreateObjectSpace();
            job = objectSpace.GetObject(job);
            job.JobMethods.Count.ShouldBeGreaterThan(0);            
        }

        
        [Test][Apartment(ApartmentState.STA)]
        [XpandTest()]
        public async Task Schedule_Successful_job() {
            MockHangfire().Test();
            using var application = JobSchedulerModule().Application.ToBlazor();
            var observable = WorkerState.Succeeded.Executed().SubscribeReplay();
            
            application.CommitNewJob();
            
            var jobState = await observable;
            var objectSpace = application.CreateObjectSpace();
            jobState=objectSpace.GetObjectByKey<JobState>(jobState.Oid);
            var jobWorker = jobState.JobWorker;
            jobWorker.State.ShouldBe(WorkerState.Succeeded);
            
            jobWorker.Executions.Count(state => state.State==WorkerState.Processing).ShouldBe(1);
            jobWorker.Executions.Count(state => state.State==WorkerState.Failed).ShouldBe(0);
            jobWorker.Executions.Count(state => state.State==WorkerState.Succeeded).ShouldBe(1);
            
        }
        
        [TestCase(nameof(TestJob.FailMethodNoRetry),1,1,0)]
        [TestCase(nameof(TestJob.FailMethodRetry),2,1,0)]
        [XpandTest()]
        public async Task Schedule_Failed_Recurrent_job(string methodName,int executions,int failedJobs,int successFullJobs) {
            MockHangfire().Test();
            using var application = JobSchedulerModule().Application.ToBlazor();
            var execute = WorkerState.Failed.Executed().SubscribeReplay();
            application.CommitNewJob(methodName:methodName);
            using var objectSpace = application.CreateObjectSpace();

            var jobState = await execute;
            
            jobState=objectSpace.GetObjectByKey<JobState>(jobState.Oid);
            jobState.JobWorker.State.ShouldBe(WorkerState.Failed);
            
        }

        [XpandTest()][Test][Order(1)]
        public async Task Pause_Job() {
            MockHangfire().Test();
            using var application = JobSchedulerModule().Application.ToBlazor();
            var observable = WorkerState.Succeeded.Executed().SubscribeReplay();
            var jobsObserver = TestJob.Jobs.Test();
            application.CommitNewJob().Pause();

            await observable;

            jobsObserver.ItemCount.ShouldBe(0);
        }

        [XpandTest()][Test]
        public async Task Resume_Job() {
            MockHangfire().Test();
            using var application = JobSchedulerModule().Application.ToBlazor();
            var observable = WorkerState.Succeeded.Executed().SubscribeReplay();
            var jobsObserver = TestJob.Jobs.Test();
            application.CommitNewJob().Pause().Resume();

            await observable;

            jobsObserver.ItemCount.ShouldBe(1);
        }

        [XpandTest()][Test]
        public void JobPause_Action() {
            MockHangfire().Test();
            using var application = JobSchedulerModule().Application.ToBlazor();

            var job = application.CommitNewJob();
            var view = application.NewDetailView(job);
            var viewWindow = application.CreateViewWindow();
            viewWindow.SetView(view);

            var action = viewWindow.Action<JobSchedulerModule>().PauseJob();
            action.Active.ResultValue.ShouldBeTrue();
            action.DoExecute(space => new[]{job});
            
            job.IsPaused.ShouldBeTrue();
            view.ObjectSpace.Refresh();

            action.Active.ResultValue.ShouldBeFalse();
            viewWindow.Action<JobSchedulerModule>().ResumeJob().Active.ResultValue.ShouldBeTrue();
            
        }

        [XpandTest()][Test]
        public void JobResume_Action() {
            MockHangfire().Test();
            using var application = JobSchedulerModule().Application.ToBlazor();

            var job = application.CommitNewJob();
            var view = application.NewDetailView(job);
            var viewWindow = application.CreateViewWindow();
            viewWindow.SetView(view);

            var action = viewWindow.Action<JobSchedulerModule>().ResumeJob();
            action.Active.ResultValue.ShouldBeFalse();
            job.Pause();
            view.ObjectSpace.Refresh();
            action.Enabled.ResultValue.ShouldBeTrue();
            action.DoExecute(space => new[]{job});
            
            job.IsPaused.ShouldBeFalse();
            view.ObjectSpace.Refresh();

            action.Active.ResultValue.ShouldBeFalse();
            viewWindow.Action<JobSchedulerModule>().PauseJob().Active.ResultValue.ShouldBeTrue();
        }

    }
}