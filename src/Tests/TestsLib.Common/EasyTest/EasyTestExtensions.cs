﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Xml;
using DevExpress.EasyTest.Framework;
using Fasterflect;
using Newtonsoft.Json;
using Xpand.Extensions.LinqExtensions;
using Xpand.Extensions.ObjectExtensions;
using Xpand.Extensions.Reactive.ErrorHandling;
using Xpand.Extensions.XAF.XafApplicationExtensions;

namespace Xpand.TestsLib.Common.EasyTest{
    public static class EasyTestExtensions{
        

        public static Platform Platform(this TestApplication application) 
            => application.AdditionalAttributes.Any(_ => _.Name == "URL")
                ? application.Browser != "chrome" ? Xpand.Extensions.XAF.XafApplicationExtensions.Platform.Web :
                Xpand.Extensions.XAF.XafApplicationExtensions.Platform.Blazor : Xpand.Extensions.XAF.XafApplicationExtensions.Platform.Win;

        public static T ConvertTo<T>(this Command command) where T:Command{
            var t = (T)typeof(T).CreateInstance();
            var parameterList = command.Parameters;
            var extraParameterValue = parameterList?.ExtraParameter?.Value;
            if (extraParameterValue != null) {
                extraParameterValue = $"({extraParameterValue})";
            }
            var firstLine = $"{(!command.ExpectException ? "*" : "!")}{typeof(T).Name.Replace("Command","")} {parameterList?.MainParameter?.Value} {extraParameterValue}";
            var parameters =parameterList==null?new string[0]: parameterList.Select(parameter => $" {parameter.Name} = {parameter.Value}");
            var lines = new []{firstLine}.Concat(parameters).ToArray();
            var param = new CommandCreationParam(new ScriptStringList(lines), 0);
            t.ParseCommand(param);
            return t;
        }


        public static TestApplication GetTestApplication(this ICommandAdapter adapter) 
            => adapter.GetType().Name=="WebCommandAdapter" || adapter.IsInstanceOf("DevExpress.ExpressApp.EasyTest.BlazorAdapter.CommandAdapter")
                ? (TestApplication) EasyTestWebApplication.Instance : EasyTestWinApplication.Instance;

        public static IObservable<Unit> Execute(this ICommandAdapter adapter, Action retriedAction) 
            => Observable.Defer(() => Observable.Start(retriedAction)).RetryWithBackoff();

        public static void Execute(this ICommandAdapter adapter,int count, params Command[] commands){
            for (int i = 0; i < count; i++){
                adapter.Execute(commands);
            }
        }

        public static void Execute(this ICommandAdapter adapter,params Command[] commands){
            foreach (var command in commands){
                if (command is IRequireApplicationOptions requireApplicationOptions){
                    requireApplicationOptions.SetApplicationOptions(adapter.GetTestApplication());
                }
                try{
                    
                    ExecuteSilent(adapter, command);
                }
                catch (CommandException){
                    if(!command.ExpectException) {
                        throw;
                    }

                }
            }
        }

        [DebuggerNonUserCode][DebuggerStepThrough][DebuggerHidden]
        private static void ExecuteSilent(ICommandAdapter adapter, Command command){
            command.Execute(adapter);
        }

        public static string EasyTestSettingsFile(this TestApplication application){
            var path = application.AdditionalAttributes.FirstOrDefault(attribute => attribute.LocalName == "FileName")?.Value;
            path = path != null ? Path.GetDirectoryName(path) : Path.GetFullPath(application.AdditionalAttributes.First(attribute => attribute.LocalName=="PhysicalPath").Value);
            return $"{path}\\EasyTestSettings.json";
        }

        public static ICommandAdapter CreateCommandAdapter(this IApplicationAdapter adapter) => adapter.CreateCommandAdapter();

        public static void AddAttribute(this TestApplication testApplication, string name, string value){
            var document = new XmlDocument();
            var attribute = document.CreateAttribute(name);
            attribute.Value = value;
            testApplication.AdditionalAttributes = testApplication.AdditionalAttributes != null
                ? testApplication.AdditionalAttributes.Concat(attribute.YieldItem()).ToArray()
                : attribute.YieldItem().ToArray();
        }

        public static TestApplication RunBlazorApplication(this IApplicationAdapter adapter, string physicalPath, int port,
            string connectionString) {
            
            var testApplication = EasyTestWebApplication.New(physicalPath,port,false);
            testApplication.Browser = "chrome";
            testApplication.ConfigSettings(connectionString);
            testApplication.AddAttribute("Configuration","Debug");
            adapter.RunApplication(testApplication, "");

            // var browser = (IWebBrowser) new ChromeBrowser();
            // adapter.Driver.Navigate().GoToUrl($"https://localhost:{port}");
            // var serverResponseAwaiter = new BlazorAppResponseAwaiter().GetServerResponseAwaiter(adapter.Driver);
            // var wait = new WebDriverWait(adapter.Driver, TimeSpan.FromSeconds(30.0));
            // wait.Until(SeleniumUtils.ElementExists(By.ClassName("app")));
            // wait.Until(serverResponseAwaiter);
            return testApplication;
        }

        public static TestApplication RunWebApplication(this IApplicationAdapter adapter, string physicalPath, int port,string connectionString){
            var testApplication = EasyTestWebApplication.New(physicalPath,port);
            testApplication.ConfigSettings(connectionString);
            adapter.RunApplication(testApplication, null);
            return testApplication;
        }

        public static TestApplication RunWinApplication(this IApplicationAdapter adapter, string fileName,string connectionString){
            foreach (var file in Directory.GetFiles($"{Path.GetDirectoryName(fileName)}", "Model.User.xafml")){
                File.Delete(file);
            }
            var testApplication = EasyTestWinApplication.New(fileName);
            testApplication.ConfigSettings(connectionString);
            adapter.RunApplication(testApplication, null);
            return testApplication;
        }

        public static void ConfigSettings(this TestApplication application,string connectionString){
            File.WriteAllText(application.EasyTestSettingsFile(),
                JsonConvert.SerializeObject(new{ConnectionString = connectionString}));
        }

        public static TestApplication RunWinApplication(this IApplicationAdapter adapter, string fileName, int port = 4100){
            var testApplication = EasyTestWinApplication.New(fileName,port);
            adapter.RunApplication(testApplication, null);
            return testApplication;
        }
    }
}